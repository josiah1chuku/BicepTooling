// ============================================================
// FILE: ArmGenerator.cs
// WHAT: Pass 5 — converts the validated AST into ARM JSON.
//
// THIS IS THE FINAL PASS — the output of your entire pipeline.
//
// ARM JSON is what Azure actually deploys. The official Bicep
// compiler does exactly this. You are building a simplified
// version of that same process.
//
// INPUT:  CompilationUnitSyntax (your AST from Pass 2)
// OUTPUT: A valid ARM template JSON string
//
// ARM TEMPLATE STRUCTURE:
// {
//   "$schema": "...",
//   "contentVersion": "1.0.0.0",
//   "parameters": { },   ← from your param declarations
//   "variables":  { },   ← from your var declarations
//   "resources":  [ ],   ← from your resource declarations
//   "outputs":    { }    ← from your output declarations
// }
// ============================================================

using System.Text;
using BicepTooling.Parser;
using BicepTooling.Semantic;

namespace BicepTooling.CodeGen;

public class ArmGenerator
{
    // Indentation level for pretty printing
    private int _indent = 0;
    private readonly StringBuilder _sb = new();
    private HashSet<string> _variableNames = new();

    // ── ENTRY POINT ──────────────────────────────────────────
    public string Generate(CompilationUnitSyntax ast, SymbolTable? symbols = null)
    {
        // Load variable names for correct ARM expression generation
        if (symbols != null)
            _variableNames = symbols.All
                .Where(s => s.Kind == SymbolKind.Variable)
                .Select(s => s.Name)
                .ToHashSet();

        // Collect all declaration types
        var parameters = ast.Statements
            .OfType<ParameterDeclarationSyntax>().ToList();
        var variables  = ast.Statements
            .OfType<VariableDeclarationSyntax>().ToList();
        var resources  = ast.Statements
            .OfType<ResourceDeclarationSyntax>().ToList();
        var outputs    = ast.Statements
            .OfType<OutputDeclarationSyntax>().ToList();

        // Build the ARM template
        WriteLine("{");
        _indent++;

        // Required ARM template fields
        WriteProperty("$schema",
            "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#");
        WriteProperty("contentVersion", "1.0.0.0");

        // Parameters section
        WriteKey("parameters");
        WriteLine("{");
        _indent++;
        for (int i = 0; i < parameters.Count; i++)
            WriteParameter(parameters[i], i < parameters.Count - 1);
        _indent--;
        WriteLine("},");

        // Variables section
        WriteKey("variables");
        WriteLine("{");
        _indent++;
        for (int i = 0; i < variables.Count; i++)
            WriteVariable(variables[i], i < variables.Count - 1);
        _indent--;
        WriteLine("},");

        // Resources section (array not object)
        WriteKey("resources");
        WriteLine("[");
        _indent++;
        for (int i = 0; i < resources.Count; i++)
            WriteResource(resources[i], i < resources.Count - 1);
        _indent--;
        WriteLine("],");

        // Outputs section
        WriteKey("outputs");
        WriteLine("{");
        _indent++;
        for (int i = 0; i < outputs.Count; i++)
            WriteOutput(outputs[i], i < outputs.Count - 1);
        _indent--;
        WriteLine("}");

        _indent--;
        WriteLine("}");

        return _sb.ToString();
    }

    // ── PARAMETER → ARM JSON ─────────────────────────────────
    // Bicep:  param location string = 'eastus'
    // ARM:    "location": { "type": "string", "defaultValue": "eastus" }
    private void WriteParameter(ParameterDeclarationSyntax p, bool comma)
    {
        WriteKey(p.Name);
        WriteLine("{");
        _indent++;
        bool hasDefault = p.Value != null;
        WriteLine($"\"type\": \"{p.Type}\"" + (hasDefault ? "," : ""));
        if (p.Value != null)
        {
            // Last property has no comma
            WritePropertyRaw("defaultValue", ExpressionToArm(p.Value), false);
        }
        _indent--;
        WriteLine("}" + (comma ? "," : ""));
    }

    // ── VARIABLE → ARM JSON ──────────────────────────────────
    // Bicep:  var storageSku = 'Standard_LRS'
    // ARM:    "storageSku": "Standard_LRS"
    private void WriteVariable(VariableDeclarationSyntax v, bool comma)
    {
        var value = ExpressionToArm(v.Value);
        WriteLine($"\"{v.Name}\": {value}" + (comma ? "," : ""));
    }

