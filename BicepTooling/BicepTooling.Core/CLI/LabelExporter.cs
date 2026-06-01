// ============================================================
// FILE: LabelExporter.cs
// WHAT: Runs SecurityLinter on every .bicep file in a directory
//       and exports a labels.csv for ML training.
//
// COLUMNS: filename, SEC001–SEC023 (23 binary label columns)
// ============================================================

using BicepLexer  = BicepTooling.Lexer.Lexer;
using BicepParser = BicepTooling.Parser.Parser;
using BicepTooling.Parser;
using BicepTooling.Semantic;

namespace BicepTooling.CLI;

public class LabelExporter
{
    private static readonly string[] Rules =
        ["SEC001","SEC002","SEC003","SEC004","SEC005",
         "SEC006","SEC007","SEC008","SEC009","SEC010",
         "SEC011","SEC012","SEC013","SEC014","SEC015",
         "SEC016","SEC017","SEC018","SEC019","SEC020",
         "SEC021","SEC022","SEC023"];

    public void Export(string directory, string csvPath)
    {
        var files = Directory.GetFiles(directory, "*.bicep",
            SearchOption.AllDirectories)
            .OrderBy(f => f)
            .ToArray();

        Console.WriteLine($"\n  Exporting labels for {files.Length} files → {csvPath}\n");

        var lines = new List<string>
        {
            "filename," + string.Join(",", Rules)
        };

        int done = 0;
        foreach (var file in files)
        {
            try
            {
                var src    = File.ReadAllText(file);
                var tokens = new BicepLexer(src).Tokenize();
                var ast    = new BicepParser(tokens).ParseCompilationUnit();
                var linter = new SecurityLinter();
                linter.Lint(ast);

                var fired = linter.Issues
                    .Select(i => i.Code)
                    .ToHashSet();

                var labels = Rules.Select(r => fired.Contains(r) ? "1" : "0");
                var name   = Path.GetFileName(file);
                lines.Add($"{name},{string.Join(",", labels)}");
                done++;
            }
            catch
            {
                // skip unparseable files — don't include in CSV
            }
        }

        File.WriteAllLines(csvPath, lines);

        Console.WriteLine($"  ✓ Exported {done} rows × {Rules.Length} label columns");
        Console.WriteLine($"  ✓ Saved to: {csvPath}");
        Console.WriteLine();

        // Print column prevalence summary
        Console.WriteLine("  LABEL PREVALENCE");
        Console.WriteLine("  ─────────────────────────────────────────");

        var dataLines = lines.Skip(1).ToList();
        for (int col = 0; col < Rules.Length; col++)
        {
            int positives = dataLines.Count(l =>
            {
                var parts = l.Split(',');
                return parts.Length > col + 1 && parts[col + 1] == "1";
            });
            double pct = done > 0 ? (positives * 100.0 / done) : 0;
            var bar = new string('█', (int)(pct / 5)) +
                      new string('░', 20 - (int)(pct / 5));
            Console.WriteLine(
                $"  {Rules[col]}  {positives,4} / {done}  ({pct,5:F1}%)  {bar}");
        }
        Console.WriteLine();
    }
}
