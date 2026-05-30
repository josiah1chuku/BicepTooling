"""
prepare_bicep_data.py — Split labels.csv into train / val / test sets.

Usage:
  python ML/prepare_bicep_data.py \
      --csv BicepTooling.Core/Samples/ml_data/labels.csv \
      --out  ML/data/

Stratified split on the any_finding column (70 / 15 / 15).
Rows with blank labels (parse failures) are dropped.
"""

import argparse
import os
import pandas as pd
from sklearn.model_selection import train_test_split


RULE_NAMES = [
    "SEC001", "SEC002", "SEC003", "SEC004", "SEC005",
    "SEC006", "SEC007", "SEC008", "SEC009", "SEC010",
]


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--csv", default="BicepTooling.Core/Samples/ml_data/labels.csv")
    p.add_argument("--out", default="ML/data")
    p.add_argument("--seed", type=int, default=42)
    args = p.parse_args()

    os.makedirs(args.out, exist_ok=True)

    df = pd.read_csv(args.csv)

    # Drop rows where labels were not generated (parse failures)
    df = df[df["any_finding"].notna() & (df["any_finding"] != "")].reset_index(drop=True)
    print(f"Usable rows: {len(df)}")

    # Ensure label columns are int
    for r in RULE_NAMES:
        df[r] = df[r].astype(int)

    # Stratify on any_finding (binary: has at least one finding)
    stratify_col = df["any_finding"].astype(int)

    train_df, tmp_df = train_test_split(
        df, test_size=0.30, random_state=args.seed, stratify=stratify_col
    )
    val_stratify = tmp_df["any_finding"].astype(int)
    val_df, test_df = train_test_split(
        tmp_df, test_size=0.50, random_state=args.seed, stratify=val_stratify
    )

    train_df.to_csv(os.path.join(args.out, "train.csv"), index=False)
    val_df.to_csv(os.path.join(args.out, "val.csv"),   index=False)
    test_df.to_csv(os.path.join(args.out, "test.csv"),  index=False)

    print(f"Train: {len(train_df)}  Val: {len(val_df)}  Test: {len(test_df)}")
    print("\nLabel distribution in train set:")
    for r in RULE_NAMES:
        pos = train_df[r].sum()
        pct = pos / len(train_df) * 100
        print(f"  {r}: {pos:3d} positives ({pct:.1f}%)")


if __name__ == "__main__":
    main()
