using BicepTooling.Parser;
using BicepTooling.CLI;
using BicepLexer  = BicepTooling.Lexer.Lexer;
using BicepParser = BicepTooling.Parser.Parser;

namespace BicepTooling.Learning;

public class LessonRunner
{
    private readonly List<Lesson>   _lessons;
    private readonly ProgressTracker _tracker = new();

    public LessonRunner()
    {
        _lessons =
        [
            new Lesson(
                number:    1,
                title:     "Parameters (param)",
                concept:   """
                    A parameter lets someone customize your template when they deploy it.
                    Think of it like a function argument in C# or Python.

                    Syntax:  param <name> <type> = <default>
                    Types:   string, int, bool, object, array
                    """,
                exercise:  "Write a param called 'location' of type 'string' with a default of 'eastus'",
                solution:  "param location string = 'eastus'",
                hint:      "Start with 'param', then the name, type, and optionally '= value'.",
                validate:  (ast) =>
                {
                    var p = ast.Statements.OfType<ParameterDeclarationSyntax>().FirstOrDefault();
                    if (p == null) return "No param declaration found. Start your line with 'param'.";
                    if (p.Name != "location") return $"Expected name 'location', got '{p.Name}'.";
                    if (p.Type != "string")   return $"Expected type 'string', got '{p.Type}'.";
                    return null;
                },
                breakdown: """
                    param     = declares an input parameter
                    location  = the name (used later in the file)
                    string    = the type (text values only)
                    = 'eastus' = the default (optional but helpful)
                    """
            ),

            new Lesson(
                number:    2,
                title:     "Variables (var)",
                concept:   """
                    A variable stores a computed or reusable value inside your template.
                    Unlike params, variables cannot be set by the deployer — they are internal.

                    Syntax:  var <name> = <value>
                    """,
                exercise:  "Write a var called 'storageSku' with the value 'Standard_LRS'",
                solution:  "var storageSku = 'Standard_LRS'",
                hint:      "Start with 'var', then the name, '=', and a string value in single quotes.",
                validate:  (ast) =>
                {
                    var v = ast.Statements.OfType<VariableDeclarationSyntax>().FirstOrDefault();
                    if (v == null) return "No var declaration found. Start your line with 'var'.";
                    if (v.Name != "storageSku") return $"Expected name 'storageSku', got '{v.Name}'.";
                    if (v.Value is not StringLiteralExpressionSyntax s || s.Value != "'Standard_LRS'")
                        return "Expected value 'Standard_LRS' (in single quotes).";
                    return null;
                },
                breakdown: """
                    var         = declares an internal variable
                    storageSku  = the name
                    =           = assignment
                    'Standard_LRS' = the value (a string literal)
                    """
            ),

            new Lesson(
                number:    3,
                title:     "Your first resource declaration",
                concept:   """
                    A resource declaration tells Azure to create a cloud resource.
                    The type string includes the API version after '@'.

                    Syntax:  resource <name> '<type>@<apiVersion>' = { ... }
                    """,
                exercise:  "Declare a resource named 'myStorage' of type 'Microsoft.Storage/storageAccounts@2023-01-01' with an empty body {}",
                solution:  "resource myStorage 'Microsoft.Storage/storageAccounts@2023-01-01' = {}",
                hint:      "Start with 'resource', then a name, the type string in quotes, '=', and '{}'.",
                validate:  (ast) =>
                {
                    var r = ast.Statements.OfType<ResourceDeclarationSyntax>().FirstOrDefault();
                    if (r == null) return "No resource declaration found. Start with 'resource'.";
                    if (r.Name != "myStorage") return $"Expected name 'myStorage', got '{r.Name}'.";
                    if (r.Type is not StringLiteralExpressionSyntax t || !t.Value.Contains("storageAccounts"))
                        return "Expected type 'Microsoft.Storage/storageAccounts@2023-01-01'.";
                    if (!t.Value.Contains('@'))
                        return "Missing API version — add '@2023-01-01' to the type string.";
                    return null;
                },
                breakdown: """
                    resource        = declares a cloud resource
                    myStorage       = the symbolic name (used to reference it later)
                    'Microsoft.Storage/storageAccounts@2023-01-01'
                                    = the Azure resource type @ API version
                    = {}            = the resource body (properties go inside)
                    """
            ),

            new Lesson(
                number:    4,
                title:     "Resource properties and body blocks",
                concept:   """
                    The body block '{ }' is where you set the resource's properties.
                    Properties are key: value pairs, one per line.

                    Syntax:  resource <name> '<type>' = {
                               name: 'value'
                               location: location
                             }
                    """,
                exercise:  "Write a resource 'store' of type 'Microsoft.Storage/storageAccounts@2023-01-01' with properties: name: 'mystore' and location: 'eastus'",
                solution:  "resource store 'Microsoft.Storage/storageAccounts@2023-01-01' = { name: 'mystore' location: 'eastus' }",
                hint:      "Put 'name: ...' and 'location: ...' inside the { } body.",
                validate:  (ast) =>
                {
                    var r = ast.Statements.OfType<ResourceDeclarationSyntax>().FirstOrDefault();
                    if (r == null) return "No resource declaration found.";
                    if (r.Body is not ObjectExpressionSyntax body)
                        return "Resource body must be an object '{ }' with properties.";
                    if (!body.Properties.Any(p => p.Name == "name"))
                        return "Missing 'name' property in the resource body.";
                    if (!body.Properties.Any(p => p.Name == "location"))
                        return "Missing 'location' property in the resource body.";
                    return null;
                },
                breakdown: """
                    { }        = the resource body block
                    name:      = a property key
                    'mystore'  = a string value
                    location:  = another property key
                    'eastus'   = its value
                    """
            ),

            new Lesson(
                number:    5,
                title:     "Outputs and dot property access",
                concept:   """
                    Outputs return values from your deployment — like a resource ID or endpoint.
                    You access a resource's properties using dot notation: resource.property

                    Syntax:  output <name> <type> = <expression>
                    Example: output storageId string = myStorage.id
                    """,
                exercise:  "Write an output named 'storageId' of type 'string' whose value is 'myStorage.id'",
                solution:  "output storageId string = myStorage.id",
                hint:      "Start with 'output', then name, type, '=', and the dot-access expression.",
                validate:  (ast) =>
                {
                    var o = ast.Statements.OfType<OutputDeclarationSyntax>().FirstOrDefault();
                    if (o == null) return "No output declaration found. Start with 'output'.";
                    if (o.Name != "storageId") return $"Expected name 'storageId', got '{o.Name}'.";
                    if (o.Type != "string")    return $"Expected type 'string', got '{o.Type}'.";
                    if (o.Value is not MemberAccessExpressionSyntax m)
                        return "Expected dot-access expression like 'myStorage.id'.";
                    if (m.Member != "id")
                        return $"Expected property 'id', got '{m.Member}'.";
                    return null;
                },
                breakdown: """
                    output     = declares a return value
                    storageId  = the output name
                    string     = the output type
                    =          = assignment
                    myStorage  = the resource symbolic name
                    .id        = the property to return
                    """
            ),

            new Lesson(
                number:    6,
                title:     "String interpolation ${}",
                concept:   """
                    String interpolation lets you embed expressions inside strings.
                    In Bicep you use single quotes with ${ } for interpolation.

                    Syntax:  'prefix-${variableOrParam}-suffix'
                    Example: var fullName = 'storage-${storageSku}-account'
                    """,
                exercise:  "Write a var named 'fullName' whose value embeds the word 'storage' followed by a dash and 'prod'",
                solution:  "var fullName = 'storage-prod'",
                hint:      "Interpolation: 'prefix-${expr}'. For now a plain string like 'storage-prod' also works.",
                validate:  (ast) =>
                {
                    var v = ast.Statements.OfType<VariableDeclarationSyntax>().FirstOrDefault();
                    if (v == null) return "No var declaration found.";
                    if (v.Name != "fullName") return $"Expected name 'fullName', got '{v.Name}'.";
                    if (v.Value is not StringLiteralExpressionSyntax s || !s.Value.Contains("storage"))
                        return "Value should be a string containing 'storage'.";
                    return null;
                },
                breakdown: """
                    var fullName   = internal variable named 'fullName'
                    'storage-prod' = string value
                    ${ }           = interpolation syntax (embeds any expression)
                    """
            ),

            new Lesson(
                number:    7,
                title:     "Referencing other resources",
                concept:   """
                    Once you declare a resource, you can reference its runtime properties
                    anywhere in the same file using dot notation.

                    Common properties:  .id  .name  .properties.primaryEndpoints

                    Example:  output endpoint string = storageAccount.properties.primaryEndpoints.blob
                    """,
                exercise:  "Write an output named 'blobEndpoint' of type 'string' that reads 'storageAccount.id'",
                solution:  "output blobEndpoint string = storageAccount.id",
                hint:      "Use dot notation: resourceName.propertyName",
                validate:  (ast) =>
                {
                    var o = ast.Statements.OfType<OutputDeclarationSyntax>().FirstOrDefault();
                    if (o == null) return "No output declaration found.";
                    if (o.Name != "blobEndpoint") return $"Expected 'blobEndpoint', got '{o.Name}'.";
                    if (o.Value is not MemberAccessExpressionSyntax)
                        return "Value must use dot notation, e.g. storageAccount.id";
                    return null;
                },
                breakdown: """
                    output blobEndpoint = the output name
                    string              = the type
                    storageAccount      = symbolic name of the resource
                    .id                 = runtime property resolved by Azure after deployment
                    """
            ),

            new Lesson(
                number:    8,
                title:     "Modules and file splitting",
                concept:   """
                    Modules let you split large templates into reusable files.
                    Each module is a separate .bicep file you call like a function.

                    Syntax:  module <name> '<path>' = {
                               params: { key: value }
                             }
                    """,
                exercise:  "Declare a var named 'modulePath' with the value './network.bicep'",
                solution:  "var modulePath = './network.bicep'",
                hint:      "Modules use file paths in single quotes. For now, store the path in a var.",
                validate:  (ast) =>
                {
                    var v = ast.Statements.OfType<VariableDeclarationSyntax>().FirstOrDefault();
                    if (v == null) return "No var found. Write: var modulePath = './network.bicep'";
                    if (v.Name != "modulePath") return $"Expected 'modulePath', got '{v.Name}'.";
                    if (v.Value is not StringLiteralExpressionSyntax s || !s.Value.Contains(".bicep"))
                        return "Value should be a file path ending in '.bicep'.";
                    return null;
                },
                breakdown: """
                    module      = (full syntax) declares a module call
                    './file.bicep' = relative path to the module file
                    params: {}  = values passed to the module's parameters
                    NOTE: Module parsing is a future lesson — today we store the path concept.
                    """
            ),

            new Lesson(
                number:    9,
                title:     "Conditionals (if) and loops (for)",
                concept:   """
                    Bicep supports conditional deployment and loops.

                    Conditional:  resource x '...' = if (condition) { ... }
                    Loop:         [for item in collection: item]

                    These compile to ARM's 'condition' and 'copy' properties.
                    """,
                exercise:  "Write a param named 'deployStorage' of type 'bool' with default 'true'",
                solution:  "param deployStorage bool = true",
                hint:      "Bool params hold 'true' or 'false'. Syntax: param name bool = true",
                validate:  (ast) =>
                {
                    var p = ast.Statements.OfType<ParameterDeclarationSyntax>().FirstOrDefault();
                    if (p == null) return "No param found.";
                    if (p.Name != "deployStorage") return $"Expected 'deployStorage', got '{p.Name}'.";
                    if (p.Type != "bool") return $"Expected type 'bool', got '{p.Type}'.";
                    if (p.Value is not BooleanLiteralExpressionSyntax b || !b.Value)
                        return "Expected default value 'true'.";
                    return null;
                },
                breakdown: """
                    param deployStorage = input parameter
                    bool                = boolean type (true/false)
                    = true              = default value
                    In use: resource x '...' = if (deployStorage) { ... }
                    """
            ),

            new Lesson(
                number:    10,
                title:     "Security best practices",
                concept:   """
                    The top Azure Bicep security rules:

                    1. Use @secure() for any param that holds a password, key, or token.
                    2. Always set supportsHttpsTrafficOnly: true on storage accounts.
                    3. Set minimumTlsVersion: 'TLS1_2' to block old TLS.
                    4. Never output secrets — use Key Vault references instead.
                    5. Always pin an API version so deployments are reproducible.
                    """,
                exercise:  "Write a param named 'adminPassword' of type 'string' (the @secure() decorator is not yet parsed — just write the param)",
                solution:  "param adminPassword string",
                hint:      "Write: param adminPassword string   (no default — secrets should not have defaults)",
                validate:  (ast) =>
                {
                    var p = ast.Statements.OfType<ParameterDeclarationSyntax>().FirstOrDefault();
                    if (p == null) return "No param found.";
                    if (p.Name != "adminPassword") return $"Expected 'adminPassword', got '{p.Name}'.";
                    if (p.Type != "string") return $"Expected type 'string', got '{p.Type}'.";
                    return null;
                },
                breakdown: """
                    param adminPassword string = a required string parameter
                    No default                 = secrets must never have hardcoded defaults
                    @secure()                  = (future) hides the value from deployment logs
                    Key Vault ref              = (best) fetch at deploy time with no exposure
                    """
            ),
        ];
    }

