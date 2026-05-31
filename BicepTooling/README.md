# BicepTooling

A static analysis tool for Azure Bicep templates that catches security misconfigurations, type errors, and cross-resource dependency issues before deployment.

Built in C# / .NET 10. Evaluated on 500 real-world Bicep templates from the Azure Quickstart Templates repository.

---

## Quick Start

```bash
dotnet run --project BicepTooling.Core -- check   <file.bicep>
dotnet run --project BicepTooling.Core -- analyze <file.bicep>
dotnet run --project BicepTooling.Core -- explain <file.bicep>
dotnet run --project BicepTooling.Core -- eval    <directory>
```

### Run the test suite
```bash
dotnet test
```

---

## What It Catches

### Type Checker — BCP001 to BCP011

| Code   | Severity | Description |
|--------|----------|-------------|
| BCP001 | Error    | Parameter default value does not match declared type |
| BCP002 | Error    | Undefined identifier (with fuzzy "Did you mean?" suggestion) |
| BCP003 | Error    | Duplicate symbol declaration |
| BCP004 | Error    | Resource type string missing `@apiVersion` |
| BCP005 | Info     | Required parameter (no default value) |
| BCP006 | Warning  | Output type mismatch |
| BCP007 | Warning  | `.id` or `.outputs` accessed on a variable or parameter, not a resource |
| BCP008 | Error    | Module file not found |
| BCP009 | Error    | Unknown parameter passed to module |
| BCP010 | Error    | Required module parameter not provided |
| BCP011 | Warning  | Type mismatch in module parameter |

### Security Linter — SEC001 to SEC016

| Code   | Severity | Rule | Compliance |
|--------|----------|------|------------|
| SEC001 | Warning  | Storage: missing HTTPS-only enforcement | CJIS 5.10 |
| SEC002 | Warning  | Storage: missing minimum TLS 1.2 | FIPS 140-2 |
| SEC003 | Error    | Storage: public blob access enabled | NIST AC-3 |
| SEC004 | Warning  | Resource: missing explicit location | CJIS / FedRAMP |
| SEC005 | Warning  | Output exposes sensitive credential name | NIST SC-12 |
| SEC006 | Error    | Key Vault: missing soft delete / purge protection | NIST CP-9 |
| SEC007 | Error    | SQL Server: missing minimum TLS version | CJIS 5.10 |
| SEC008 | Warning  | Virtual Network: no DDoS protection plan | NIST SC-5 |
| SEC009 | Error    | App Service: missing HTTPS-only flag | CJIS 5.10 |
| SEC010 | Error    | Role Assignment: Owner or Contributor scope | NIST AC-6 |
| SEC011 | Error    | Storage: missing required `sku` property | Deployment safety |
| SEC012 | Warning  | Storage: missing `kind` property | Deployment safety |
| SEC013 | Error    | Parameter: hardcoded secret as default value | NIST SC-12 |
| SEC014 | Warning  | Resource: deprecated API version in use | CJIS compliance |
| SEC015 | Warning  | Resources: inconsistent hardcoded locations | FedRAMP |
| SEC016 | Warning  | Resources: mixed hardcoded and param-based locations | Consistency |

---

## Evaluation Results

Evaluated on **500 real Bicep templates** from the Azure Quickstart Templates repository (493 successfully analyzed, 7 skipped for unsupported syntax).

| Rule   | Description                          | Files | % of Analyzed |
|--------|--------------------------------------|------:|---------------|
| SEC004 | Resource: missing location property  |   215 | **43.6%**     |
| SEC014 | Resource: deprecated API version     |   147 | **29.8%**     |
| SEC008 | VNet: no DDoS protection             |   131 | **26.6%**     |
| SEC002 | Storage: missing min TLS 1.2         |    66 | 13.4%         |
| SEC001 | Storage: missing HTTPS-only          |    64 | 13.0%         |
| SEC016 | Resources: mixed location style      |    43 |  8.7%         |
| SEC009 | App Service: missing HTTPS-only      |    39 |  7.9%         |
| SEC006 | Key Vault: missing soft delete       |    20 |  4.1%         |
| SEC011 | Storage: missing sku property        |    20 |  4.1%         |
| SEC012 | Storage: missing kind property       |    20 |  4.1%         |
| SEC007 | SQL Server: missing TLS              |     7 |  1.4%         |
| SEC005 | Output: exposes sensitive name       |     6 |  1.2%         |
| SEC015 | Resources: inconsistent locations    |     3 |  0.6%         |
| SEC003 | Storage: public blob access enabled  |     0 |  0.0%         |
| SEC010 | Role Assignment: overly broad scope  |     0 |  0.0%         |
| SEC013 | Param: hardcoded secret default      |     0 |  0.0%         |

**Key finding:** SEC004 fires in 43.6% of templates and SEC014 in 29.8% — nearly 1 in 3 real-world templates reference deprecated Azure API versions.

---

## Test Suite

**76 tests — all passing.**

| Test Class           | Tests | Coverage |
|----------------------|------:|---------|
| LexerTests           |     7 | Tokenisation of all token types |
| ParserTests          |     7 | All declaration and expression forms |
| TypeCheckerTests     |    36 | BCP001–007 positive and negative, line numbers, fuzzy suggestion |
| SecurityLinterTests  |    29 | SEC001–016 positive and negative |
| ModuleCheckerTests   |    11 | BCP008–011, clean module, optional params |

---

## Architecture

```
BicepTooling.Core/
├── Lexer/
│   ├── Lexer.cs           — tokeniser; tracks line/column
│   ├── Token.cs           — token with location info
│   └── TokenKind.cs       — all token types
├── Parser/
│   ├── Parser.cs          — recursive descent parser
│   └── SyntaxNodes.cs     — AST node types (all carry Line/Column)
├── Semantic/
│   ├── SymbolTable.cs     — symbol registration and lookup
│   ├── TypeChecker.cs     — BCP001–007; Levenshtein fuzzy suggestions
│   ├── SecurityLinter.cs  — SEC001–016; CJIS/NIST/FedRAMP rules
│   ├── ModuleChecker.cs   — BCP008–011; cross-file param type checking
│   └── DiagnosticMessage.cs — structured What/Why/Fix/Rule messages
├── CodeGen/
│   └── ArmGenerator.cs    — Bicep → ARM JSON
└── CLI/
    ├── PipelineRunner.cs  — orchestrates all passes
    └── EvaluationRunner.cs — batch evaluation with prevalence report
```

### Pipeline Passes

```
Source → Lexer → Parser → SymbolResolver → TypeChecker → SecurityLinter → ModuleChecker → ArmGenerator
```

Each pass produces `DiagnosticMessage` objects with severity (Error/Warning/Info), a BCP/SEC code, and a structured explanation including the exact line and column.

---

## Example Output

```
[ERROR] BCP002: Undefined reference 'undeclaredVar' in var 'result'
  Location : [Line 3, Col 14]
  Problem  : You used 'undeclaredVar' but it was never declared as a param, var, or resource.
  Fix      : Add a declaration for 'undeclaredVar (Did you mean 'storageSku'?)':
             param undeclaredVar string   if it is an input
             var undeclaredVar = 'value'  if it is a variable

[WARNING] SEC014: Resource 'storage' uses API version '2019-04-01' — '2023-01-01' is available
  Problem  : Older API versions may lack security properties required for CJIS compliance.
  Fix      : Update to: 'Microsoft.Storage/storageAccounts@2023-01-01'
```
