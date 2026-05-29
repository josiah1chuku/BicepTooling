// ============================================================
// FILE: SecurityLinter.cs
// WHAT: Pass 7 — detects Azure security misconfigurations
//       before they reach production cloud systems.
//
// PUBLIC SAFETY CONTEXT:
//   Public safety agencies deploy 911 dispatch, incident
//   management, and first responder systems on Azure.
//   Each rule below maps to a real compliance standard:
//     CJIS Security Policy (criminal justice data)
//     NIST SP 800-53 (federal security controls)
//     FIPS 140-2 (encryption standards)
//     FedRAMP (federal cloud authorization)
//
// RULES:
//   SEC001 — Storage: missing HTTPS-only enforcement
//   SEC002 — Storage: missing TLS 1.2 minimum
//   SEC003 — Storage: public blob access enabled
//   SEC004 — Any resource: missing location property
//   SEC005 — Output: sensitive name exposes credentials
//   SEC006 — Key Vault: soft delete not enabled
//   SEC007 — SQL Server: missing TDE / auditing
//   SEC008 — Virtual Network: missing DDoS protection
//   SEC009 — App Service: missing HTTPS-only flag
//   SEC010 — Role Assignment: overly broad permissions
// ============================================================

using BicepTooling.Parser;

namespace BicepTooling.Semantic;

public class SecurityLinter
{
    public List<DiagnosticMessage> Issues { get; } = new();

    // ── RESOURCE TYPE MATCHERS ────────────────────────────────
    private static bool IsStorageAccount(ResourceDeclarationSyntax r) =>
        GetTypeStr(r).Contains("storageaccounts");

    private static bool IsKeyVault(ResourceDeclarationSyntax r) =>
        GetTypeStr(r).Contains("vaults") &&
        GetTypeStr(r).Contains("keyvault");

    private static bool IsSqlServer(ResourceDeclarationSyntax r) =>
        GetTypeStr(r).Contains("microsoft.sql/servers");

    private static bool IsSqlDatabase(ResourceDeclarationSyntax r) =>
        GetTypeStr(r).Contains("microsoft.sql/servers/databases");

    private static bool IsVirtualNetwork(ResourceDeclarationSyntax r) =>
        GetTypeStr(r).Contains("virtualnetworks");

    private static bool IsAppService(ResourceDeclarationSyntax r) =>
        GetTypeStr(r).Contains("microsoft.web/sites");

    private static bool IsRoleAssignment(ResourceDeclarationSyntax r) =>
        GetTypeStr(r).Contains("roleassignments");

    private static string GetTypeStr(ResourceDeclarationSyntax r) =>
        r.Type is StringLiteralExpressionSyntax s
            ? s.Value.Trim('\'').ToLower()
            : string.Empty;

    // ── MAIN LINT ENTRY POINT ─────────────────────────────────
    public void Lint(CompilationUnitSyntax ast)
    {
        foreach (var stmt in ast.Statements)
        {
            if (stmt == null) continue;

            if (stmt is ResourceDeclarationSyntax r)
                LintResource(r);

            if (stmt is OutputDeclarationSyntax o)
                LintOutput(o);
        }
    }

    // ── RESOURCE ROUTER ──────────────────────────────────────
    private void LintResource(ResourceDeclarationSyntax r)
    {
        // Always check for missing location (SEC004)
        LintLocation(r);

        // Route to type-specific linters
        if (IsStorageAccount(r))  LintStorageAccount(r);
        if (IsKeyVault(r))        LintKeyVault(r);
        if (IsSqlServer(r))       LintSqlServer(r);
        if (IsVirtualNetwork(r))  LintVirtualNetwork(r);
        if (IsAppService(r))      LintAppService(r);
        if (IsRoleAssignment(r))  LintRoleAssignment(r);
    }

    // ── SEC004: MISSING LOCATION ──────────────────────────────
    private void LintLocation(ResourceDeclarationSyntax r)
    {
        if (r.Body is not ObjectExpressionSyntax body) return;

        bool hasLocation = body.Properties
            .Any(p => p.Name.ToLower() == "location");

        if (!hasLocation)
            Issues.Add(new DiagnosticMessage(
                severity: DiagnosticSeverity.Warning,
                code:     "SEC004",
                what:     $"Resource '{r.Name}' is missing a location",
                why:      "Resources without explicit location may deploy to unexpected " +
                          "Azure regions. For public safety systems, data residency " +
                          "requirements (CJIS, state law) may mandate specific regions.",
                rule:     "Always set location explicitly using a parameter:\n" +
                          "  param location string = resourceGroup().location\n" +
                          "  resource x '...' = {\n" +
                          "    location: location\n" +
                          "  }",
                fix:      $"Add location to '{r.Name}':\n" +
                          "  location: location\n" +
                          "  (use a param so it can be set per environment)"
            ));
    }

