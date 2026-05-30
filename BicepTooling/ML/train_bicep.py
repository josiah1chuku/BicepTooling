"""
train_bicep.py — Fine-tune CodeBERT for multi-label Bicep security rule detection.

Usage:
  python ML/train_bicep.py \
      --train ML/data/train.csv \
      --val   ML/data/val.csv   \
      --out   ML/checkpoints/

Target: AUC-ROC > 0.75 per trainable rule (SEC001, SEC002, SEC004, SEC006, SEC008, SEC009).
"""

import argparse, json, os, sys
import numpy as np
import pandas as pd
import torch
import torch.nn as nn
from sklearn.metrics import roc_auc_score, average_precision_score, f1_score
from torch.optim import AdamW
from torch.utils.data import DataLoader, Dataset
from transformers import RobertaTokenizer, get_linear_schedule_with_warmup
from tqdm import tqdm

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from ML.bicep_model import BicepSecurityDetector, RULE_NAMES, N_RULES, TRAINABLE_IDX


# ── DATASET ─────────────────────────────────────────────────────────────────

class BicepDataset(Dataset):
    def __init__(self, csv_path: str, tok, max_len: int = 512):
        df = pd.read_csv(csv_path)
        df = df[df["any_finding"].notna() & (df["any_finding"] != "")].reset_index(drop=True)
        self.sources = df["source"].tolist()
        self.labels  = df[RULE_NAMES].astype(int).values.astype(np.float32)
        self.tok     = tok
        self.max_len = max_len
        print(f"  {len(df):,} samples: {os.path.basename(csv_path)}")

    def __len__(self):
        return len(self.sources)

    def __getitem__(self, idx):
        # Unescape newlines stored as \n in CSV
        source = str(self.sources[idx]).replace("\\n", "\n")
        enc = self.tok(
            source,
            max_length=self.max_len,
            padding="max_length",
            truncation=True,
            return_tensors="pt",
        )
        return {
            "input_ids":      enc["input_ids"].squeeze(0),
            "attention_mask": enc["attention_mask"].squeeze(0),
            "labels":         torch.tensor(self.labels[idx]),
        }


# ── METRICS ──────────────────────────────────────────────────────────────────

def compute_metrics(all_labels: np.ndarray, all_probs: np.ndarray) -> dict:
    """Per-rule AUC-ROC and AP; macro average over trainable rules only."""
    results = {}
    auc_list, ap_list = [], []

    for i, rule in enumerate(RULE_NAMES):
        pos = all_labels[:, i].sum()
        if pos == 0 or pos == len(all_labels):
            results[rule] = {"auc": None, "ap": None, "positives": int(pos)}
            continue
        try:
            auc = roc_auc_score(all_labels[:, i], all_probs[:, i])
            ap  = average_precision_score(all_labels[:, i], all_probs[:, i])
            results[rule] = {"auc": round(auc, 4), "ap": round(ap, 4),
                             "positives": int(pos)}
            if i in TRAINABLE_IDX:
                auc_list.append(auc)
                ap_list.append(ap)
        except Exception as e:
            results[rule] = {"auc": None, "ap": None, "error": str(e)}

    results["macro_auc"] = round(float(np.mean(auc_list)), 4) if auc_list else None
    results["macro_ap"]  = round(float(np.mean(ap_list)),  4) if ap_list  else None
    return results


# ── EPOCH ────────────────────────────────────────────────────────────────────

def run_epoch(model, loader, criterion, optimizer, scheduler, device, train: bool):
    model.train() if train else model.eval()
    total_loss   = 0.0
    all_labels   = []
    all_probs    = []

    ctx = torch.enable_grad() if train else torch.no_grad()
    with ctx:
        for batch in tqdm(loader, desc="train" if train else "val  ", leave=False):
            ids  = batch["input_ids"].to(device)
            mask = batch["attention_mask"].to(device)
            lbl  = batch["labels"].to(device)

            if train:
                optimizer.zero_grad()

            logits = model(ids, mask)
            loss   = criterion(logits, lbl)

            if train:
                loss.backward()
                nn.utils.clip_grad_norm_(model.parameters(), 1.0)
                optimizer.step()
                scheduler.step()

            total_loss += loss.item()
            all_labels.append(lbl.cpu().numpy())
            all_probs.append(torch.sigmoid(logits).cpu().detach().numpy())

    all_labels = np.vstack(all_labels)
    all_probs  = np.vstack(all_probs)
    metrics    = compute_metrics(all_labels, all_probs)
    return total_loss / len(loader), metrics


