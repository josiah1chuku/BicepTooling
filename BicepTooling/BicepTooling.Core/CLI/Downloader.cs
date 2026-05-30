using System.Net.Http.Headers;
using System.Text.Json;

namespace BicepTooling.CLI;

public static class Downloader
{
    // azure-quickstart-templates has hundreds of real-world .bicep files.
    // Its git tree is public — no auth token needed.
    private const string Repo      = "Azure/azure-quickstart-templates";
    private const string Branch    = "master";
    private const string RawBase   = $"https://raw.githubusercontent.com/{Repo}/{Branch}/";
    private const string TreeUrl   =
        $"https://api.github.com/repos/{Repo}/git/trees/{Branch}?recursive=1";

    public static async Task FetchAsync(string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        var token = GetGhToken() ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        ConsoleUI.Banner("Downloading real Bicep files from GitHub");
        Console.WriteLine();
        ConsoleUI.Tip($"  Source: {Repo}  (Azure Quickstart Templates)");
        ConsoleUI.Tip(token is null
            ? "  Auth:   none  (unauthenticated — 60 API req/hr)"
            : "  Auth:   token found  (5000 API req/hr)");
        Console.WriteLine();

        using var http = BuildClient(token);

        // ── Step 1: get the full repo file tree (1 API call, no auth needed) ────
        Console.Write("  Fetching repo file list... ");
        string treeJson;
        try
        {
            treeJson = await http.GetStringAsync(TreeUrl);
        }
        catch (Exception ex)
        {
            ConsoleUI.Error($"\nFailed: {ex.Message}");
            return;
        }

        var doc      = JsonDocument.Parse(treeJson);
        var root     = doc.RootElement;
        bool truncated = root.TryGetProperty("truncated", out var t) && t.GetBoolean();

        var allPaths = root.GetProperty("tree")
            .EnumerateArray()
            .Where(n => n.GetProperty("type").GetString() == "blob")
            .Select(n => n.GetProperty("path").GetString()!)
            .Where(p => p.EndsWith(".bicep", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Prefer main.bicep files (top-level entry points, most representative)
        var mainFiles  = allPaths.Where(p => Path.GetFileName(p) == "main.bicep").ToList();
        var otherFiles = allPaths.Where(p => Path.GetFileName(p) != "main.bicep").ToList();

        var selected = mainFiles.Concat(otherFiles).Take(500).ToList();

        ConsoleUI.PassOk("Tree",
            $"{allPaths.Count} .bicep files found  →  downloading {selected.Count}" +
            (truncated ? "  (tree was truncated — partial list)" : ""));
        Console.WriteLine();

        // ── Step 2: download each via raw.githubusercontent.com ──────────────────
        // raw.githubusercontent.com has no rate limit for unauthenticated reads.
        int ok = 0, fail = 0;

        for (int i = 0; i < selected.Count; i++)
        {
            var path     = selected[i];
            var rawUrl   = RawBase + path;
            var safeName = path.Replace('/', '_');
            var dest     = Path.Combine(outputDir, safeName);

            Console.Write($"  [{i + 1,2}/{selected.Count}] {safeName,-60}");

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
                Console.WriteLine($"skip  ({ex.Message[..Math.Min(50, ex.Message.Length)]})");
                Console.ResetColor();
                fail++;
            }
        }

        Console.WriteLine();
        ConsoleUI.Success($"✓  Downloaded {ok} files → {outputDir}");
        if (fail > 0) ConsoleUI.Tip($"   {fail} files skipped.");
        Console.WriteLine();
        ConsoleUI.Tip("  Now run:  dotnet run -- eval");
    }

    // Try to get the token from the gh CLI silently.
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
