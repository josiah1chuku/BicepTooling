using System.Net.Http.Headers;
using System.Text.Json;

namespace BicepTooling.CLI;

public static class MultiSourceDownloader
{
    // Three repos that are entirely separate from the existing source
    // (Azure/azure-quickstart-templates).  Each contributes a different
    // style of Bicep: AVM reusable modules, landing-zone infrastructure,
    // and official documentation examples.
    private static readonly (string Repo, string Branch, int Quota, string Prefix, string Label)[] Sources =
    [
        ("Azure/bicep-registry-modules",          "main",   2000, "avm",  "Azure Verified Modules"),
        ("Azure/ALZ-Bicep",                       "main",    500, "alz",  "Azure Landing Zones"),
        ("Azure/azure-docs-bicep-samples",        "main",    300, "docs", "Bicep Docs Samples"),
        ("Azure/ResourceModules",                 "main",    400, "carml","CARML Resource Modules"),
        ("Azure/azure-monitor-baseline-alerts",   "main",    200, "amba", "Azure Monitor Baseline"),
        ("microsoft/AzureTRE",                   "main",    200, "tre",  "Azure TRE"),
        ("Azure/azure-openai-landing-zone-reference-architecture", "main", 100, "aoai", "Azure OpenAI LZ"),
        ("Azure-Samples/aks-store-demo",          "main",    100, "aksdemo","AKS Store Demo"),
    ];

    public static async Task FetchAsync(string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        var token = GetGhToken() ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        ConsoleUI.Banner("Downloading extended Bicep dataset from GitHub");
        Console.WriteLine();
        ConsoleUI.Tip("  Sources:");
        foreach (var (repo, _, quota, _, label) in Sources)
            ConsoleUI.Tip($"    {label,-30}  ({repo})  target: {quota} files");
        ConsoleUI.Tip(token is null
            ? "\n  Auth: none — unauthenticated (60 API req/hr). Set GITHUB_TOKEN to raise limit."
            : "\n  Auth: token found (5000 API req/hr).");
        Console.WriteLine();

        using var http = BuildClient(token);

        int grandTotal = 0;

        foreach (var (repo, branch, quota, prefix, label) in Sources)
        {
            var downloaded = await FetchFromRepo(http, repo, branch, quota, prefix, outputDir);
            grandTotal += downloaded;
            Console.WriteLine();
        }

        ConsoleUI.Success($"✓  Total downloaded: {grandTotal} files → {outputDir}");
        Console.WriteLine();
        ConsoleUI.Tip("  Now run:  dotnet run -- export " + outputDir);
    }

    private static async Task<int> FetchFromRepo(
        HttpClient http, string repo, string branch,
        int quota, string prefix, string outputDir)
    {
        var treeUrl  = $"https://api.github.com/repos/{repo}/git/trees/{branch}?recursive=1";
        var rawBase  = $"https://raw.githubusercontent.com/{repo}/{branch}/";

        ConsoleUI.Section($"{repo}  (target: {quota} files)");

        string treeJson;
        try
        {
            Console.Write("  Fetching file list... ");
            treeJson = await http.GetStringAsync(treeUrl);
        }
        catch (Exception ex)
        {
            ConsoleUI.Error($"Failed: {ex.Message}");
            return 0;
        }

        var doc  = JsonDocument.Parse(treeJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("tree", out var tree))
        {
            ConsoleUI.Error("Unexpected API response — no 'tree' field.");
            return 0;
        }

        var allPaths = tree.EnumerateArray()
            .Where(n => n.GetProperty("type").GetString() == "blob")
            .Select(n => n.GetProperty("path").GetString()!)
            .Where(p => p.EndsWith(".bicep", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Prefer main.bicep / main entry-points for diversity
        var mains  = allPaths.Where(p => Path.GetFileName(p) == "main.bicep").ToList();
        var others = allPaths.Where(p => Path.GetFileName(p) != "main.bicep").ToList();
        var selected = mains.Concat(others).Take(quota).ToList();

        bool truncated = root.TryGetProperty("truncated", out var t) && t.GetBoolean();
        ConsoleUI.PassOk("Tree",
            $"{allPaths.Count} .bicep files found  →  downloading {selected.Count}" +
            (truncated ? "  ⚠ tree truncated" : ""));

        int ok = 0, fail = 0;
        for (int i = 0; i < selected.Count; i++)
        {
            var path     = selected[i];
            var rawUrl   = rawBase + path;
            var safeName = $"{prefix}__{path.Replace('/', '_')}";
            var dest     = Path.Combine(outputDir, safeName);

            // Skip if already downloaded in a previous run
            if (File.Exists(dest)) { ok++; continue; }

            Console.Write($"  [{i + 1,4}/{selected.Count}] {safeName[..(Math.Min(safeName.Length, 60))],-60}");

            try
            {
                var content = await http.GetStringAsync(rawUrl);
                await File.WriteAllTextAsync(dest, content);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓");
                Console.ResetColor();
                ok++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"skip ({ex.Message[..Math.Min(40, ex.Message.Length)]})");
                Console.ResetColor();
                fail++;
            }

            // Small delay to be a polite API client
            if (i % 20 == 19) await Task.Delay(500);
        }

        ConsoleUI.PassOk("Done", $"{ok} downloaded, {fail} skipped");
        return ok;
    }

    private static string? GetGhToken()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("gh", "auth token")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            var token = proc.StandardOutput.ReadLine()?.Trim();
            proc.WaitForExit();
            return string.IsNullOrEmpty(token) ? null : token;
        }
        catch { return null; }
    }

    private static HttpClient BuildClient(string? token)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("BicepTooling", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (token is not null)
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
