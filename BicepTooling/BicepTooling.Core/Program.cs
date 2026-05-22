using BicepTooling.Lexer;
using BicepTooling.Parser;
using BicepTooling.Semantic;
using BicepTooling.CodeGen;

string source = File.ReadAllText("Samples/hello.bicep");

// Pass 1
var tokens = new Lexer(source).Tokenize();
Console.WriteLine($"Pass 1: {tokens.Count} tokens");

// Pass 2
var ast = new Parser(tokens).ParseCompilationUnit();
Console.WriteLine($"Pass 2: {ast.Statements.Count} declarations");

// Pass 3
var symbols = new SymbolResolver().Resolve(ast);
Console.WriteLine($"Pass 3: {symbols.All.Count()} symbols");

// Pass 4
var checker = new TypeChecker(symbols);
checker.Check(ast);
Console.WriteLine($"Pass 4: {checker.Errors.Count} errors\n");

// Pass 5
var armJson = new ArmGenerator().Generate(ast, symbols);
Console.WriteLine("Pass 5: ARM JSON generated\n");
Console.WriteLine("=== ARM TEMPLATE OUTPUT ===\n");
Console.WriteLine(armJson);

// Save to file
File.WriteAllText("Samples/azuredeploy.json", armJson);
Console.WriteLine(" Saved to Samples/azuredeploy.json");
