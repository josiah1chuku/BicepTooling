// ============================================================
// FILE: DiagnosticMessage.cs
// WHAT: Rich error/warning messages that teach Bicep rules.
//
// INSPIRED BY YOUR HALIX ASSEMBLER:
//   Your assembler had error messages that guided users.
//   This does the same for Bicep — every error explains:
//   1. WHAT went wrong
//   2. WHY it is a problem
//   3. HOW to fix it
//   4. The BICEP RULE that was violated
// ============================================================

namespace BicepTooling.Semantic;

public enum DiagnosticSeverity
{
    Error,    // Must fix — deployment will fail
    Warning,  // Should fix — may cause problems
    Info      // Good to know — educational tip
}

public class DiagnosticMessage
{
    public DiagnosticSeverity Severity { get; }
    public string             Code     { get; }  // e.g. BCP001
    public string             What     { get; }  // what went wrong
    public string             Why      { get; }  // why it matters
    public string             Fix      { get; }  // how to fix it
    public string             Rule     { get; }  // the Bicep rule
    public string             Location { get; private set; }  // where in the file

    public DiagnosticMessage(
        DiagnosticSeverity severity,
        string code,
        string what,
        string why,
        string fix,
        string rule,
        string location = "")
    {
        Severity = severity;
        Code     = code;
        What     = what;
        Why      = why;
        Fix      = fix;
        Rule     = rule;
        Location = location;
    }

    public DiagnosticMessage WithLocation(int line, int col)
    {
        Location = $"[Line {line}, Col {col}]";
        return this;
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        var prefix = Severity switch
        {
            DiagnosticSeverity.Error   => "[ERROR]",
            DiagnosticSeverity.Warning => "[WARNING]",
            DiagnosticSeverity.Info    => "[INFO]",
            _                          => "[INFO]"
        };

        sb.AppendLine($"{prefix} {Code}: {What}");
        if (!string.IsNullOrEmpty(Location))
            sb.AppendLine($"  Location : {Location}");
        sb.AppendLine($"  Problem  : {Why}");
        sb.AppendLine($"  Bicep Rule: {Rule}");
        sb.AppendLine($"  Fix      : {Fix}");
        return sb.ToString();
    }
}

// ── DIAGNOSTIC CATALOG ───────────────────────────────────────
// All error codes and their educational messages.
// Like a reference manual built into your tool.
public static class Diagnostics
{
    // ── TYPE MISMATCH ────────────────────────────────────────
    public static DiagnosticMessage TypeMismatch(
        string paramName, string declaredType, string actualType) =>
        new(
            severity: DiagnosticSeverity.Error,
            code:     "BCP001",
            what:     $"Type mismatch in param '{paramName}'",
            why:      $"You declared '{paramName}' as '{declaredType}' " +
                      $"but the default value is '{actualType}'.",
            rule:     $"Every param default value must match its declared type.\n" +
                      $"  param count int    = 42        correct\n" +
                      $"  param name  string = 'eastus'  correct\n" +
                      $"  param count int    = 'hello'   WRONG",
            fix:      $"Change either the type or the default value:\n" +
                      $"  param {paramName} {actualType} = ...  change type to match value\n" +
                      $"  param {paramName} {declaredType} = ... change value to match type"
        );

    // ── UNDEFINED REFERENCE ──────────────────────────────────
    public static DiagnosticMessage UndefinedReference(
        string name, string context) =>
        new(
            severity: DiagnosticSeverity.Error,
            code:     "BCP002",
            what:     $"Undefined reference '{name}' in {context}",
            why:      $"You used '{name}' but it was never declared " +
                      $"as a param, var, or resource.",
            rule:     "Every identifier must be declared before use.\n" +
                      "  param location string = 'eastus'  declares 'location'\n" +
                      "  var storageSku = 'Standard_LRS'   declares 'storageSku'\n" +
                      "  resource storageAccount '...' = {} declares 'storageAccount'",
            fix:      $"Add a declaration for '{name}':\n" +
                      $"  param {name} string           if it is an input\n" +
                      $"  var {name} = 'value'          if it is a variable\n" +
                      $"  Check for typos in the name"
        );

