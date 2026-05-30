using BicepTooling.CLI;
using BicepTooling.Learning;

var command  = args.Length > 0 ? args[0].ToLower() : "";
var argument = args.Length > 1 ? args[1] : "";

switch (command)
{
    case "learn":
    {
        int n = int.TryParse(argument, out int ln) ? ln : 0;
        new LessonRunner().Run(n);
        break;
    }

    case "lesson":
    {
        int n = int.TryParse(argument, out int ln) ? ln : 1;
        new LessonRunner().Run(n);
        break;
    }

    case "analyze":
        new PipelineRunner().Analyze(argument is "" ? "Samples/hello.bicep" : argument);
        break;

    case "check":
        new PipelineRunner().Check(argument is "" ? "Samples/hello.bicep" : argument);
        break;

    case "explain":
        new PipelineRunner().Explain(argument is "" ? "Samples/hello.bicep" : argument);
        break;

    case "eval":
        new PipelineRunner().Evaluate(
            argument is "" ? "Samples/eval" : argument);
        break;

    case "export":
    {
        var dir = argument is "" ? "Samples/ml_data" : argument;
        var csv = args.Length > 2 ? args[2] : Path.Combine(dir, "labels.csv");
        new LabelExporter().Export(dir, csv);
        break;
    }

    case "download":
        await Downloader.FetchAsync(argument is "" ? "Samples/eval" : argument);
        break;

    case "help":
        ShowHelp();
        break;

    default:
        ShowMenu();
        break;
}

static void ShowMenu()
{
    ConsoleUI.Banner(
        "BicepTooling",
        "Azure Bicep analyzer, linter, and learning companion");

    Console.WriteLine();
    Console.WriteLine("  LEARN");
    ConsoleUI.Tip("    dotnet run -- learn           Resume from your last lesson");
    ConsoleUI.Tip("    dotnet run -- learn 3         Jump to lesson 3");
    Console.WriteLine();
    Console.WriteLine("  ANALYZE");
    ConsoleUI.Tip("    dotnet run -- analyze <file>  Full pipeline: lex → parse → check → ARM");
    ConsoleUI.Tip("    dotnet run -- check   <file>  Type-check and security scan only");
    ConsoleUI.Tip("    dotnet run -- explain <file>  Plain-English explanation of every line");
    Console.WriteLine();
    Console.WriteLine("  OTHER");
    ConsoleUI.Tip("    dotnet run -- help            Show this menu");
    Console.WriteLine();

    new ProgressTracker().PrintSummary();
}

static void ShowHelp()
{
    ShowMenu();
    Console.WriteLine();
    Console.WriteLine("  LESSONS");
    string[] titles =
    [
        "Parameters (param)",
        "Variables (var)",
        "Your first resource declaration",
        "Resource properties and body blocks",
        "Outputs and dot property access",
        "String interpolation ${}",
        "Referencing other resources",
        "Modules and file splitting",
        "Conditionals (if) and loops (for)",
        "Security best practices",
    ];
    var tracker = new ProgressTracker();
    for (int i = 0; i < titles.Length; i++)
    {
        var done = tracker.IsCompleted(i + 1) ? "✓" : "·";
        Console.ForegroundColor = tracker.IsCompleted(i + 1)
            ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {done} {i + 1,2}. {titles[i]}");
        Console.ResetColor();
    }
    Console.WriteLine();
}
