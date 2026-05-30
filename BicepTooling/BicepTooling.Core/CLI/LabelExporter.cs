using BicepTooling.Semantic;
using BicepLexer  = BicepTooling.Lexer.Lexer;
using BicepParser = BicepTooling.Parser.Parser;

namespace BicepTooling.CLI;

public class LabelExporter
{
    private static readonly string[] Rules =
        ["SEC001","SEC002","SEC003","SEC004","SEC005",
         "SEC006","SEC007","SEC008","SEC009","SEC010"];

    public void Export(string directory, string outputCsv)
    {
        var files = Directory.GetFiles(directory, "*.bicep", SearchOption.AllDirectories)
                             .OrderBy(f => f)
                             .ToArray();

        if (files.Length == 0)
        {
            ConsoleUI.Error($"No .bicep files found in: {directory}");
            return;
        }

        ConsoleUI.Banner(
            "Label Exporter — CodeBERT Training Data",
            $"{files.Length} files  →  {outputCsv}");
        Console.WriteLine();

        int ok = 0, skipped = 0;

        using (var writer = new StreamWriter(outputCsv))
        {
            writer.WriteLine("filename," + string.Join(",", Rules) + ",any_finding,source");

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var name = Path.GetFileName(file);
                Console.Write($"  [{i + 1,3}/{files.Length}] {name,-52}");

                try
                {
                    var source  = File.ReadAllText(file);
                    var tokens  = new BicepLexer(source).Tokenize();
                    var ast     = new BicepParser(tokens).ParseCompilationUnit();
                    var linter  = new SecurityLinter();
                    linter.Lint(ast);

                    var fired      = linter.Issues.Select(issue => issue.Code).ToHashSet();
                    var labels     = Rules.Select(r => fired.Contains(r) ? "1" : "0").ToArray();
                    int anyFinding = fired.Count > 0 ? 1 : 0;

                    var csvSource = "\"" + source.Replace("\"", "\"\"")
                                                 .Replace("\r\n", "\\n")
                                                 .Replace("\n", "\\n") + "\"";

                    writer.WriteLine(
                        $"{name}," + string.Join(",", labels) + $",{anyFinding}," + csvSource);

                    Console.ForegroundColor = anyFinding == 1
                        ? ConsoleColor.DarkYellow : ConsoleColor.Green;
                    Console.WriteLine(anyFinding == 1
                        ? $"{fired.Count} findings  [{string.Join(" ", fired)}]"
                        : "clean");
                    Console.ResetColor();
                    ok++;
                }
                catch (Exception ex) when (
                    ex is BicepTooling.Lexer.LexerException or
                    BicepTooling.Parser.ParserException)
                {
                    writer.WriteLine($"{name}," + string.Join(",", Rules.Select(_ => "")) + ",,");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("skip");
                    Console.ResetColor();
                    skipped++;
                }
            }
        } // writer flushed and closed here — safe to read in PrintLabelStats

        Console.WriteLine();
        ConsoleUI.Success($"✓  CSV saved → {outputCsv}");
        Console.WriteLine($"   Labeled rows : {ok}");
        Console.WriteLine($"   Skipped rows : {skipped}  (blank labels — exclude from training)");
        Console.WriteLine();

        PrintLabelStats(outputCsv, ok);
    }

    private static void PrintLabelStats(string csvPath, int total)
    {
        if (total == 0) return;

        ConsoleUI.Section("LABEL DISTRIBUTION");
        Console.WriteLine($"  {"RULE",-8}  {"POSITIVES":>9}  {"NEGATIVES":>9}  {"% POS":>7}  IMBALANCE");
        Console.WriteLine($"  {new string('─', 55)}");

        var lines = File.ReadAllLines(csvPath).Skip(1)
                        .Where(l => !l.EndsWith(",,"))  // skip skipped rows
                        .ToArray();

        for (int c = 0; c < Rules.Length; c++)
        {
            int pos = lines.Count(l =>
            {
                var cols = l.Split(',');
                return cols.Length > c + 1 && cols[c + 1] == "1";
            });
            int neg  = total - pos;
            double pct = total == 0 ? 0 : (double)pos / total * 100;
            double ratio = pos == 0 ? double.MaxValue : (double)neg / pos;

            Console.ForegroundColor = pct < 5  ? ConsoleColor.Red
                                    : pct < 20 ? ConsoleColor.DarkYellow
                                    :             ConsoleColor.Green;

            Console.WriteLine(
                $"  {Rules[c],-8}  {pos,9}  {neg,9}  {pct,6:F1}%  " +
                (pos == 0 ? "no positives — rule may need more data"
                           : $"1:{ratio:F0} pos:neg ratio"));
            Console.ResetColor();
        }

        Console.WriteLine();
        ConsoleUI.Tip("  Rows with no labels (skipped files) are excluded from stats.");
        ConsoleUI.Tip("  For CodeBERT training: drop blank-label rows, oversample minority classes.");
        ConsoleUI.Tip("  Suggested split: 80% train / 10% val / 10% test, stratified per rule.");
    }
}
