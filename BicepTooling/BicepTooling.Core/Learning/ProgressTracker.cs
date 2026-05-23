using System.Text.Json;
using BicepTooling.CLI;

namespace BicepTooling.Learning;

public class ProgressTracker
{
    private static readonly string SavePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".bicep-learn", "progress.json");

    private readonly ProgressData _data;

    public ProgressTracker() => _data = Load();

    public int  CompletedCount => _data.Completed.Count;
    public int  LastLesson     => _data.LastLesson;
    public bool IsCompleted(int n) => _data.Completed.Contains(n);
    public int  Attempts(int n)    => _data.Attempts.GetValueOrDefault(n, 0);

    public void MarkCompleted(int lesson, int attempts)
    {
        _data.Completed.Add(lesson);
        _data.Attempts[lesson] = attempts;
        _data.LastLesson = lesson;
        Save();
    }

    public void PrintSummary()
    {
        Console.WriteLine();
        Console.Write("  Progress: ");
        ConsoleUI.Success($"{_data.Completed.Count}/10 lessons complete");

        Console.Write("  ");
        for (int n = 1; n <= 10; n++)
        {
            if (_data.Completed.Contains(n))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($" ✓{n}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($" ·{n}");
            }
        }
        Console.ResetColor();
        Console.WriteLine();
        ConsoleUI.ProgressBar(_data.Completed.Count, 10);
    }

    private static ProgressData Load()
    {
        try
        {
            if (File.Exists(SavePath))
                return JsonSerializer.Deserialize<ProgressData>(File.ReadAllText(SavePath))
                       ?? new ProgressData();
        }
        catch { }
        return new ProgressData();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
            File.WriteAllText(SavePath,
                JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private sealed class ProgressData
    {
        public HashSet<int>          Completed { get; set; } = [];
        public Dictionary<int, int>  Attempts  { get; set; } = [];
        public int                   LastLesson { get; set; } = 0;
    }
}
