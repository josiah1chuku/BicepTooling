using BicepLexer  = BicepTooling.Lexer.Lexer;
using BicepParser = BicepTooling.Parser.Parser;
using BicepTooling.Parser;
using DiagFactory = BicepTooling.Semantic.Diagnostics;

namespace BicepTooling.Semantic;

public class ModuleChecker
{
    private readonly string _baseDirectory;
    public List<DiagnosticMessage> Issues { get; } = new();

    public ModuleChecker(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    public void Check(CompilationUnitSyntax ast)
    {
        foreach (var stmt in ast.Statements)
            if (stmt is ModuleDeclarationSyntax m)
                CheckModule(m);
    }

    private void CheckModule(ModuleDeclarationSyntax m)
    {
        if (m.Path is not StringLiteralExpressionSyntax pathExpr) return;

        var relativePath = pathExpr.Value.Trim('\'');
        var fullPath     = Path.GetFullPath(Path.Combine(_baseDirectory, relativePath));

        if (!File.Exists(fullPath))
        {
            Issues.Add(DiagFactory.ModuleNotFound(m.Name, relativePath)
                .WithLocation(m.Line, m.Column));
            return;
        }

        CompilationUnitSyntax moduleAst;
        try
        {
            var src = File.ReadAllText(fullPath);
            moduleAst = new BicepParser(new BicepLexer(src).Tokenize()).ParseCompilationUnit();
        }
        catch { return; }

        var moduleParams = moduleAst.Statements
            .OfType<ParameterDeclarationSyntax>()
            .ToDictionary(p => p.Name);

        if (m.Body is not ObjectExpressionSyntax body) return;
        var paramsNode = body.Properties.FirstOrDefault(p => p.Name == "params");
        if (paramsNode?.Value is not ObjectExpressionSyntax passedParams) return;

        // BCP009 — unknown params
        foreach (var passed in passedParams.Properties)
        {
            if (!moduleParams.ContainsKey(passed.Name))
                Issues.Add(DiagFactory.UnknownModuleParam(m.Name, passed.Name)
                    .WithLocation(m.Line, m.Column));
        }

        // BCP010 — missing required params
        foreach (var (paramName, param) in moduleParams)
        {
            if (param.Value == null &&
                !passedParams.Properties.Any(p => p.Name == paramName))
                Issues.Add(DiagFactory.MissingModuleParam(m.Name, paramName, param.Type)
                    .WithLocation(m.Line, m.Column));
        }

        // BCP011 — type mismatches
        foreach (var passed in passedParams.Properties)
        {
            if (!moduleParams.TryGetValue(passed.Name, out var moduleParam)) continue;
            var passedType = InferType(passed.Value);
            if (passedType != "unknown" && passedType != moduleParam.Type)
                Issues.Add(DiagFactory.ModuleParamTypeMismatch(
                    m.Name, passed.Name, moduleParam.Type, passedType)
                    .WithLocation(m.Line, m.Column));
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
}
