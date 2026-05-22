using BicepTooling.Lexer;
using BicepTooling.Parser;

// ── PASS 1: Lex ──────────────────────────────────────────────
string source = File.ReadAllText("Samples/hello.bicep");
var lexer     = new Lexer(source);
var tokens    = lexer.Tokenize();
Console.WriteLine($"✅ Pass 1 complete: {tokens.Count} tokens found\n");

// ── PASS 2: Parse ────────────────────────────────────────────
var parser = new Parser(tokens);
var ast    = parser.Parse();
Console.WriteLine($"✅ Pass 2 complete: {ast.Declarations.Count} declarations found\n");

// ── PRINT AST ────────────────────────────────────────────────
Console.WriteLine("=== AST OUTPUT ===\n");
foreach (var decl in ast.Declarations)
{
    Console.ForegroundColor = decl switch
    {
        ParamDeclarationSyntax    => ConsoleColor.Cyan,
        VarDeclarationSyntax      => ConsoleColor.Green,
        ResourceDeclarationSyntax => ConsoleColor.Yellow,
        OutputDeclarationSyntax   => ConsoleColor.Magenta,
        _                         => ConsoleColor.White
    };
    Console.WriteLine($"  {decl.GetType().Name}");
    Console.WriteLine($"    → {decl}");
}
Console.ResetColor();