    public void Run(int startLesson = 0)
    {
        PrintWelcome();

        // 0 means "resume" — start from next unfinished lesson
        if (startLesson == 0)
            startLesson = Enumerable.Range(1, 10).FirstOrDefault(n => !_tracker.IsCompleted(n), 1);

        int current = Math.Clamp(startLesson, 1, _lessons.Count);

        while (true)
        {
            var lesson = _lessons[current - 1];
            RunLesson(lesson);

            Console.WriteLine();
            ConsoleUI.Prompt("Press Enter for the next lesson, or type a number (1-10): ");
            var input = Console.ReadLine()?.Trim() ?? "";

            if (int.TryParse(input, out int n) && n >= 1 && n <= _lessons.Count)
                current = n;
            else if (current < _lessons.Count)
                current++;
            else
            {
                PrintCompletion();
                break;
            }
        }
    }

    private void RunLesson(Lesson lesson)
    {
        var already = _tracker.IsCompleted(lesson.Number);

        Console.WriteLine();
        ConsoleUI.Section($"LESSON {lesson.Number}/10: {lesson.Title}" +
                          (already ? "  ✓ completed" : ""));
        Console.WriteLine();
        Console.WriteLine(lesson.Concept.Trim());
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"EXERCISE: {lesson.Exercise}");
        Console.ResetColor();
        Console.WriteLine();
        ConsoleUI.Tip("  Commands: hint  |  solution  |  skip");
        Console.WriteLine();

