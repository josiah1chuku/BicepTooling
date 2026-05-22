# BicepTooling

A multipass compiler for Azure Bicep Infrastructure-as-Code files, built in C# (.NET 10).

## Pipeline

- Pass 1 — Lexer: tokenizes .bicep source files
- Pass 2 — Parser: builds an Abstract Syntax Tree (AST)
- Pass 3 — Symbol Resolution: registers all declared names
- Pass 4 — Type Checker: validates type correctness
- Pass 5 — Code Generator: outputs deployable ARM JSON

## Usage

dotnet run --project BicepTooling.Core

## Tests

dotnet test

## Built With

- C# / .NET 10
- xUnit for testing
# CI enabled
