# Evaluation Results — Azure Quickstart Templates

**Date:** May 29, 2026  
**Source:** Azure/azure-quickstart-templates (official Microsoft repo)  
**Files downloaded:** 50  
**Successfully analyzed:** 12 (24%)  
**Skipped (advanced syntax):** 38 (76%)  
**False positives:** 0  

## Security Rule Detection

| Rule | Description | Files | % of Analyzed |
|------|-------------|-------|----------------|
| SEC001 | Storage: missing HTTPS-only | 1 | 8.3% |
| SEC002 | Storage: missing TLS 1.2 | 1 | 8.3% |
| SEC004 | Resource: missing location | 1 | 8.3% |

## Notable Finding
`demos_cloud-shell-vnet-storage_main.bicep` — 10 security issues detected in a single official Microsoft template.