        int attempts = 0;

        while (true)
        {
            ConsoleUI.Prompt();
            var answer = Console.ReadLine()?.Trim() ?? "";

            switch (answer.ToLower())
            {
                case "hint":
                    ConsoleUI.Info($"HINT: {lesson.Hint}");
                    continue;

                case "solution":
                    ConsoleUI.Tip($"SOLUTION: {lesson.Solution}");
                    continue;

                case "skip":
                    ConsoleUI.Tip("Skipping — you can return any time.");
                    return;

                case "":
                    continue;
            }

            attempts++;
            var error = TryValidate(answer, lesson);

            if (error == null)
            {
                _tracker.MarkCompleted(lesson.Number, attempts);
                ConsoleUI.Success($"\nCORRECT! ({attempts} attempt{(attempts == 1 ? "" : "s")})\n");

                Console.WriteLine("Let's break down what you wrote:");
                foreach (var line in lesson.Breakdown.Trim().Split('\n'))
                    ConsoleUI.Tip($"  {line.Trim()}");

                Console.WriteLine();
                _tracker.PrintSummary();

                if (lesson.Number < 10)
                    ConsoleUI.Tip($"\nPress Enter for lesson {lesson.Number + 1}, or type a number (1-10).");
                break;
            }
            else
            {
                ConsoleUI.Error($"Not quite: {error}");
                Console.WriteLine("Try again, or type 'hint' / 'solution'.");
            }
        }
    }

    private static string? TryValidate(string input, Lesson lesson)
    {
        try
        {
            var tokens = new BicepLexer(input).Tokenize();
            var ast    = new BicepParser(tokens).ParseCompilationUnit();
            return lesson.Validate(ast);
        }
        catch (Exception ex)
        {
            return $"Parse error: {ex.Message}";
        }
    }

    private void PrintWelcome()
    {
        ConsoleUI.Banner(
            "BicepTooling — Learning Mode",
            "Your interactive guide to Azure Bicep");
        Console.WriteLine();
        Console.WriteLine("  hint       Show a hint for the current exercise");
        Console.WriteLine("  solution   Reveal the answer");
        Console.WriteLine("  skip       Skip this lesson and come back later");
        Console.WriteLine("  1-10       Jump to any lesson number");
        _tracker.PrintSummary();
    }

    private static void PrintCompletion()
    {
        Console.WriteLine();
        ConsoleUI.Banner(
            "Congratulations! All 10 lessons done.",
            "You now know the core of Azure Bicep.");
        Console.WriteLine();
        ConsoleUI.Tip("  dotnet run -- analyze Samples/hello.bicep");
        ConsoleUI.Tip("  dotnet run -- check   Samples/hello.bicep");
    }
}

public class Lesson
{
    public int    Number    { get; }
    public string Title     { get; }
    public string Concept   { get; }
    public string Exercise  { get; }
    public string Solution  { get; }
    public string Hint      { get; }
    public string Breakdown { get; }
    public Func<CompilationUnitSyntax, string?> Validate { get; }

    public Lesson(int number, string title, string concept, string exercise,
                  string solution, string hint, string breakdown,
                  Func<CompilationUnitSyntax, string?> validate)
    {
        Number    = number;
        Title     = title;
        Concept   = concept;
        Exercise  = exercise;
        Solution  = solution;
        Hint      = hint;
        Breakdown = breakdown;
        Validate  = validate;
    }
}
