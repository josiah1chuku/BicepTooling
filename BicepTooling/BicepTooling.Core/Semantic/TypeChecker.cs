using BicepTooling.Parser;
using DiagFactory = BicepTooling.Semantic.Diagnostics;

namespace BicepTooling.Semantic;

public class TypeChecker
{
    private readonly SymbolTable _symbols;
    public List<DiagnosticMessage> Diagnostics { get; } = new();

    public List<DiagnosticMessage> Errors =>
        Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

    public List<DiagnosticMessage> Warnings =>
        Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();

    public List<DiagnosticMessage> Infos =>
        Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Info).ToList();

    public TypeChecker(SymbolTable symbols)
    {
        _symbols = symbols;
    }

    public void Check(CompilationUnitSyntax ast)
    {
        foreach (var stmt in ast.Statements)
        {
            if (stmt == null) continue;
            CheckStatement(stmt);
        }
    }

    private void CheckStatement(StatementSyntax stmt)
    {
        switch (stmt)
        {
            case ParameterDeclarationSyntax p: CheckParameter(p); break;
            case VariableDeclarationSyntax  v: CheckVariable(v);  break;
            case ResourceDeclarationSyntax  r: CheckResource(r);  break;
            case OutputDeclarationSyntax    o: CheckOutput(o);    break;
        }
    }

    private void CheckParameter(ParameterDeclarationSyntax p)
    {
        // Check type mismatch on default value
        if (p.Value != null)
        {
            var valueType = InferType(p.Value);
            if (!TypesCompatible(p.Type, valueType))
                Diagnostics.Add(DiagFactory.TypeMismatch(p.Name, p.Type, valueType));
        }
        else
        {
            // Inform user this param is required
            Diagnostics.Add(DiagFactory.RequiredParam(p.Name, p.Type));
        }
    }

    private void CheckVariable(VariableDeclarationSyntax v)
    {
        CheckExpressionReferences(v.Value, $"var '{v.Name}'");
    }

    private void CheckResource(ResourceDeclarationSyntax r)
    {
        var typeStr = r.Type is StringLiteralExpressionSyntax s
            ? s.Value.Trim('\'') : "";

        if (!typeStr.Contains('@'))
            Diagnostics.Add(DiagFactory.MissingApiVersion(r.Name, typeStr));

        if (r.Body is ObjectExpressionSyntax body)
            foreach (var prop in body.Properties)
                CheckExpressionReferences(prop.Value, $"resource '{r.Name}'.{prop.Name}");
    }

    private void CheckOutput(OutputDeclarationSyntax o)
    {
        var valueType = InferType(o.Value);
        if (o.Value is MemberAccessExpressionSyntax)
        {
            CheckExpressionReferences(o.Value, $"output '{o.Name}'");
            return;
        }
        if (valueType != "unknown" && !TypesCompatible(o.Type, valueType))
            Diagnostics.Add(DiagFactory.OutputTypeMismatch(o.Name, o.Type, valueType));
    }

    private void CheckExpressionReferences(ExpressionSyntax? expr, string context)
    {
        if (expr == null) return;
        switch (expr)
        {
            case IdentifierExpressionSyntax id:
                if (!_symbols.Contains(id.Name))
                    Diagnostics.Add(DiagFactory.UndefinedReference(id.Name, context));
                break;
            case MemberAccessExpressionSyntax m:
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
        }
    }

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

    private static bool TypesCompatible(string declared, string actual)
    {
        if (actual == "unknown") return true;
        if (declared == actual)  return true;
        return false;
    }
}