    // ── SEC001-003: STORAGE ACCOUNT ──────────────────────────
    private void LintStorageAccount(ResourceDeclarationSyntax r)
    {
        if (r.Body is not ObjectExpressionSyntax body) return;

        var propsNode = body.Properties
            .FirstOrDefault(p => p.Name.ToLower() == "properties");

        bool hasHttpsOnly    = false;
        bool hasMinTls       = false;
        bool hasPublicAccess = false;

        if (propsNode?.Value is ObjectExpressionSyntax props)
        {
            hasHttpsOnly = props.Properties
                .Any(p => p.Name == "supportsHttpsTrafficOnly");

            hasMinTls = props.Properties
                .Any(p => p.Name == "minimumTlsVersion");

            var publicAccess = props.Properties
                .FirstOrDefault(p => p.Name == "allowBlobPublicAccess");

            if (publicAccess?.Value is BooleanLiteralExpressionSyntax b
                && b.Value)
                hasPublicAccess = true;
        }

        // SEC001
        if (!hasHttpsOnly)
            Issues.Add(new DiagnosticMessage(
                severity: DiagnosticSeverity.Warning,
                code:     "SEC001",
                what:     $"Storage '{r.Name}' is missing HTTPS-only enforcement",
                why:      "Without this, data can travel over unencrypted HTTP. " +
                          "CJIS Security Policy 5.10.1.3 mandates encryption " +
                          "in transit for all criminal justice information.",
                rule:     "Azure Storage best practice (CJIS 5.10):\n" +
                          "  properties: {\n" +
                          "    supportsHttpsTrafficOnly: true\n" +
                          "  }",
                fix:      $"Add to '{r.Name}' properties block:\n" +
                          "  supportsHttpsTrafficOnly: true"
            ));

        // SEC002
        if (!hasMinTls)
            Issues.Add(new DiagnosticMessage(
                severity: DiagnosticSeverity.Warning,
                code:     "SEC002",
                what:     $"Storage '{r.Name}' is missing minimum TLS version",
                why:      "TLS 1.0 and 1.1 have known cryptographic vulnerabilities. " +
                          "FIPS 140-2 and CJIS require TLS 1.2 or higher for " +
                          "all data in transit on public safety systems.",
                rule:     "Set minimum TLS version to 1.2 (FIPS 140-2):\n" +
                          "  properties: {\n" +
                          "    minimumTlsVersion: 'TLS1_2'\n" +
                          "  }",
                fix:      $"Add to '{r.Name}' properties block:\n" +
                          "  minimumTlsVersion: 'TLS1_2'"
            ));

        // SEC003
        if (hasPublicAccess)
            Issues.Add(new DiagnosticMessage(
                severity: DiagnosticSeverity.Error,
                code:     "SEC003",
                what:     $"Storage '{r.Name}' allows public blob access",
                why:      "Public blob access means ANYONE on the internet can " +
                          "read your storage data without authentication. For " +
                          "public safety systems, this could expose incident " +
                          "records, evidence files, or operational data. " +
                          "This violates NIST SP 800-53 AC-3 (access enforcement).",
                rule:     "Never enable public blob access (NIST 800-53 AC-3):\n" +
                          "  properties: {\n" +
                          "    allowBlobPublicAccess: false\n" +
                          "  }",
                fix:      $"Change in '{r.Name}':\n" +
                          "  allowBlobPublicAccess: false"
            ));
    }

