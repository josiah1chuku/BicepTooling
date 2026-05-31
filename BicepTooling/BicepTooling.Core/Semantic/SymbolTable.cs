// ============================================================
// FILE: SymbolTable.cs
// WHAT: Pass 3 — walks the AST and registers every name.
//
// THINK OF IT LIKE YOUR HALIX ASSEMBLER:
//   In your assembler Pass 2, you walked all instructions
//   and recorded every label and its address.
//   Here we walk all AST nodes and record every name,
//   its kind (param/var/resource/output) and its type.
//
// WHY THIS MATTERS:
//   Later passes need to know: "is storageSku defined?"
//   "is storageAccount a resource or a variable?"
//   The symbol table answers these questions instantly.
// ============================================================

using BicepTooling.Parser;

namespace BicepTooling.Semantic;

// ── SYMBOL KIND ──────────────────────────────────────────────
// What kind of declaration produced this symbol?
public enum SymbolKind
{
    Parameter,  // from: param location string = 'eastus'
    Variable,   // from: var storageSku = 'Standard_LRS'
    Resource,   // from: resource storageAccount '...' = {}
    Output,     // from: output storageId string = ...
    Module      // from: module network './network.bicep' = {}
}

// ── SYMBOL ───────────────────────────────────────────────────
// One entry in the symbol table.
// Like a row in your assembler's label table.
public class Symbol
{
    public string     Name       { get; }  // the identifier name
    public SymbolKind Kind       { get; }  // param / var / resource / output
    public string     Type       { get; }  // string / int / bool / resource type
    public bool       HasDefault { get; }  // only for params

    public Symbol(string name, SymbolKind kind, string type, bool hasDefault = false)
    {
        Name       = name;
        Kind       = kind;
        Type       = type;
        HasDefault = hasDefault;
    }

    public override string ToString() =>
        $"{Kind,-12} {Name,-25} {Type}";
}

// ── SYMBOL TABLE ─────────────────────────────────────────────
// Stores all symbols found in a Bicep file.
public class SymbolTable
{
    // Dictionary for O(1) lookup by name — same as your assembler
    private readonly Dictionary<string, Symbol> _symbols = new();

    // All errors found during symbol resolution
    public List<string> Errors { get; } = new();

    // ── REGISTER ─────────────────────────────────────────────

    // Add a new symbol. Reports error if name already exists.
    public void Register(Symbol symbol)
    {
        if (_symbols.ContainsKey(symbol.Name))
        {
            Errors.Add($"Duplicate symbol: '{symbol.Name}' is already defined.");
            return;
        }
        _symbols[symbol.Name] = symbol;
    }

    // ── LOOKUP ───────────────────────────────────────────────

    // Find a symbol by name. Returns null if not found.
    public Symbol? Lookup(string name) =>
        _symbols.TryGetValue(name, out var symbol) ? symbol : null;

    // Check if a name exists in the table
    public bool Contains(string name) => _symbols.ContainsKey(name);

    // All symbols as a list (for printing)
    public IEnumerable<Symbol> All => _symbols.Values;
}

// ── SYMBOL RESOLVER ──────────────────────────────────────────
// Walks the AST and fills the SymbolTable.
// This is the actual Pass 3 logic.
public class SymbolResolver
{
    private readonly SymbolTable _table = new();

    // Entry point — resolve all symbols in the file
    public SymbolTable Resolve(CompilationUnitSyntax ast)
    {
        foreach (var stmt in ast.Statements)
        {
            if (stmt == null) continue;
            RegisterDeclaration(stmt);
        }
        return _table;
    }

    // ── REGISTRATION PASS ────────────────────────────────────
    // First pass: register every declared name
    private void RegisterDeclaration(StatementSyntax stmt)
    {
        switch (stmt)
        {
            // param location string = 'eastus'
            case ParameterDeclarationSyntax p:
                _table.Register(new Symbol(
                    name:       p.Name,
                    kind:       SymbolKind.Parameter,
                    type:       p.Type,
                    hasDefault: p.Value != null
                ));
                break;

            // var storageSku = 'Standard_LRS'
            case VariableDeclarationSyntax v:
                _table.Register(new Symbol(
                    name: v.Name,
                    kind: SymbolKind.Variable,
                    type: InferType(v.Value)   // infer type from value
                ));
                break;

            // resource storageAccount '...' = {}
            case ResourceDeclarationSyntax r:
                _table.Register(new Symbol(
                    name: r.Name,
                    kind: SymbolKind.Resource,
                    type: r.Type is StringLiteralExpressionSyntax s ? s.Value : r.Type?.ToString() ?? "unknown"
                ));
                break;

            // output storageId string = storageAccount.id
            case OutputDeclarationSyntax o:
                _table.Register(new Symbol(
                    name: o.Name,
                    kind: SymbolKind.Output,
                    type: o.Type
                ));
                break;

            // module network './network.bicep' = {}
            case ModuleDeclarationSyntax m:
                _table.Register(new Symbol(
                    name: m.Name,
                    kind: SymbolKind.Module,
                    type: m.Path is StringLiteralExpressionSyntax ms ? ms.Value : "module"
                ));
                break;
        }
    }

    // ── TYPE INFERENCE ───────────────────────────────────────
    // Infer the type of a variable from its value expression.
    // Bicep variables have no type annotation so we guess.
    private static string InferType(ExpressionSyntax? expr) => expr switch
    {
        StringLiteralExpressionSyntax  => "string",
        IntegerLiteralExpressionSyntax => "int",
        BooleanLiteralExpressionSyntax => "bool",
        NullLiteralExpressionSyntax    => "null",
        ObjectExpressionSyntax         => "object",
        ArrayExpressionSyntax          => "array",
        _                              => "unknown"
    };
}
