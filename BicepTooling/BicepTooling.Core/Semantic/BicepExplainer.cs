// ============================================================
// FILE: BicepExplainer.cs
// WHAT: Pass 6 — explains every declaration in plain English.
//
// LEARNING TOOL:
//   A user pastes their Bicep file and gets a plain English
//   explanation of what every line does in Azure.
//   Like a teacher reading through their code with them.
// ============================================================

using BicepTooling.Parser;

namespace BicepTooling.Semantic;

public class BicepExplainer
{
    private readonly SymbolTable _symbols;

    public BicepExplainer(SymbolTable symbols)
    {
        _symbols = symbols;
    }

    public void Explain(CompilationUnitSyntax ast)
    {
        Console.WriteLine("=== BICEP EXPLAINER — What your code does ===\n");

        int lineNum = 1;
        foreach (var stmt in ast.Statements)
        {
            if (stmt == null) continue;
            ExplainStatement(stmt, lineNum++);
            Console.WriteLine();
        }
    }

    private void ExplainStatement(StatementSyntax stmt, int num)
    {
        switch (stmt)
        {
            case ParameterDeclarationSyntax p:
                ExplainParam(p, num);
                break;
            case VariableDeclarationSyntax v:
                ExplainVar(v, num);
                break;
            case ResourceDeclarationSyntax r:
                ExplainResource(r, num);
                break;
            case OutputDeclarationSyntax o:
                ExplainOutput(o, num);
                break;
        }
    }

    private void ExplainParam(ParameterDeclarationSyntax p, int num)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[{num}] PARAMETER: {p.Name}");
        Console.ResetColor();

        Console.WriteLine($"  Type    : {p.Type}");

        if (p.Value != null)
            Console.WriteLine($"  Default : {ExpressionValue(p.Value)}");
        else
            Console.WriteLine($"  Default : none — REQUIRED at deployment");

        Console.WriteLine($"  Meaning : This is an INPUT to your template.");

        if (p.Value != null)
            Console.WriteLine($"  In Azure: If nobody sets it, Azure uses " +
                            $"'{ExpressionValue(p.Value)}'.");
        else
            Console.WriteLine($"  In Azure: Whoever runs 'az deployment' MUST " +
                            $"provide a value for '{p.Name}'.");

        Console.WriteLine($"  Example : az deployment group create " +
                        $"--parameters {p.Name}=myValue");
    }

    private void ExplainVar(VariableDeclarationSyntax v, int num)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[{num}] VARIABLE: {v.Name}");
        Console.ResetColor();

        Console.WriteLine($"  Value   : {ExpressionValue(v.Value)}");
        Console.WriteLine($"  Meaning : This is an INTERNAL variable — not " +
                        $"exposed as an input.");
        Console.WriteLine($"  In Azure: Nobody can override this from outside. " +
                        $"It is always '{ExpressionValue(v.Value)}'.");
        Console.WriteLine($"  Used as : [variables('{v.Name}')] in ARM JSON");
    }

    private void ExplainResource(ResourceDeclarationSyntax r, int num)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[{num}] RESOURCE: {r.Name}");
        Console.ResetColor();

        var typeStr = r.Type is StringLiteralExpressionSyntax s
            ? s.Value.Trim('\'') : r.Type?.ToString() ?? "";
        var atIndex  = typeStr.IndexOf('@');
        var resType  = atIndex >= 0 ? typeStr[..atIndex]  : typeStr;
        var apiVer   = atIndex >= 0 ? typeStr[(atIndex+1)..] : "unknown";
        var resName  = resType.Contains('/') 
            ? resType.Split('/').Last() : resType;

        Console.WriteLine($"  Type    : {resType}");
        Console.WriteLine($"  API Ver : {apiVer}");
        Console.WriteLine($"  Meaning : This CREATES an Azure {resName} " +
                        $"when you deploy.");
        Console.WriteLine($"  Ref as  : Use '{r.Name}.id' to get its " +
                        $"Azure resource ID.");
        Console.WriteLine($"  In Azure: Azure will provision this resource " +
                        $"in your subscription.");

        if (r.Body is ObjectExpressionSyntax body && body.Properties.Count > 0)
        {
            Console.WriteLine($"  Properties ({body.Properties.Count}):");
            foreach (var prop in body.Properties)
                Console.WriteLine($"    {prop.Name}: {ExpressionValue(prop.Value)}");
        }

    }

    private void ExplainOutput(OutputDeclarationSyntax o, int num)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"[{num}] OUTPUT: {o.Name}");
        Console.ResetColor();

        Console.WriteLine($"  Type    : {o.Type}");
        Console.WriteLine($"  Value   : {ExpressionValue(o.Value)}");
        Console.WriteLine($"  Meaning : This VALUE is returned after deployment.");
        Console.WriteLine($"  In Azure: After 'az deployment group create', " +
                        $"you can read this value.");
        Console.WriteLine($"  Example : az deployment group show " +
                        $"--query properties.outputs.{o.Name}.value");
    }

    private string ExpressionValue(ExpressionSyntax? expr) => expr switch
    {
        StringLiteralExpressionSyntax  s => s.Value,
        IntegerLiteralExpressionSyntax i => i.Value,
        BooleanLiteralExpressionSyntax b => b.Value.ToString().ToLower(),
        NullLiteralExpressionSyntax      => "null",
        IdentifierExpressionSyntax     i => i.Name,
        MemberAccessExpressionSyntax   m => $"{ExpressionValue(m.Target)}.{m.Member}",
        ObjectExpressionSyntax         o => $"{{ {o.Properties.Count} properties }}",
        ArrayExpressionSyntax          a => $"[ {a.Items.Count} items ]",
        _                                => "unknown"
    };
}
