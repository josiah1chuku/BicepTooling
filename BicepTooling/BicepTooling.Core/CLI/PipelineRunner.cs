using System.Diagnostics;
using BicepTooling.Parser;
using BicepTooling.Semantic;
using BicepTooling.CodeGen;
using BicepTooling.Lexer;
using BicepLexer  = BicepTooling.Lexer.Lexer;
using BicepParser = BicepTooling.Parser.Parser;

namespace BicepTooling.CLI;

public class PipelineRunner
{
    public void Analyze(string filePath)
    {
        if (!ResolveFile(filePath, out var source, out var resolved)) return;

        ConsoleUI.Banner($"Analyzing: {Path.GetFileName(resolved)}");
        Console.WriteLine();

        try
        {
            var (ast, symbols, checker, linter) = RunPasses(source);
            PrintDiagnostics(checker, linter);
            var outPath = Path.ChangeExtension(resolved, ".json");
            File.WriteAllText(outPath, new ArmGenerator().Generate(ast, symbols));
            Console.WriteLine();
            ConsoleUI.Success($"✓  ARM JSON saved → {outPath}");
        }
        catch (LexerException) { }
    }

    public void Check(string filePath)
    {
        if (!ResolveFile(filePath, out var source, out var resolved)) return;

        ConsoleUI.Banner($"Checking: {Path.GetFileName(resolved)}");
        Console.WriteLine();

        try
        {
            var (_, _, checker, linter) = RunPasses(source, armGen: false, explainer: false);
            PrintDiagnostics(checker, linter);
            Console.WriteLine();
            if (checker.Errors.Count == 0 && linter.Issues.All(i => i.Severity != DiagnosticSeverity.Error))
                ConsoleUI.Success("✓  No blocking errors found.");
            else
                ConsoleUI.Error($"✗  {checker.Errors.Count + linter.Issues.Count(i => i.Severity == DiagnosticSeverity.Error)} error(s) must be fixed before deployment.");
        }
        catch (LexerException) { }
    }

    public void Explain(string filePath)
    {
        if (!ResolveFile(filePath, out var source, out var resolved)) return;

        ConsoleUI.Banner($"Explaining: {Path.GetFileName(resolved)}");
        Console.WriteLine();

        try
        {
            var tokens  = new BicepLexer(source).Tokenize();
            var ast     = new BicepParser(tokens).ParseCompilationUnit();
            var symbols = new SymbolResolver().Resolve(ast);
            new BicepExplainer(symbols).Explain(ast);
        }
        catch (LexerException ex)
        {
            ConsoleUI.Section("SYNTAX ERROR");
            ConsoleUI.Error(ex.Message);
        }
    }

    private (CompilationUnitSyntax ast, SymbolTable symbols, TypeChecker checker, SecurityLinter linter)
        RunPasses(string source, bool armGen = true, bool explainer = false)
    {
        var sw = Stopwatch.StartNew();

        List<BicepTooling.Lexer.Token> tokens;
        try
        {
            tokens = new BicepLexer(source).Tokenize();
        }
        catch (LexerException ex)
        {
            ConsoleUI.PassFail("Lexer", $"syntax error  ({Elapsed(sw)})");
            ConsoleUI.Section("SYNTAX ERROR");
            ConsoleUI.Error(ex.Message);
            throw;
        }
        ConsoleUI.PassOk("Lexer",     $"{tokens.Count} tokens  ({Elapsed(sw)})");

        var ast = new BicepParser(tokens).ParseCompilationUnit();
        ConsoleUI.PassOk("Parser",    $"{ast.Statements.Count} declarations  ({Elapsed(sw)})");

        var symbols = new SymbolResolver().Resolve(ast);
        ConsoleUI.PassOk("Symbols",   $"{symbols.All.Count()} registered  ({Elapsed(sw)})");

        var checker = new TypeChecker(symbols);
        checker.Check(ast);
        var p4 = $"{checker.Errors.Count} errors  {checker.Warnings.Count} warnings  {checker.Infos.Count} tips  ({Elapsed(sw)})";
        if (checker.Errors.Count > 0) ConsoleUI.PassFail("TypeCheck", p4);
        else                          ConsoleUI.PassOk  ("TypeCheck", p4);

        if (armGen)
        {
            new ArmGenerator().Generate(ast, symbols);
            ConsoleUI.PassOk("ArmGen", $"ARM JSON built  ({Elapsed(sw)})");
        }

        var linter = new SecurityLinter();
        linter.Lint(ast);
        var p6 = $"{linter.Issues.Count} issues  ({Elapsed(sw)})";
        if (linter.Issues.Count > 0) ConsoleUI.PassWarn("Security", p6);
        else                         ConsoleUI.PassOk  ("Security", p6);

        if (explainer)
            new BicepExplainer(symbols).Explain(ast);

        return (ast, symbols, checker, linter);
    }

    public void Evaluate(string directory) => new EvaluationRunner().Run(directory);

    private static void PrintDiagnostics(TypeChecker checker, SecurityLinter linter)
    {
        if (checker.Errors.Any())
        {
            ConsoleUI.Section("ERRORS");
            foreach (var d in checker.Errors)   ConsoleUI.Error(d.ToString());
        }
        if (checker.Warnings.Any())
        {
            ConsoleUI.Section("WARNINGS");
            foreach (var d in checker.Warnings) ConsoleUI.Warning(d.ToString());
        }
        if (checker.Infos.Any())
        {
            ConsoleUI.Section("TIPS");
            foreach (var d in checker.Infos)    ConsoleUI.Info(d.ToString());
        }
        if (linter.Issues.Any())
        {
            ConsoleUI.Section("SECURITY");
            foreach (var issue in linter.Issues)
            {
                if (issue.Severity == DiagnosticSeverity.Error) ConsoleUI.Error(issue.ToString());
                else                                             ConsoleUI.Warning(issue.ToString());
            }
        }
    }

    private static bool ResolveFile(string path, out string source, out string resolved)
    {
        resolved = path;
        if (!File.Exists(path))
        {
            ConsoleUI.Error($"File not found: {path}");
            source = "";
            return false;
        }
        source = File.ReadAllText(path);
        return true;
    }

    private static string Elapsed(Stopwatch sw)
    {
        var ms = sw.ElapsedMilliseconds;
        sw.Restart();
        return $"{ms}ms";
    }
}