    // ── SEC006: KEY VAULT ────────────────────────────────────
    // Key Vaults store encryption keys, certificates, and secrets
    // for public safety systems. Soft delete prevents accidental
    // or malicious permanent deletion of critical key material.
    private void LintKeyVault(ResourceDeclarationSyntax r)
    {
        if (r.Body is not ObjectExpressionSyntax body) return;

        var propsNode = body.Properties
            .FirstOrDefault(p => p.Name.ToLower() == "properties");

        bool hasSoftDelete    = false;
        bool hasPurgeProtect  = false;

        if (propsNode?.Value is ObjectExpressionSyntax props)
        {
            // softDeleteRetentionInDays should be >= 7
            hasSoftDelete = props.Properties
                .Any(p => p.Name == "softDeleteRetentionInDays");

            hasPurgeProtect = props.Properties
                .Any(p => p.Name == "enablePurgeProtection");
        }

        // SEC006a: missing soft delete
        if (!hasSoftDelete)
            Issues.Add(new DiagnosticMessage(
                severity: DiagnosticSeverity.Error,
                code:     "SEC006",
                what:     $"Key Vault '{r.Name}' is missing soft delete retention",
                why:      "Without soft delete, encryption keys and certificates " +
                          "for public safety systems can be permanently and " +
                          "immediately deleted — potentially locking agencies " +
                          "out of encrypted incident databases. " +
                          "NIST SP 800-53 CP-9 requires data backup and recovery.",
                rule:     "Enable soft delete with minimum 7-day retention:\n" +
                          "  properties: {\n" +
                          "    softDeleteRetentionInDays: 90\n" +
                          "    enablePurgeProtection: true\n" +
                          "  }",
                fix:      $"Add to '{r.Name}' properties:\n" +
                          "  softDeleteRetentionInDays: 90\n" +
                          "  enablePurgeProtection: true"
            ));

        // SEC006b: missing purge protection (separate finding)
        else if (!hasPurgeProtect)
            Issues.Add(new DiagnosticMessage(
                severity: DiagnosticSeverity.Warning,
                code:     "SEC006",
                what:     $"Key Vault '{r.Name}' is missing purge protection",
                why:      "Without purge protection, soft-deleted vaults can " +
                          "be permanently purged before the retention period ends. " +
                          "This prevents recovery of encryption keys critical " +
                          "to public safety data systems.",
                rule:     "Enable purge protection alongside soft delete:\n" +
                          "  properties: {\n" +
                          "    enablePurgeProtection: true\n" +
                          "  }",
                fix:      $"Add to '{r.Name}' properties:\n" +
                          "  enablePurgeProtection: true"
            ));
    }

    // ── SEC007: SQL SERVER ───────────────────────────────────
    // SQL Servers in public safety contexts hold incident
    // databases, records management systems, and CAD data.
    // Auditing and TDE are mandatory for CJIS compliance.
    private void LintSqlServer(ResourceDeclarationSyntax r)
    {
        if (r.Body is not ObjectExpressionSyntax body) return;

        var propsNode = body.Properties
            .FirstOrDefault(p => p.Name.ToLower() == "properties");

        bool hasMinTls = false;

        if (propsNode?.Value is ObjectExpressionSyntax props)
        {
            // minimalTlsVersion should be '1.2'
            hasMinTls = props.Properties
                .Any(p => p.Name == "minimalTlsVersion");
        }

        // SEC007: missing minimum TLS on SQL
        if (!hasMinTls)
            Issues.Add(new DiagnosticMessage(
                severity: DiagnosticSeverity.Error,
                code:     "SEC007",
                what:     $"SQL Server '{r.Name}' is missing minimum TLS version",
                why:      "SQL Server connections without TLS 1.2 enforcement " +
                          "are vulnerable to downgrade attacks. For public safety " +
                          "records management systems holding CJIS-regulated data, " +
                          "all database connections must use TLS 1.2 or higher.",
                rule:     "Enforce TLS 1.2 on SQL Server (CJIS 5.10 / FIPS 140-2):\n" +
                          "  properties: {\n" +
                          "    minimalTlsVersion: '1.2'\n" +
                          "    publicNetworkAccess: 'Disabled'\n" +
                          "  }",
                fix:      $"Add to '{r.Name}' properties:\n" +
                          "  minimalTlsVersion: '1.2'\n" +
                          "  publicNetworkAccess: 'Disabled'"
            ));
    }

