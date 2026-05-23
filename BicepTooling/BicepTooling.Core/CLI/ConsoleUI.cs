namespace BicepTooling.CLI;

public static class ConsoleUI
{
    public static void Banner(params string[] lines)
    {
        int width = lines.Max(l => l.Length) + 4;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔" + new string('═', width) + "╗");
        foreach (var line in lines)
            Console.WriteLine($"║  {line.PadRight(width - 2)}║");
        Console.WriteLine("╚" + new string('═', width) + "╝");
        Console.ResetColor();
    }

    public static void Section(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        int dashes = Math.Max(0, 44 - title.Length);
        Console.WriteLine($"── {title} {new string('─', dashes)}");
        Console.ResetColor();
    }

    public static void PassOk(string name, string detail)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  ✓ ");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"{name,-14}");
        Console.ResetColor();
        Console.WriteLine(detail);
    }

    public static void PassWarn(string name, string detail)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write("  ⚠ ");
        Console.ResetColor();
        Console.Write($"{name,-14}");
        Console.WriteLine(detail);
    }

    public static void PassFail(string name, string detail)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  ✗ ");
        Console.ResetColor();
        Console.Write($"{name,-14}");
        Console.WriteLine(detail);
    }

    public static void Success(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(msg);
        Console.ResetColor();
    }

    public static void Error(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(msg);
        Console.ResetColor();
    }

    public static void Warning(string msg)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine(msg);
        Console.ResetColor();
    }

    public static void Info(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(msg);
        Console.ResetColor();
    }

    public static void Tip(string msg)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(msg);
        Console.ResetColor();
    }

    public static void Prompt(string text = "> ")
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(text);
        Console.ResetColor();
    }

    public static void ProgressBar(int completed, int total, int width = 20)
    {
        int filled = total == 0 ? 0 : (int)((double)completed / total * width);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  [" + new string('█', filled) + new string('░', width - filled) + "]");
        Console.ResetColor();
        Console.WriteLine($"  {completed}/{total}");
    }
}
