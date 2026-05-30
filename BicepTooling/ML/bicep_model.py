"""
bicep_model.py — CodeBERT multi-label classifier for Bicep security rules.

Architecture:
  CodeBERT CLS token → 512-dim projection → 2-layer MLP → 10 binary outputs
  (one per SEC rule, sigmoid at inference / BCEWithLogitsLoss during training)

Simplified from VulnDetector: no R-GCN graph branch.
Rationale: Bicep security rules depend on missing/present properties,
not data flow — text-only CodeBERT captures this well.
"""

import torch
import torch.nn as nn
from transformers import RobertaModel

RULE_NAMES = [
    "SEC001", "SEC002", "SEC003", "SEC004", "SEC005",
    "SEC006", "SEC007", "SEC008", "SEC009", "SEC010",
]
N_RULES = len(RULE_NAMES)

# Rules with >= 20 positives in the 493-sample corpus — the ones to train on.
# SEC003 (0), SEC005 (6), SEC007 (7), SEC010 (0) excluded as too few positives.
TRAINABLE_IDX   = [0, 1, 3, 5, 7, 8]          # indices into RULE_NAMES
TRAINABLE_RULES = [RULE_NAMES[i] for i in TRAINABLE_IDX]


class BicepSecurityDetector(nn.Module):
    def __init__(self, hidden: int = 512, drop: float = 0.3):
        super().__init__()
        self.encoder    = RobertaModel.from_pretrained("microsoft/codebert-base")
        self.text_proj  = nn.Linear(768, hidden)
        self.drop       = nn.Dropout(drop)
        self.classifier = nn.Sequential(
            nn.Linear(hidden, hidden // 2),
            nn.ReLU(),
            nn.Dropout(drop),
            nn.Linear(hidden // 2, N_RULES),
        )

    def forward(self, ids: torch.Tensor, mask: torch.Tensor) -> torch.Tensor:
        cls = self.encoder(ids, attention_mask=mask).last_hidden_state[:, 0, :]
        h   = torch.relu(self.text_proj(cls))
        return self.classifier(self.drop(h))  # raw logits — no sigmoid

    def freeze_encoder(self, freeze_up_to: int = 10) -> None:
        for p in self.encoder.embeddings.parameters():
            p.requires_grad = False
        for i, layer in enumerate(self.encoder.encoder.layer):
            if i < freeze_up_to:
                for p in layer.parameters():
                    p.requires_grad = False
        trainable = sum(p.numel() for p in self.parameters() if p.requires_grad)
        total     = sum(p.numel() for p in self.parameters())
        print(f"Frozen layers 0-{freeze_up_to - 1} | "
              f"Trainable: {trainable:,} / {total:,}")