    // ── RESOURCE → ARM JSON ──────────────────────────────────
    // Bicep:
    //   resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
    //     name: storageAccountName
    //     location: location
    //   }
    //
    // ARM:
    //   {
    //     "type": "Microsoft.Storage/storageAccounts",
    //     "apiVersion": "2023-01-01",
    //     "name": "[parameters('storageAccountName')]",
    //     "location": "[parameters('location')]"
    //   }
    private void WriteResource(ResourceDeclarationSyntax r, bool comma)
    {
        // Split 'Microsoft.Storage/storageAccounts@2023-01-01'
        // into type and apiVersion
        var typeStr = r.Type is StringLiteralExpressionSyntax s
            ? s.Value.Trim('\'') : "";
        var atIndex = typeStr.IndexOf('@');
        var resType  = atIndex >= 0 ? typeStr[..atIndex]  : typeStr;
        var apiVer   = atIndex >= 0 ? typeStr[(atIndex+1)..] : "";

        WriteLine("{");
        _indent++;
        WriteProperty("type",       resType);
        WriteProperty("apiVersion", apiVer);

        // Write body properties
        if (r.Body is ObjectExpressionSyntax body)
        {
            foreach (var prop in body.Properties)
            {
                var val = ExpressionToArm(prop.Value);
                WriteLine($"\"{prop.Name}\": {val},");
            }
        }

        _indent--;
        WriteLine("}" + (comma ? "," : ""));
    }

    // ── OUTPUT → ARM JSON ────────────────────────────────────
    // Bicep:  output storageId string = storageAccount.id
    // ARM:    "storageId": { "type": "string", "value": "[reference(...)]" }
    private void WriteOutput(OutputDeclarationSyntax o, bool comma)
    {
        WriteKey(o.Name);
        WriteLine("{");
        _indent++;
        WriteProperty("type",  o.Type);
        WritePropertyRaw("value", ExpressionToArm(o.Value), false);
        _indent--;
        WriteLine("}" + (comma ? "," : ""));
    }

    // ── EXPRESSION → ARM EXPRESSION STRING ───────────────────
    // Converts AST expression nodes to ARM template expressions.
    //
    // BICEP LESSON: ARM uses a special expression syntax with [brackets]
    //   Bicep:  location          → ARM: "[parameters('location')]"
    //   Bicep:  storageSku        → ARM: "[variables('storageSku')]"
    //   Bicep:  storageAccount.id → ARM: "[resourceId(...)]"
    //   Bicep:  'eastus'          → ARM: "eastus" (plain string)
    //   Bicep:  42                → ARM: 42 (plain number)
    private string ExpressionToArm(ExpressionSyntax? expr)
    {
        return expr switch
        {
            // String literal → plain JSON string
            StringLiteralExpressionSyntax s =>
                $"\"{s.Value.Trim('\'')}\"",

            // Integer literal → plain JSON number
            IntegerLiteralExpressionSyntax i =>
                i.Value,

            // Boolean literal → plain JSON boolean
            BooleanLiteralExpressionSyntax b =>
                b.Value.ToString().ToLower(),

            // Null literal
            NullLiteralExpressionSyntax =>
                "null",

            // Identifier → ARM parameters() or variables() expression
            // We don't know which without checking the symbol table,
            // so we emit parameters() as default (most common case)
            IdentifierExpressionSyntax id when _variableNames.Contains(id.Name) =>
                $"\"[variables('{id.Name}')]\"",

            IdentifierExpressionSyntax id =>
                $"\"[parameters('{id.Name}')]\"",

            // Member access: storageAccount.id
            // → ARM reference expression
            MemberAccessExpressionSyntax m =>
                $"\"[reference('{GetRootName(m)}').{GetMemberChain(m)}]\"",

            // Object → JSON object
            ObjectExpressionSyntax obj =>
                ObjectToArm(obj),

            // Array → JSON array
            ArrayExpressionSyntax arr =>
                $"[{string.Join(", ", arr.Items.Select(ExpressionToArm))}]",

            _ => "null"
        };
    }

    // Gets root identifier name from member access chain
    // storageAccount.properties.id → "storageAccount"
    private string GetRootName(ExpressionSyntax expr) => expr switch
    {
        IdentifierExpressionSyntax id when _variableNames.Contains(id.Name) =>
            $"\"[variables('{id.Name}')]\"",

        IdentifierExpressionSyntax id => id.Name,
        MemberAccessExpressionSyntax m => GetRootName(m.Target),
        _ => "unknown"
    };

    // Gets member chain from member access
    // storageAccount.properties.id → "properties.id"
    private string GetMemberChain(MemberAccessExpressionSyntax m) =>
        m.Target is MemberAccessExpressionSyntax parent
            ? $"{GetMemberChain(parent)}.{m.Member}"
            : m.Member;

    // Convert object expression to inline JSON
    private string ObjectToArm(ObjectExpressionSyntax obj)
    {
        var props = obj.Properties
            .Select(p => $"\"{p.Name}\": {ExpressionToArm(p.Value)}");
        return "{ " + string.Join(", ", props) + " }";
    }

    // ── WRITE HELPERS ────────────────────────────────────────
    private void WriteLine(string line) =>
        _sb.AppendLine(new string(' ', _indent * 2) + line);

    private void WriteKey(string key) =>
        _sb.Append(new string(' ', _indent * 2) + $"\"{key}\": ");

    private void WriteProperty(string key, string value) =>
        WriteLine($"\"{key}\": \"{value}\",");

    private void WritePropertyRaw(string key, string value, bool comma) =>
        WriteLine($"\"{key}\": {value}" + (comma ? "," : ""));
}