    // ── SEC008: VIRTUAL NETWORK ──────────────────────────────
    // Virtual networks connect public safety systems including
    // emergency operations centers, dispatch systems, and
    // first responder communication platforms. DDoS protection
    // is critical for 911 system availability.
    private void LintVirtualNetwork(ResourceDeclarationSyntax r)
    {
        if (r.Body is not ObjectExpressionSyntax body) return;

        // Check for DDoS protection plan in properties
        var propsNode = body.Properties
            .FirstOrDefault(p => p.Name.ToLower() == "properties");

        bool hasDdosProtection = false;

        if (propsNode?.Value is ObjectExpressionSyntax props)
        {
            hasDdosProtection = props.Properties
                .Any(p => p.Name == "ddosProtectionPlan" ||
                          p.Name == "enableDdosProtection");
        }

        // SEC008: missing DDoS protection
        if (!hasDdosProtection)
            Issues.Add(new DiagnosticMessage(
                severity: DiagnosticSeverity.Warning,
                code:     "SEC008",
                what:     $"Virtual Network '{r.Name}' has no DDoS protection configured",
                why:      "Public safety networks including 911 dispatch, emergency " +
                          "alert systems, and first responder communications are " +
                          "high-value DDoS targets. A successful DDoS attack during " +
                          "a mass casualty event could disable emergency response. " +
                          "NIST SP 800-53 SC-5 requires DoS protection.",
                rule:     "Enable DDoS protection (NIST 800-53 SC-5):\n" +
                          "  resource ddosPlan 'Microsoft.Network/" +
                          "ddosProtectionPlans@2023-04-01' = {\n" +
                          "    name: ddosProtectionPlanName\n" +
                          "    location: location\n" +
                          "  }\n" +
                          "  properties: {\n" +
                          "    enableDdosProtection: true\n" +
                          "    ddosProtectionPlan: { id: ddosPlan.id }\n" +
                          "  }",
                fix:      $"Add DDoS protection plan to '{r.Name}' properties:\n" +
                          "  enableDdosProtection: true\n" +
                          "  ddosProtectionPlan: { id: ddosPlan.id }"
            ));
    }

    // ── SEC009: APP SERVICE ──────────────────────────────────
    // App Services host public safety web applications
    // including dispatch portals, evidence management UIs,
    // and citizen reporting systems. HTTPS must be enforced.
    private void LintAppService(ResourceDeclarationSyntax r)
    {
        if (r.Body is not ObjectExpressionSyntax body) return;

        var propsNode = body.Properties
            .FirstOrDefault(p => p.Name.ToLower() == "properties");

        bool hasHttpsOnly = false;
        bool hasMinTls    = false;

        if (propsNode?.Value is ObjectExpressionSyntax props)
        {
            hasHttpsOnly = props.Properties
                .Any(p => p.Name == "httpsOnly");

            hasMinTls = props.Properties
                .Any(p => p.Name == "minTlsVersion" ||
                          p.Name == "minTlsCipherSuite");
        }

        // SEC009a: missing HTTPS only
        if (!hasHttpsOnly)
            Issues.Add(new DiagnosticMessage(
                severity: DiagnosticSeverity.Error,
                code:     "SEC009",
                what:     $"App Service '{r.Name}' is missing HTTPS-only enforcement",
                why:      "Without httpsOnly: true, your public safety web " +
                          "application accepts unencrypted HTTP connections. " +
                          "Officers accessing dispatch portals or evidence " +
                          "management systems over HTTP expose credentials " +
                          "and case data. CJIS 5.10 mandates encryption in transit.",
                rule:     "Enforce HTTPS for all App Service connections (CJIS 5.10):\n" +
                          "  resource app 'Microsoft.Web/sites@2023-01-01' = {\n" +
                          "    properties: {\n" +
                          "      httpsOnly: true\n" +
                          "    }\n" +
                          "  }",
                fix:      $"Add to '{r.Name}' properties:\n" +
                          "  httpsOnly: true"
            ));

        // SEC009b: missing min TLS version
        if (!hasMinTls)
            Issues.Add(new DiagnosticMessage(
                severity: DiagnosticSeverity.Warning,
                code:     "SEC009",
                what:     $"App Service '{r.Name}' has no minimum TLS version set",
                why:      "App Services without a minimum TLS version may accept " +
                          "TLS 1.0 or 1.1 connections. FIPS 140-2 requires " +
                          "TLS 1.2 or higher for public safety applications.",
                rule:     "Set minimum TLS version in siteConfig (FIPS 140-2):\n" +
                          "  properties: {\n" +
                          "    siteConfig: {\n" +
                          "      minTlsVersion: '1.2'\n" +
                          "    }\n" +
                          "  }",
                fix:      $"Add to '{r.Name}' siteConfig:\n" +
                          "  minTlsVersion: '1.2'"
            ));
    }

