using BicepTooling.Parser;

namespace BicepTooling.Semantic;

public class SecurityLinter
{
    public List<DiagnosticMessage> Issues { get; } = new();

    public void Lint(CompilationUnitSyntax ast)
    {
        foreach (var stmt in ast.Statements)
        {
            if (stmt == null) continue;
            if (stmt is ResourceDeclarationSyntax r) LintResource(r);
            if (stmt is OutputDeclarationSyntax  o) LintOutput(o);
        }
    }

    private void LintResource(ResourceDeclarationSyntax r)
    {
        var typeStr = r.Type is StringLiteralExpressionSyntax s
            ? s.Value.Trim('\'').ToLower() : "";

        if (typeStr.Contains("storageaccounts"))
            LintStorageAccount(r);

        if (r.Body is ObjectExpressionSyntax body)
        {
            bool hasLocation = body.Properties.Any(p => p.Name.ToLower() == "location");
            if (!hasLocation)
                Issues.Add(new DiagnosticMessage(
                    severity: DiagnosticSeverity.Warning,
                    code:     "SEC004",
                    what:     $"Resource '{r.Name}' is missing a location",
                    why:      "Resources without explicit location may deploy to unexpected regions.",
                    rule:     "Always set location explicitly:\n" +
                              "  resource x '...' = {\n" +
                              "    location: location  // use a param\n" +
                              "  }",
                    fix:      $"Add location to resource '{r.Name}':\n" +
                              $"  param location string = resourceGroup().location\n" +
                              $"  Then add: location: location"
                ));
        }
    }

    private void LintStorageAccount(ResourceDeclarationSyntax r)
    {
        if (r.Body is not ObjectExpressionSyntax body) return;

        var propsNode = body.Properties.FirstOrDefault(p => p.Name.ToLower() == "properties");

        bool hasHttpsOnly    = false;
        bool hasMinTls       = false;
        bool hasPublicAccess = false;

        if (propsNode?.Value is ObjectExpressionSyntax props)
        {
            hasHttpsOnly = props.Properties.Any(p => p.Name == "supportsHttpsTrafficOnly");
            hasMinTls    = props.Properties.Any(p => p.Name == "minimumTlsVersion");
            var publicAccess = props.Properties.FirstOrDefault(p => p.Name == "allowBlobPublicAccess");
            if (publicAccess?.Value is BooleanLiteralExpressionSyntax b && b.Value)
                hasPublicAccess = true;
        }

        if (!hasHttpsOnly)
            Issues.Add(new DiagnosticMessage(
                severity: DiagnosticSeverity.Warning,
                code:     "SEC001",
                what:     $"Storage '{r.Name}' missing HTTPS-only enforcement",
                why:      "Without this, data can be transferred over unencrypted HTTP — a serious security risk.",
                rule:     "Azure Storage best practice requires HTTPS-only:\n" +
                          "  properties: {\n" +
                          "    supportsHttpsTrafficOnly: true\n" +
                          "  }",
                fix:      $"Add to resource '{r.Name}' properties:\n" +
                          $"  supportsHttpsTrafficOnly: true"
            ));

        if (!hasMinTls)
            Issues.Add(new DiagnosticMessage(
                severity: DiagnosticSeverity.Warning,
                code:     "SEC002",
                what:     $"Storage '{r.Name}' missing minimum TLS version",
                why:      "TLS 1.0 and 1.1 have known vulnerabilities. Enforcing TLS 1.2+ protects data in transit.",
                rule:     "Set minimum TLS version to 1.2:\n" +
                          "  properties: {\n" +
                          "    minimumTlsVersion: 'TLS1_2'\n" +
                          "  }",
                fix:      $"Add to resource '{r.Name}' properties:\n" +
                          $"  minimumTlsVersion: 'TLS1_2'"
            ));

        if (hasPublicAccess)
            Issues.Add(new DiagnosticMessage(
                severity: DiagnosticSeverity.Error,
                code:     "SEC003",
                what:     $"Storage '{r.Name}' allows public blob access",
                why:      "Public blob access means ANYONE on the internet can read your storage data — a critical risk.",
                rule:     "Never enable public blob access in production:\n" +
                          "  properties: {\n" +
                          "    allowBlobPublicAccess: false\n" +
                          "  }",
                fix:      $"Change in resource '{r.Name}':\n" +
                          $"  allowBlobPublicAccess: false"
            ));
    }

    private void LintOutput(OutputDeclarationSyntax o)
    {
        var sensitiveNames = new[] { "key", "secret", "password", "connectionstring", "token" };
        if (sensitiveNames.Any(s => o.Name.ToLower().Contains(s)))
            Issues.Add(new DiagnosticMessage(
                severity: DiagnosticSeverity.Warning,
                code:     "SEC005",
                what:     $"Output '{o.Name}' may expose sensitive data",
                why:      "Outputting keys, passwords, or secrets makes them visible in deployment history.",
                rule:     "Never output secrets from Bicep templates.\n" +
                          "Use Azure Key Vault references instead:\n" +
                          "  resource kv 'Microsoft.KeyVault/vaults@2023-02-01'",
                fix:      $"Remove output '{o.Name}' and store the secret in Azure Key Vault instead."
            ));
    }
}