# ── MAIN ─────────────────────────────────────────────────────────────────────

def main():
    p = argparse.ArgumentParser()
    p.add_argument("--train",       default="ML/data/train.csv")
    p.add_argument("--val",         default="ML/data/val.csv")
    p.add_argument("--out",         default="ML/checkpoints")
    p.add_argument("--epochs",      type=int,   default=10)
    p.add_argument("--batch_size",  type=int,   default=8)
    p.add_argument("--lr",          type=float, default=2e-5)
    p.add_argument("--max_len",     type=int,   default=512)
    p.add_argument("--freeze",      type=int,   default=10)
    p.add_argument("--patience",    type=int,   default=3)
    p.add_argument("--seed",        type=int,   default=42)
    args = p.parse_args()

    torch.manual_seed(args.seed)
    np.random.seed(args.seed)
    os.makedirs(args.out, exist_ok=True)

    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print(f"Device: {device}")

    tok = RobertaTokenizer.from_pretrained("microsoft/codebert-base")

    train_ds = BicepDataset(args.train, tok, args.max_len)
    val_ds   = BicepDataset(args.val,   tok, args.max_len)

    train_dl = DataLoader(train_ds, batch_size=args.batch_size, shuffle=True)
    val_dl   = DataLoader(val_ds,   batch_size=args.batch_size, shuffle=False)

    model = BicepSecurityDetector().to(device)
    model.encoder.gradient_checkpointing_enable()
    model.freeze_encoder(args.freeze)

    # Class-weighted BCE: weight each rule by neg/pos ratio from training set
    train_df   = pd.read_csv(args.train)
    pos_counts = train_df[RULE_NAMES].astype(int).sum()
    neg_counts = len(train_df) - pos_counts
    pos_weight = torch.tensor(
        [neg_counts[r] / max(pos_counts[r], 1) for r in RULE_NAMES],
        dtype=torch.float32,
    ).to(device)
    criterion = nn.BCEWithLogitsLoss(pos_weight=pos_weight)

    optimizer = AdamW(
        filter(lambda p: p.requires_grad, model.parameters()),
        lr=args.lr, weight_decay=0.01,
    )
    total_steps = len(train_dl) * args.epochs
    scheduler   = get_linear_schedule_with_warmup(
        optimizer, num_warmup_steps=total_steps // 10,
        num_training_steps=total_steps,
    )

    best_auc = 0.0
    patience = 0
    history  = []

    print(f"\nTraining {args.epochs} epochs | {len(train_dl)} batches/epoch\n")

    for epoch in range(1, args.epochs + 1):
        tr_loss, tr_m = run_epoch(model, train_dl, criterion, optimizer,
                                   scheduler, device, train=True)
        vl_loss, vl_m = run_epoch(model, val_dl,   criterion, optimizer,
                                   scheduler, device, train=False)

        macro = vl_m["macro_auc"] or 0.0
        print(f"Epoch {epoch:2d}/{args.epochs} | "
              f"Train loss: {tr_loss:.4f} | Val loss: {vl_loss:.4f} | "
              f"Val macro AUC: {macro:.4f}")

        for rule in RULE_NAMES:
            r = vl_m.get(rule, {})
            if r.get("auc") is not None:
                print(f"  {rule}: AUC={r['auc']:.4f}  AP={r['ap']:.4f}  "
                      f"pos={r['positives']}")

        history.append({"epoch": epoch, "val_macro_auc": macro,
                        "val_loss": vl_loss, "per_rule": vl_m})

        if macro > best_auc:
            best_auc  = macro
            patience  = 0
            ckpt_path = os.path.join(args.out, "best_bicep_model.pt")
            torch.save(model.state_dict(), ckpt_path)
            print(f"  ✓ New best macro AUC: {best_auc:.4f} — saved")
        else:
            patience += 1
            if patience >= args.patience:
                print("Early stopping.")
                break

    with open(os.path.join(args.out, "bicep_history.json"), "w") as f:
        json.dump(history, f, indent=2)

    best = max(history, key=lambda x: x["val_macro_auc"] or 0)
    print(f"\nBest val macro AUC: {best['val_macro_auc']:.4f}  "
          f"(epoch {best['epoch']})")


if __name__ == "__main__":
    main()
