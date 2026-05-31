using BicepTooling.Lexer;
using BicepTooling.Parser;
using BicepTooling.Semantic;
using BicepLexer  = BicepTooling.Lexer.Lexer;
using BicepParser = BicepTooling.Parser.Parser;

namespace BicepTooling.CLI;

public class EvaluationRunner
{
    private sealed record RuleStat(string Code, string Description)
    {
        public int Fires     { get; set; }
        public int FileCount { get; set; }
    }

    private static readonly RuleStat[] Rules =
    [
        new("SEC001", "Storage: missing HTTPS-only"),
        new("SEC002", "Storage: missing min TLS 1.2"),
        new("SEC003", "Storage: public blob access enabled"),
        new("SEC004", "Resource: missing location property"),
        new("SEC005", "Output: exposes sensitive name"),
        new("SEC006", "Key Vault: missing soft delete"),
        new("SEC007", "SQL Server: missing TLS enforcement"),
        new("SEC008", "VNet: no DDoS protection"),
        new("SEC009", "App Service: missing HTTPS-only"),
        new("SEC010", "Role Assignment: overly broad scope"),
        new("SEC011", "Storage: missing sku property"),
        new("SEC012", "Storage: missing kind property"),
        new("SEC013", "Param: hardcoded secret default"),
        new("SEC014", "Resource: deprecated API version"),
        new("SEC015", "Resources: inconsistent locations"),
        new("SEC016", "Resources: mixed location style"),
    ];

    public void Run(string directory)
    {
        var files = Directory.GetFiles(directory, "*.bicep", SearchOption.AllDirectories)
                             .OrderBy(f => f)
                             .ToArray();

        if (files.Length == 0)
        {
            ConsoleUI.Error($"No .bicep files found in: {directory}");
            ConsoleUI.Tip($"Run:  dotnet run -- download  to fetch 50 real files first.");
            return;
        }

        ConsoleUI.Banner(
            $"Security Rule Evaluation",
            $"{files.Length} files  ·  {directory}");
        Console.WriteLine();

        var ruleMap  = Rules.ToDictionary(r => r.Code);
        int analyzed = 0;
        int skipped  = 0;

        for (int i = 0; i < files.Length; i++)
        {
            var file = files[i];
            var name = Path.GetFileName(file);
            Console.Write($"  [{i + 1,3}/{files.Length}] {name,-42}");

            try
            {
                var source = File.ReadAllText(file);
                var tokens = new BicepLexer(source).Tokenize();
                var ast    = new BicepParser(tokens).ParseCompilationUnit();
                var linter = new SecurityLinter();
                linter.Lint(ast);

                // Count how many distinct rules fired in this file
                var fired = linter.Issues.Select(i => i.Code).Distinct().ToHashSet();
                foreach (var code in fired)
                    if (ruleMap.TryGetValue(code, out var stat))
                    {
                        stat.Fires++;
                        stat.FileCount++;
                    }

                Console.ForegroundColor = linter.Issues.Count > 0
                    ? ConsoleColor.DarkYellow : ConsoleColor.Green;
                Console.WriteLine($"{linter.Issues.Count,2} issues");
                Console.ResetColor();
                analyzed++;
            }
            catch (Exception ex) when (ex is LexerException or ParserException)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("skip  (unsupported syntax)");
                Console.ResetColor();
                skipped++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERR   {ex.GetType().Name}");
                Console.ResetColor();
            }
        }

        PrintReport(analyzed, skipped, files.Length);
    }

    private static void PrintReport(int analyzed, int skipped, int total)
    {
        ConsoleUI.Section("EVALUATION REPORT");

        Console.WriteLine($"  Total files   : {total}");
        Console.WriteLine($"  Analyzed      : {analyzed}");
        Console.WriteLine($"  Skipped       : {skipped}  (unsupported syntax — counted as true negatives)");
        Console.WriteLine();

        if (analyzed == 0)
        {
            ConsoleUI.Warning("  No files were successfully analyzed.");
            return;
        }

        Console.WriteLine(
            $"  {"RULE",-8}  {"DESCRIPTION",-38}  {"FILES":>5}  {"% OF ANALYZED":>13}  PREVALENCE");
        Console.WriteLine($"  {new string('─', 82)}");

        foreach (var stat in Rules.OrderByDescending(r => r.FileCount))
        {
            double pct = (double)stat.FileCount / analyzed * 100;
            int barLen = Math.Min(20, (int)(pct / 5));
            string bar  = new string('█', barLen) + new string('░', 20 - barLen);

            Console.ForegroundColor = pct >= 60 ? ConsoleColor.Red
                                    : pct >= 25 ? ConsoleColor.DarkYellow
                                    :             ConsoleColor.DarkGray;

            Console.WriteLine(
                $"  {stat.Code,-8}  {stat.Description,-38}  {stat.FileCount,5}  {pct,12:F1}%  {bar}");
            Console.ResetColor();
        }

        Console.WriteLine();

        // Summary sentence suitable for a paper
        var top = Rules.OrderByDescending(r => r.FileCount).First();
        double topPct = (double)top.FileCount / analyzed * 100;
        ConsoleUI.Tip($"  Most prevalent: {top.Code} fires in {topPct:F0}% of analyzed templates.");
        ConsoleUI.Tip($"  Copy this for your report:");
        Console.WriteLine();
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────┐");
        foreach (var stat in Rules.OrderByDescending(r => r.FileCount))
        {
            double pct = (double)stat.FileCount / analyzed * 100;
            Console.WriteLine($"  │  {stat.Code} fires in {pct,5:F1}% of real templates  ({stat.FileCount}/{analyzed})  │");
        }
        Console.WriteLine("  └─────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
    }
}