    // ── SEC010: ROLE ASSIGNMENT ──────────────────────────────
    // Role assignments control who can access public safety
    // cloud resources. Overly broad roles (Owner, Contributor)
    // on sensitive resources violate least-privilege principles
    // required by CJIS and NIST 800-53.
    private void LintRoleAssignment(ResourceDeclarationSyntax r)
    {
        if (r.Body is not ObjectExpressionSyntax body) return;

        var propsNode = body.Properties
            .FirstOrDefault(p => p.Name.ToLower() == "properties");

        if (propsNode?.Value is not ObjectExpressionSyntax props)
            return;

        // Check roleDefinitionId for well-known overly broad roles
        var roleDefProp = props.Properties
            .FirstOrDefault(p => p.Name == "roleDefinitionId");

        if (roleDefProp?.Value is StringLiteralExpressionSyntax roleDef)
        {
            var roleStr = roleDef.Value.ToLower();

            // Owner role GUID: 8e3af657-a8ff-443c-a75c-2fe8c4bcb635
            // Contributor role GUID: b24988ac-6180-42a0-ab88-20f7382dd24c
            bool isOwner = roleStr.Contains("8e3af657") ||
                           roleStr.Contains("owner");
            bool isContributor = roleStr.Contains("b24988ac") ||
                                 roleStr.Contains("contributor");

            if (isOwner)
                Issues.Add(new DiagnosticMessage(
                    severity: DiagnosticSeverity.Error,
                    code:     "SEC010",
                    what:     $"Role assignment '{r.Name}' grants Owner role",
                    why:      "The Owner role grants full control including the " +
                              "ability to change access policies on public safety " +
                              "resources. Assigning Owner to service principals or " +
                              "automated pipelines violates the least-privilege " +
                              "principle required by NIST SP 800-53 AC-6 and CJIS.",
                    rule:     "Use the minimum required role (NIST 800-53 AC-6):\n" +
                              "  Owner       → use Contributor or custom role\n" +
                              "  Contributor → use a specific Reader or Data role\n" +
                              "  Reader      → acceptable for read-only scenarios",
                    fix:      $"Replace Owner role in '{r.Name}' with a specific " +
                              "built-in role:\n" +
                              "  Storage Blob Data Reader  (read-only access)\n" +
                              "  Storage Blob Data Contributor (read-write)\n" +
                              "  Key Vault Secrets User    (secrets access only)"
                ));

            else if (isContributor)
                Issues.Add(new DiagnosticMessage(
                    severity: DiagnosticSeverity.Warning,
                    code:     "SEC010",
                    what:     $"Role assignment '{r.Name}' grants broad Contributor role",
                    why:      "Contributor grants full resource management rights " +
                              "except access policy changes. For public safety " +
                              "resources, prefer specific data-plane roles over " +
                              "management-plane roles. NIST SP 800-53 AC-6.",
                    rule:     "Consider more specific roles (NIST 800-53 AC-6):\n" +
                              "  Instead of Contributor, use:\n" +
                              "  Storage Blob Data Contributor\n" +
                              "  SQL DB Contributor\n" +
                              "  Key Vault Contributor",
                    fix:      $"Review role in '{r.Name}' — use the most specific " +
                              "data-plane role available for this resource type."
                ));
        }
    }

    // ── SEC005: SENSITIVE OUTPUT ─────────────────────────────
    private void LintOutput(OutputDeclarationSyntax o)
    {
        var sensitiveNames = new[]
        {
            "key", "secret", "password", "connectionstring",
            "token", "credential", "apikey", "accesskey",
            "primarykey", "secondarykey", "sharedaccesssignature"
        };

        if (sensitiveNames.Any(s => o.Name.ToLower().Contains(s)))
            Issues.Add(new DiagnosticMessage(
                severity: DiagnosticSeverity.Warning,
                code:     "SEC005",
                what:     $"Output '{o.Name}' may expose sensitive data",
                why:      "Outputting keys, passwords, tokens, or connection " +
                          "strings makes them visible in Azure deployment history " +
                          "which is accessible to anyone with Reader access to " +
                          "the resource group. For public safety systems, this " +
                          "could expose credentials for 911 systems or incident " +
                          "databases. NIST SP 800-53 SC-12.",
                rule:     "Never output secrets from Bicep templates.\n" +
                          "Use Azure Key Vault references instead:\n" +
                          "  resource kv 'Microsoft.KeyVault/" +
                          "vaults@2023-02-01' existing = {\n" +
                          "    name: keyVaultName\n" +
                          "  }",
                fix:      $"Remove output '{o.Name}' and store the secret " +
                          "in Azure Key Vault. Reference it at runtime."
            ));
    }
}
