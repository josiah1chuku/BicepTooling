// ============================================================
// FILE: TypeChecker.cs
// WHAT: Pass 4 — validates that types are used correctly.
//
// THINK OF IT LIKE YOUR HALIX ASSEMBLER:
//   In your assembler you checked that operands matched
//   the expected size/type for each instruction.
//   Here we check that Bicep values match their declared types.
//
// WHAT IT CATCHES:
//   param enableHttps bool = 'yes'  ERROR: string -> bool
//   param count int = true          ERROR: bool -> int
//   output storageId string = 42    ERROR: int -> string
//   var x = undeclaredVar           ERROR: undefined reference
// ============================================================

using BicepTooling.Parser;

namespace BicepTooling.Semantic;

public class TypeChecker
{
    private readonly SymbolTable _symbols;
    public List<string> Errors   { get; } = new();
    public List<string> Warnings { get; } = new();

    public TypeChecker(SymbolTable symbols)
    {
        _symbols = symbols;
    }

    // ── ENTRY POINT ──────────────────────────────────────────
    public void Check(CompilationUnitSyntax ast)
    {
        foreach (var stmt in ast.Statements)
        {
            if (stmt == null) continue;
            CheckStatement(stmt);
        }
    }

    // ── STATEMENT CHECKER ────────────────────────────────────
    private void CheckStatement(StatementSyntax stmt)
    {
        switch (stmt)
        {
            case ParameterDeclarationSyntax p:
                CheckParameter(p);
                break;
            case VariableDeclarationSyntax v:
                CheckVariable(v);
                break;
            case ResourceDeclarationSyntax r:
                CheckResource(r);
                break;
            case OutputDeclarationSyntax o:
                CheckOutput(o);
                break;
        }
    }

    // ── PARAMETER CHECK ──────────────────────────────────────
    // Verifies default value matches declared type.
    // param location string = 'eastus'  OK
    // param count int = 'hello'         ERROR
    private void CheckParameter(ParameterDeclarationSyntax p)
    {
        if (p.Value == null) return; // no default = nothing to check

        var valueType = InferType(p.Value);
        if (!TypesCompatible(p.Type, valueType))
            Errors.Add(
                $"Type mismatch in param '{p.Name}': " +
                $"declared as '{p.Type}' but default value is '{valueType}'.");
    }

    // ── VARIABLE CHECK ───────────────────────────────────────
    // Verifies references in variable values exist.
    // var sku = storageSku   → checks storageSku is defined
    private void CheckVariable(VariableDeclarationSyntax v)
    {
        CheckExpressionReferences(v.Value, $"var '{v.Name}'");
    }

    // ── RESOURCE CHECK ───────────────────────────────────────
    // Verifies resource type string format and body references.
    // resource x 'Microsoft.Storage/storageAccounts@2023-01-01'
    //   → checks type string has @apiVersion
    private void CheckResource(ResourceDeclarationSyntax r)
    {
        // Check resource type string contains @apiVersion
        var typeStr = r.Type is StringLiteralExpressionSyntax s ? s.Value : r.Type?.ToString() ?? "";
        if (!typeStr.Contains('@'))
            Errors.Add(
                $"Resource '{r.Name}': type string '{typeStr}' " +
                $"is missing API version (expected format: 'provider/type@version').");

        // Check all property value references in the body
        if (r.Body is ObjectExpressionSyntax body)
            foreach (var prop in body.Properties)
                CheckExpressionReferences(prop.Value, $"resource '{r.Name}'.{prop.Name}");
    }

    // ── OUTPUT CHECK ─────────────────────────────────────────
    // Verifies output value type matches declared type.
    // output storageId string = storageAccount.id  OK
    // output count string = 42                     WARNING
    private void CheckOutput(OutputDeclarationSyntax o)
    {
        var valueType = InferType(o.Value);

        // For member access (storageAccount.id) we trust the reference
        // check and skip deep type inference — that needs schema data
        if (o.Value is MemberAccessExpressionSyntax)
        {
            CheckExpressionReferences(o.Value, $"output '{o.Name}'");
            return;
        }

        if (valueType != "unknown" && !TypesCompatible(o.Type, valueType))
            Warnings.Add(
                $"Possible type mismatch in output '{o.Name}': " +
                $"declared as '{o.Type}' but value appears to be '{valueType}'.");
    }

    // ── REFERENCE CHECKER ────────────────────────────────────
    // Walks an expression and checks all identifiers are defined.
    private void CheckExpressionReferences(ExpressionSyntax? expr, string context)
    {
        if (expr == null) return;

        switch (expr)
        {
            case IdentifierExpressionSyntax id:
                if (!_symbols.Contains(id.Name))
                    Errors.Add(
                        $"Undefined reference in {context}: '{id.Name}' is not declared.");
                break;

            case MemberAccessExpressionSyntax m:
                // Check the root object exists (e.g. storageAccount in storageAccount.id)
                CheckExpressionReferences(m.Target, context);
                break;

            case ObjectExpressionSyntax obj:
                foreach (var prop in obj.Properties)
                    CheckExpressionReferences(prop.Value, context);
                break;

            case ArrayExpressionSyntax arr:
                foreach (var item in arr.Items)
                    CheckExpressionReferences(item, context);
                break;

            // Literals are always fine
            case StringLiteralExpressionSyntax:
            case IntegerLiteralExpressionSyntax:
            case BooleanLiteralExpressionSyntax:
            case NullLiteralExpressionSyntax:
                break;
        }
    }

    // ── TYPE INFERENCE ───────────────────────────────────────
    // Infer the type of an expression from its value.
    private static string InferType(ExpressionSyntax? expr) => expr switch
    {
        StringLiteralExpressionSyntax  => "string",
        IntegerLiteralExpressionSyntax => "int",
        BooleanLiteralExpressionSyntax => "bool",
        NullLiteralExpressionSyntax    => "null",
        ObjectExpressionSyntax         => "object",
        ArrayExpressionSyntax          => "array",
        IdentifierExpressionSyntax     => "unknown", // resolved at runtime
        MemberAccessExpressionSyntax   => "unknown", // resolved at runtime
        _                              => "unknown"
    };

    // ── TYPE COMPATIBILITY ───────────────────────────────────
    // Check if a value type is compatible with a declared type.
    private static bool TypesCompatible(string declared, string actual)
    {
        if (actual == "unknown") return true; // can't check at compile time
        if (declared == actual)  return true; // exact match
        if (declared == "object" && actual == "object") return true;
        if (declared == "array"  && actual == "array")  return true;
        return false;
    }
}
