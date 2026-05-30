# HALIX Assembler

![CI](https://github.com/josiah1chuku/halix-assembler/actions/workflows/ci.yml/badge.svg)

A complete five-pass assembler for the HALIX Assembly Language, implemented in C++17. Translates `.hal` source files into machine code `.hlx` files compatible with the legacy `halix.crun` v1.7 runtime.

---

## Quick Start

### Compile
```bash
g++ -std=c++17 -o halixAssembler halixAssembler.cpp
```

### Assemble a program
```bash
./halixAssembler myprogram.hal
```

### Run all tests
```bash
bash tests/run_all_tests.sh
```

---

## Output Files

| File | Description |
|---|---|
| `<n>.hlx` | Machine code — compatible with halix.crun |
| `<n>.lst` | Assembly listing — source mapped to machine code |
| `<n>.log` | Error log — "NO errors" on success |

---

## .hlx Output Format (Legacy)
```
codeSize
instruction_1
...
dataSize
dataValue_1
...
```

Uninitialised variables are written as `9999`.

---

## Assembler Passes

| Pass | Description |
|---|---|
| Pass 0 | Directive validation (.ALLOC, .BEGIN, .END) |
| Pass 1 | Data Symbol Table construction |
| Pass 2a | Mnemonic validation against halix.opcode |
| Pass 2b | Instruction Label Table construction |
| Pass 3 | Operand validation |
| Pass 4 | Machine code generation |

---

## CI/CD Pipeline

Every push to GitHub automatically:
1. Spins up a fresh Ubuntu Linux machine
2. Installs g++ and compiles the assembler
3. Runs all 36 tests
4. Assembles a demo program

The badge at the top shows the current pipeline status.

---

## Project Structure
```
halix-assembler/
├── halixAssembler.cpp     # Assembler source code
├── halix.opcode           # Instruction set configuration
├── tests/
│   ├── run_all_tests.sh   # Automated test runner
│   ├── tc_p0_*.hal        # Pass 0 test cases (6)
│   ├── tc_p1_*.hal        # Pass 1 test cases (8)
│   ├── tc_p2a_*.hal       # Pass 2a test cases (6)
│   ├── tc_p3_*.hal        # Pass 3 test cases (8)
│   └── tc_p4_*.hal        # Pass 4 test cases (8)
├── SRS_HALIX_v2.docx
├── SDD_HALIX_v2.docx
├── TPD_HALIX_v2.docx
├── DPD_Halix_v1.docx
└── MPD_Halix_v1.docx
```

---

## SDLC Documents

| Document | Version | Description |
|---|---|---|
| SRS | v2.0 | 40 functional requirements, 12 non-functional requirements |
| SDD | v2.0 | Five-pass pipeline architecture, data structures |
| TPD | v2.0 | 36 automated test cases, all passing, halix.crun verified |
| DPD | v1.0 | Deployment package, GitHub release process |
| MPD | v1.0 | Maintenance procedures, versioning, roadmap |

---

## References

- E. L. Jones, *HALIX COMPUTER version 12 — Instruction Set*
- E. L. Jones, *The HALIX ASSEMBLER (HASM.csh)*, AnatomyOfAnAssembler.txt
- E. L. Jones, *halix.crun v1.7*, 2011