    // ── DUPLICATE SYMBOL ─────────────────────────────────────
    public static DiagnosticMessage DuplicateSymbol(string name) =>
        new(
            severity: DiagnosticSeverity.Error,
            code:     "BCP003",
            what:     $"Duplicate declaration '{name}'",
            why:      $"'{name}' is declared more than once in this file.",
            rule:     "Every name in a Bicep file must be unique.\n" +
                      "  param location string = 'eastus'  OK\n" +
                      "  param location string = 'westus'  ERROR - duplicate",
            fix:      $"Remove or rename one of the '{name}' declarations."
        );

    // ── MISSING API VERSION ──────────────────────────────────
    public static DiagnosticMessage MissingApiVersion(
        string resourceName, string typeStr) =>
        new(
            severity: DiagnosticSeverity.Error,
            code:     "BCP004",
            what:     $"Resource '{resourceName}' is missing API version",
            why:      $"The type string '{typeStr}' has no @apiVersion. " +
                      $"Azure needs this to know which API to use.",
            rule:     "Resource type strings must include @apiVersion:\n" +
                      "  'Microsoft.Storage/storageAccounts@2023-01-01'  correct\n" +
                      "  'Microsoft.Storage/storageAccounts'              WRONG",
            fix:      $"Add @apiVersion to your resource type string:\n" +
                      $"  resource {resourceName} " +
                      $"'Microsoft.Storage/storageAccounts@2023-01-01' = {{}}"
        );

    // ── MISSING REQUIRED PARAM ───────────────────────────────
    public static DiagnosticMessage RequiredParam(string name, string type) =>
        new(
            severity: DiagnosticSeverity.Info,
            code:     "BCP005",
            what:     $"param '{name}' is required (no default value)",
            why:      $"'{name}' has no default value so whoever deploys " +
                      $"this template MUST provide it.",
            rule:     "Params without defaults are required at deployment:\n" +
                      "  param location string = 'eastus'  optional - has default\n" +
                      "  param storageAccountName string   required - no default",
            fix:      $"Either provide a default value:\n" +
                      $"  param {name} {type} = 'your-default-value'\n" +
                      $"Or document that this param is intentionally required."
        );

    // ── CROSS-RESOURCE MISMATCH ──────────────────────────────
    public static DiagnosticMessage CrossResourceMismatch(
        string name, string actualKind, string member, string context) =>
        new(
            severity: DiagnosticSeverity.Warning,
            code:     "BCP007",
            what:     $"'{name}.{member}' — '{name}' is a {actualKind.ToLower()}, not a resource",
            why:      $"'.{member}' is a resource-only property. " +
                      $"'{name}' is declared as a {actualKind.ToLower()} so it has no '.{member}'. " +
                      $"In {context} this will fail at deployment.",
            rule:     $"Only resource symbolic names have '.id' and '.outputs':\n" +
                      $"  resource storageAccount '...' = {{}}   ← storageAccount.id  OK\n" +
                      $"  var storageName = 'myStorage'         ← storageName.id     WRONG",
            fix:      $"Either declare '{name}' as a resource:\n" +
                      $"  resource {name} '<type>@<apiVersion>' = {{ ... }}\n" +
                      $"Or replace '{name}.{member}' with the literal value you need."
        );

    // ── OUTPUT TYPE MISMATCH ─────────────────────────────────
    public static DiagnosticMessage OutputTypeMismatch(
        string name, string declaredType, string actualType) =>
        new(
            severity: DiagnosticSeverity.Warning,
            code:     "BCP006",
            what:     $"Possible type mismatch in output '{name}'",
            why:      $"Output '{name}' is declared as '{declaredType}' " +
                      $"but the value appears to be '{actualType}'.",
            rule:     "Output types must match their value types:\n" +
                      "  output count  int    = 42           correct\n" +
                      "  output name   string = 'hello'      correct\n" +
                      "  output count  string = 42           WARNING",
            fix:      $"Change the output type to match the value:\n" +
                      $"  output {name} {actualType} = ..."
        );
}
