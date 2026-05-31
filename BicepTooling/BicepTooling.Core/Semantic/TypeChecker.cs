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
        if (p.Value != null)
        {
            var valueType = InferType(p.Value);
            if (!TypesCompatible(p.Type, valueType))
                Diagnostics.Add(DiagFactory.TypeMismatch(p.Name, p.Type, valueType)
                    .WithLocation(p.Line, p.Column));
        }
        else
        {
            Diagnostics.Add(DiagFactory.RequiredParam(p.Name, p.Type)
                .WithLocation(p.Line, p.Column));
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
            Diagnostics.Add(DiagFactory.MissingApiVersion(r.Name, typeStr)
                .WithLocation(r.Line, r.Column));

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
            Diagnostics.Add(DiagFactory.OutputTypeMismatch(o.Name, o.Type, valueType)
                .WithLocation(o.Line, o.Column));
    }

    private void CheckExpressionReferences(ExpressionSyntax? expr, string context)
    {
        if (expr == null) return;
        switch (expr)
        {
            case IdentifierExpressionSyntax id:
                if (!_symbols.Contains(id.Name))
                {
                    // BCP002 — IMPROVED: fuzzy name suggestion
                    var suggestion = SuggestClosestName(
                        id.Name, _symbols.All.Select(s => s.Name));
                    Diagnostics.Add(DiagFactory.UndefinedReference(
                        id.Name + suggestion, context)
                        .WithLocation(id.Line, id.Column));
                }
                break;
            case MemberAccessExpressionSyntax m:
                if ((m.Member == "id" || m.Member == "outputs") &&
                    m.Target is IdentifierExpressionSyntax root)
                {
                    var sym = _symbols.Lookup(root.Name);
                    if (sym != null && sym.Kind != SymbolKind.Resource)
                        Diagnostics.Add(DiagFactory.CrossResourceMismatch(
                            root.Name, sym.Kind.ToString(), m.Member, context)
                            .WithLocation(root.Line, root.Column));
                }
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

    // ── FUZZY NAME SUGGESTION (Levenshtein distance) ─────────────────────────
    // Inspired by Becker et al. (2019): enhanced error messages reduce
    // time-to-fix by 25% in developer studies.
    private static string SuggestClosestName(
        string unknown, IEnumerable<string> declared)
    {
        var candidates = declared.ToList();
        if (candidates.Count == 0) return "";

        var best = candidates
            .Select(d => (name: d, dist: Levenshtein(unknown, d)))
            .OrderBy(x => x.dist)
            .First();

        return best.dist <= 3
            ? $" (Did you mean '{best.name}'?)"
            : "";
    }

    private static int Levenshtein(string a, string b)
    {
        int[,] dp = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                dp[i, j] = a[i-1] == b[j-1]
                    ? dp[i-1, j-1]
                    : 1 + Math.Min(dp[i-1, j-1],
                          Math.Min(dp[i-1, j], dp[i, j-1]));
        return dp[a.Length, b.Length];
    }
    // ─────────────────────────────────────────────────────────────────────────

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
