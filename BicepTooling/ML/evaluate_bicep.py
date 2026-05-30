"""
evaluate_bicep.py — Per-rule evaluation on the held-out test set.

Usage:
  python ML/evaluate_bicep.py \
      --test       ML/data/test.csv \
      --checkpoint ML/checkpoints/best_bicep_model.pt

Prints the table that goes into the paper.
"""

import argparse, os, sys
import numpy as np
import torch
from sklearn.metrics import (
    roc_auc_score, average_precision_score, f1_score,
    precision_score, recall_score,
)
from torch.utils.data import DataLoader
from transformers import RobertaTokenizer

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from ML.bicep_model import BicepSecurityDetector, RULE_NAMES, TRAINABLE_IDX
from ML.train_bicep import BicepDataset


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--test",       default="ML/data/test.csv")
    p.add_argument("--checkpoint", default="ML/checkpoints/best_bicep_model.pt")
    p.add_argument("--threshold",  type=float, default=0.5)
    p.add_argument("--batch_size", type=int,   default=8)
    p.add_argument("--max_len",    type=int,   default=512)
    args = p.parse_args()

    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

    tok     = RobertaTokenizer.from_pretrained("microsoft/codebert-base")
    test_ds = BicepDataset(args.test, tok, args.max_len)
    test_dl = DataLoader(test_ds, batch_size=args.batch_size, shuffle=False)

    model = BicepSecurityDetector().to(device)
    model.load_state_dict(torch.load(args.checkpoint, map_location=device))
    model.eval()

    all_labels, all_probs = [], []

    with torch.no_grad():
        for batch in test_dl:
            ids  = batch["input_ids"].to(device)
            mask = batch["attention_mask"].to(device)
            lbl  = batch["labels"].numpy()
            prob = torch.sigmoid(model(ids, mask)).cpu().numpy()
            all_labels.append(lbl)
            all_probs.append(prob)

    all_labels = np.vstack(all_labels)
    all_probs  = np.vstack(all_probs)
    all_preds  = (all_probs >= args.threshold).astype(int)

    print(f"\n{'─'*72}")
    print(f"{'RULE':<8}  {'AUC-ROC':>8}  {'AP':>8}  {'F1':>8}  "
          f"{'Prec':>8}  {'Rec':>8}  {'Pos/N':>8}")
    print(f"{'─'*72}")

    auc_scores = []
    for i, rule in enumerate(RULE_NAMES):
        pos = int(all_labels[:, i].sum())
        n   = len(all_labels)
        if pos == 0:
            print(f"{rule:<8}  {'—':>8}  {'—':>8}  {'—':>8}  "
                  f"{'—':>8}  {'—':>8}  {pos:>4}/{n}")
            continue
        try:
            auc  = roc_auc_score(all_labels[:, i], all_probs[:, i])
            ap   = average_precision_score(all_labels[:, i], all_probs[:, i])
            f1   = f1_score(all_labels[:, i], all_preds[:, i], zero_division=0)
            prec = precision_score(all_labels[:, i], all_preds[:, i], zero_division=0)
            rec  = recall_score(all_labels[:, i], all_preds[:, i], zero_division=0)
            print(f"{rule:<8}  {auc:>8.4f}  {ap:>8.4f}  {f1:>8.4f}  "
                  f"{prec:>8.4f}  {rec:>8.4f}  {pos:>4}/{n}")
            if i in TRAINABLE_IDX:
                auc_scores.append(auc)
        except Exception as e:
            print(f"{rule:<8}  error: {e}")

    print(f"{'─'*72}")
    if auc_scores:
        print(f"{'Macro AUC (trainable rules)':<40}  {np.mean(auc_scores):.4f}")
    print()

    # Compact table for paper
    print("=== PAPER TABLE ===")
    print(f"{'Rule':<8}  {'AUC-ROC':>8}  {'AP':>8}")
    for i, rule in enumerate(RULE_NAMES):
        if int(all_labels[:, i].sum()) == 0:
            continue
        try:
            auc = roc_auc_score(all_labels[:, i], all_probs[:, i])
            ap  = average_precision_score(all_labels[:, i], all_probs[:, i])
            print(f"{rule:<8}  {auc:>8.4f}  {ap:>8.4f}")
        except:
            pass
    if auc_scores:
        print(f"{'Macro':<8}  {np.mean(auc_scores):>8.4f}")


if __name__ == "__main__":
    main()
