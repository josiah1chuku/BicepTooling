using BicepTooling.Lexer;
using BicepTooling.Parser;
using BicepTooling.Semantic;
using BicepTooling.CodeGen;

string source = File.ReadAllText("Samples/hello.bicep");

// Pass 1
var tokens = new Lexer(source).Tokenize();
Console.WriteLine($"Pass 1: {tokens.Count} tokens");

// Pass 2
var ast = new Parser(tokens).ParseCompilationUnit();
Console.WriteLine($"Pass 2: {ast.Statements.Count} declarations");

// Pass 3
var symbols = new SymbolResolver().Resolve(ast);
Console.WriteLine($"Pass 3: {symbols.All.Count()} symbols");

// Pass 4
var checker = new TypeChecker(symbols);
checker.Check(ast);
Console.WriteLine($"Pass 4: {checker.Errors.Count} errors, " +
                  $"{checker.Warnings.Count} warnings, " +
                  $"{checker.Infos.Count} info\n");

// Pass 5
var armJson = new ArmGenerator().Generate(ast, symbols);
Console.WriteLine($"Pass 5: ARM JSON generated\n");

// Print diagnostics
if (checker.Errors.Count > 0)
{
    Console.WriteLine("=== ERRORS ===\n");
    foreach (var d in checker.Errors)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(d);
    }
    Console.ResetColor();
}

if (checker.Warnings.Count > 0)
{
    Console.WriteLine("=== WARNINGS ===\n");
    foreach (var d in checker.Warnings)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine(d);
    }
    Console.ResetColor();
}

if (checker.Infos.Count > 0)
{
    Console.WriteLine("=== INFO ===\n");
    foreach (var d in checker.Infos)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(d);
    }
    Console.ResetColor();
}

if (checker.Diagnostics.Count == 0)
    Console.WriteLine("No issues found!");

// Pass 7 - Security Linter
var linter = new BicepTooling.Semantic.SecurityLinter();
linter.Lint(ast);
Console.WriteLine($"Pass 7: {linter.Issues.Count} security issues\n");

if (linter.Issues.Count > 0)
{
    Console.WriteLine("=== SECURITY ISSUES ===\n");
    foreach (var issue in linter.Issues)
    {
        Console.ForegroundColor = issue.Severity == BicepTooling.Semantic.DiagnosticSeverity.Error
            ? ConsoleColor.Red : ConsoleColor.DarkYellow;
        Console.WriteLine(issue);
    }
    Console.ResetColor();
}

// Pass 6 - Explainer
var explainer = new BicepTooling.Semantic.BicepExplainer(symbols);
explainer.Explain(ast);

File.WriteAllText("Samples/azuredeploy.json", armJson);
Console.WriteLine("\nSaved to Samples/azuredeploy.json");
