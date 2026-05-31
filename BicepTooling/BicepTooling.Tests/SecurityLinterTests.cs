namespace BicepTooling.Tests;

using BicepTooling.Lexer;
using BicepTooling.Parser;
using BicepTooling.Semantic;

public class SecurityLinterTests
{
    private static SecurityLinter Lint(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        var ast    = new Parser(tokens).ParseCompilationUnit();
        var linter = new SecurityLinter();
        linter.Lint(ast);
        return linter;
    }

    private static string Storage(string apiVersion, string body) =>
        $"resource sa 'Microsoft.Storage/storageAccounts@{apiVersion}' = {{ {body} }}";

    // ── SEC001 HTTPS-ONLY ─────────────────────────────────────

    [Fact]
    public void SEC001_StorageMissingHttpsOnly_ReportsWarning()
    {
        var linter = Lint(Storage("2023-01-01", "properties: { minimumTlsVersion: 'TLS1_2' }"));
        Assert.Contains(linter.Issues, d => d.Code == "SEC001");
    }

    [Fact]
    public void SEC001_StorageHasHttpsOnly_NoWarning()
    {
        var linter = Lint(Storage("2023-01-01",
            "location: 'eastus' sku: { name: 'Standard_LRS' } kind: 'StorageV2' " +
            "properties: { supportsHttpsTrafficOnly: true minimumTlsVersion: 'TLS1_2' }"));
        Assert.DoesNotContain(linter.Issues, d => d.Code == "SEC001");
    }

    // ── SEC002 MIN TLS ────────────────────────────────────────

    [Fact]
    public void SEC002_StorageMissingMinTls_ReportsWarning()
    {
        var linter = Lint(Storage("2023-01-01", "properties: { supportsHttpsTrafficOnly: true }"));
        Assert.Contains(linter.Issues, d => d.Code == "SEC002");
    }

    // ── SEC003 PUBLIC BLOB ACCESS ─────────────────────────────

    [Fact]
    public void SEC003_PublicBlobAccessTrue_ReportsError()
    {
        var linter = Lint(Storage("2023-01-01", "properties: { allowBlobPublicAccess: true }"));
        Assert.Contains(linter.Issues, d => d.Code == "SEC003");
    }

    [Fact]
    public void SEC003_PublicBlobAccessFalse_NoError()
    {
        var linter = Lint(Storage("2023-01-01", "properties: { allowBlobPublicAccess: false }"));
        Assert.DoesNotContain(linter.Issues, d => d.Code == "SEC003");
    }

    // ── SEC004 MISSING LOCATION ───────────────────────────────

    [Fact]
    public void SEC004_ResourceMissingLocation_ReportsWarning()
    {
        var linter = Lint(Storage("2023-01-01", ""));
        Assert.Contains(linter.Issues, d => d.Code == "SEC004");
    }

    [Fact]
    public void SEC004_ResourceHasLocation_NoWarning()
    {
        var linter = Lint(Storage("2023-01-01", "location: 'eastus'"));
        Assert.DoesNotContain(linter.Issues, d => d.Code == "SEC004");
    }

    // ── SEC005 SENSITIVE OUTPUT ───────────────────────────────

    [Fact]
    public void SEC005_OutputNamedKey_ReportsWarning()
    {
        var linter = Lint("output storageKey string = 'value'");
        Assert.Contains(linter.Issues, d => d.Code == "SEC005");
    }

    [Fact]
    public void SEC005_OutputNamedId_NoWarning()
    {
        var linter = Lint("output storageId string = 'value'");
        Assert.DoesNotContain(linter.Issues, d => d.Code == "SEC005");
    }

    // ── SEC006 KEY VAULT SOFT DELETE ──────────────────────────

    [Fact]
    public void SEC006_KeyVaultMissingSoftDelete_ReportsError()
    {
        var linter = Lint(
            "resource kv 'Microsoft.KeyVault/vaults@2023-02-01' = { properties: { } }");
        Assert.Contains(linter.Issues, d => d.Code == "SEC006");
    }

    [Fact]
    public void SEC006_KeyVaultHasSoftDelete_NoError()
    {
        var linter = Lint(
            "resource kv 'Microsoft.KeyVault/vaults@2023-02-01' = { " +
            "properties: { softDeleteRetentionInDays: 90 enablePurgeProtection: true } }");
        Assert.DoesNotContain(linter.Issues, d => d.Code == "SEC006");
    }

    // ── SEC007 SQL SERVER MIN TLS ─────────────────────────────

    [Fact]
    public void SEC007_SqlServerMissingTls_ReportsError()
    {
        var linter = Lint(
            "resource sql 'Microsoft.Sql/servers@2023-02-01-preview' = { properties: { } }");
        Assert.Contains(linter.Issues, d => d.Code == "SEC007");
    }

    // ── SEC008 VNET DDOS ──────────────────────────────────────

    [Fact]
    public void SEC008_VNetMissingDdos_ReportsWarning()
    {
        var linter = Lint(
            "resource vnet 'Microsoft.Network/virtualNetworks@2023-04-01' = { properties: { } }");
        Assert.Contains(linter.Issues, d => d.Code == "SEC008");
    }

    // ── SEC009 APP SERVICE HTTPS ──────────────────────────────

    [Fact]
    public void SEC009_AppServiceMissingHttps_ReportsError()
    {
        var linter = Lint(
            "resource app 'Microsoft.Web/sites@2023-01-01' = { properties: { } }");
        Assert.Contains(linter.Issues, d => d.Code == "SEC009");
    }

    // ── SEC010 ROLE ASSIGNMENT ────────────────────────────────

    [Fact]
    public void SEC010_OwnerRole_ReportsError()
    {
        var linter = Lint(
            "resource ra 'Microsoft.Authorization/roleAssignments@2022-04-01' = { " +
            "properties: { roleDefinitionId: '8e3af657-a8ff-443c-a75c-2fe8c4bcb635' } }");
        Assert.Contains(linter.Issues,
            d => d.Code == "SEC010" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void SEC010_ContributorRole_ReportsWarning()
    {
        var linter = Lint(
            "resource ra 'Microsoft.Authorization/roleAssignments@2022-04-01' = { " +
            "properties: { roleDefinitionId: 'b24988ac-6180-42a0-ab88-20f7382dd24c' } }");
        Assert.Contains(linter.Issues,
            d => d.Code == "SEC010" && d.Severity == DiagnosticSeverity.Warning);
    }

    // ── SEC011 STORAGE MISSING SKU ────────────────────────────

    [Fact]
    public void SEC011_StorageMissingSku_ReportsError()
    {
        var linter = Lint(Storage("2023-01-01", "location: 'eastus'"));
        Assert.Contains(linter.Issues, d => d.Code == "SEC011");
    }

    [Fact]
    public void SEC011_StorageHasSku_NoError()
    {
        var linter = Lint(Storage("2023-01-01",
            "location: 'eastus' sku: { name: 'Standard_LRS' } kind: 'StorageV2' " +
            "properties: { supportsHttpsTrafficOnly: true minimumTlsVersion: 'TLS1_2' }"));
        Assert.DoesNotContain(linter.Issues, d => d.Code == "SEC011");
    }

    // ── SEC012 STORAGE MISSING KIND ──────────────────────────

    [Fact]
    public void SEC012_StorageMissingKind_ReportsWarning()
    {
        var linter = Lint(Storage("2023-01-01",
            "location: 'eastus' sku: { name: 'Standard_LRS' }"));
        Assert.Contains(linter.Issues, d => d.Code == "SEC012");
    }

    [Fact]
    public void SEC012_StorageHasKind_NoWarning()
    {
        var linter = Lint(Storage("2023-01-01",
            "location: 'eastus' sku: { name: 'Standard_LRS' } kind: 'StorageV2' " +
            "properties: { supportsHttpsTrafficOnly: true minimumTlsVersion: 'TLS1_2' }"));
        Assert.DoesNotContain(linter.Issues, d => d.Code == "SEC012");
    }

    // ── SEC013 HARDCODED SECRETS ──────────────────────────────

    [Fact]
    public void SEC013_PasswordParamWithComplexDefault_ReportsError()
    {
        var linter = Lint("param adminPassword string = 'Passw0rd123'");
        Assert.Contains(linter.Issues, d => d.Code == "SEC013");
    }

    [Fact]
    public void SEC013_PasswordParamWithSimpleDefault_NoError()
    {
        // no uppercase — doesn't match complexity heuristic
        var linter = Lint("param adminPassword string = 'simplevalue'");
        Assert.DoesNotContain(linter.Issues, d => d.Code == "SEC013");
    }

    [Fact]
    public void SEC013_NonSecretNameWithComplexValue_NoError()
    {
        // param name 'location' is not in SecretParamNames
        var linter = Lint("param location string = 'EastUS1Region'");
        Assert.DoesNotContain(linter.Issues, d => d.Code == "SEC013");
    }

    // ── SEC014 DEPRECATED API VERSION ────────────────────────

    [Fact]
    public void SEC014_OldApiVersion_ReportsWarning()
    {
        var linter = Lint(Storage("2019-04-01", ""));
        Assert.Contains(linter.Issues, d => d.Code == "SEC014");
    }

    [Fact]
    public void SEC014_CurrentApiVersion_NoWarning()
    {
        var linter = Lint(Storage("2023-01-01", ""));
        Assert.DoesNotContain(linter.Issues, d => d.Code == "SEC014");
    }

    // ── SEC015 INCONSISTENT LOCATIONS ────────────────────────

    [Fact]
    public void SEC015_DifferentHardcodedLocations_ReportsWarning()
    {
        var source =
            "resource sa 'Microsoft.Storage/storageAccounts@2023-01-01' = { location: 'eastus' }\n" +
            "resource sql 'Microsoft.Sql/servers@2023-02-01-preview' = { location: 'westus' }";
        Assert.Contains(Lint(source).Issues, d => d.Code == "SEC015");
    }

    [Fact]
    public void SEC015_SameHardcodedLocation_NoWarning()
    {
        var source =
            "resource sa 'Microsoft.Storage/storageAccounts@2023-01-01' = { location: 'eastus' }\n" +
            "resource sql 'Microsoft.Sql/servers@2023-02-01-preview' = { location: 'eastus' }";
        Assert.DoesNotContain(Lint(source).Issues, d => d.Code == "SEC015");
    }

    // ── SEC017 NSG UNRESTRICTED RDP ───────────────────────────

    [Fact]
    public void SEC017_OpenRdpRule_ReportsError()
    {
        var source =
            "resource nsg 'Microsoft.Network/networkSecurityGroups@2023-04-01' = { " +
            "properties: { securityRules: [ { name: 'rdp' properties: { " +
            "direction: 'Inbound' access: 'Allow' " +
            "sourceAddressPrefix: '*' destinationPortRange: '3389' } } ] } }";
        Assert.Contains(Lint(source).Issues, d => d.Code == "SEC017");
    }

    [Fact]
    public void SEC017_RestrictedRdpRule_NoError()
    {
        var source =
            "resource nsg 'Microsoft.Network/networkSecurityGroups@2023-04-01' = { " +
            "properties: { securityRules: [ { name: 'rdp' properties: { " +
            "direction: 'Inbound' access: 'Allow' " +
            "sourceAddressPrefix: '10.0.0.0/8' destinationPortRange: '3389' } } ] } }";
        Assert.DoesNotContain(Lint(source).Issues, d => d.Code == "SEC017");
    }

    // ── SEC018 NSG UNRESTRICTED SSH ───────────────────────────

    [Fact]
    public void SEC018_OpenSshRule_ReportsError()
    {
        var source =
            "resource nsg 'Microsoft.Network/networkSecurityGroups@2023-04-01' = { " +
            "properties: { securityRules: [ { name: 'ssh' properties: { " +
            "direction: 'Inbound' access: 'Allow' " +
            "sourceAddressPrefix: 'Internet' destinationPortRange: '22' } } ] } }";
        Assert.Contains(Lint(source).Issues, d => d.Code == "SEC018");
    }

    [Fact]
    public void SEC018_OutboundSshRule_NoError()
    {
        var source =
            "resource nsg 'Microsoft.Network/networkSecurityGroups@2023-04-01' = { " +
            "properties: { securityRules: [ { name: 'ssh' properties: { " +
            "direction: 'Outbound' access: 'Allow' " +
            "sourceAddressPrefix: '*' destinationPortRange: '22' } } ] } }";
        Assert.DoesNotContain(Lint(source).Issues, d => d.Code == "SEC018");
    }

    // ── SEC019 VM MISSING HOST ENCRYPTION ────────────────────

    [Fact]
    public void SEC019_VmMissingEncryptionAtHost_ReportsWarning()
    {
        var source =
            "resource vm 'Microsoft.Compute/virtualMachines@2023-03-01' = { " +
            "properties: { osProfile: { adminUsername: 'dispatchAdmin' } } }";
        Assert.Contains(Lint(source).Issues, d => d.Code == "SEC019");
    }

    [Fact]
    public void SEC019_VmHasEncryptionAtHost_NoWarning()
    {
        var source =
            "resource vm 'Microsoft.Compute/virtualMachines@2023-03-01' = { " +
            "properties: { " +
            "securityProfile: { encryptionAtHost: true } " +
            "osProfile: { adminUsername: 'dispatchAdmin' } } }";
        Assert.DoesNotContain(Lint(source).Issues, d => d.Code == "SEC019");
    }

    // ── SEC020 VM WEAK ADMIN USERNAME ─────────────────────────

    [Fact]
    public void SEC020_WeakAdminUsername_ReportsError()
    {
        var source =
            "resource vm 'Microsoft.Compute/virtualMachines@2023-03-01' = { " +
            "properties: { osProfile: { adminUsername: 'admin' } } }";
        Assert.Contains(Lint(source).Issues, d => d.Code == "SEC020");
    }

    [Fact]
    public void SEC020_StrongAdminUsername_NoError()
    {
        var source =
            "resource vm 'Microsoft.Compute/virtualMachines@2023-03-01' = { " +
            "properties: { osProfile: { adminUsername: 'dispatchAdmin' } } }";
        Assert.DoesNotContain(Lint(source).Issues, d => d.Code == "SEC020");
    }

    // ── SEC021 VM LINUX PASSWORD AUTH ─────────────────────────

    [Fact]
    public void SEC021_PasswordAuthNotDisabled_ReportsError()
    {
        var source =
            "resource vm 'Microsoft.Compute/virtualMachines@2023-03-01' = { " +
            "properties: { osProfile: { adminUsername: 'dispatchAdmin' " +
            "linuxConfiguration: { disablePasswordAuthentication: false } } } }";
        Assert.Contains(Lint(source).Issues, d => d.Code == "SEC021");
    }

    [Fact]
    public void SEC021_PasswordAuthDisabled_NoError()
    {
        var source =
            "resource vm 'Microsoft.Compute/virtualMachines@2023-03-01' = { " +
            "properties: { osProfile: { adminUsername: 'dispatchAdmin' " +
            "linuxConfiguration: { disablePasswordAuthentication: true } } } }";
        Assert.DoesNotContain(Lint(source).Issues, d => d.Code == "SEC021");
    }

    // ── SEC022 SQL DATABASE BACKUP REDUNDANCY ─────────────────

    [Fact]
    public void SEC022_SqlDbMissingBackupRedundancy_ReportsWarning()
    {
        var source =
            "resource db 'Microsoft.Sql/servers/databases@2023-02-01-preview' = { " +
            "properties: { collation: 'SQL_Latin1_General_CP1_CI_AS' } }";
        Assert.Contains(Lint(source).Issues, d => d.Code == "SEC022");
    }

    [Fact]
    public void SEC022_SqlDbHasBackupRedundancy_NoWarning()
    {
        var source =
            "resource db 'Microsoft.Sql/servers/databases@2023-02-01-preview' = { " +
            "properties: { requestedBackupStorageRedundancy: 'Geo' } }";
        Assert.DoesNotContain(Lint(source).Issues, d => d.Code == "SEC022");
    }

    // ── SEC023 POSTGRESQL SSL ENFORCEMENT ────────────────────

    [Fact]
    public void SEC023_PostgreSqlMissingSsl_ReportsError()
    {
        var source =
            "resource pg 'Microsoft.DBforPostgreSQL/servers@2017-12-01' = { " +
            "properties: { administratorLogin: 'pgAdmin' } }";
        Assert.Contains(Lint(source).Issues, d => d.Code == "SEC023");
    }

    [Fact]
    public void SEC023_PostgreSqlSslEnabled_NoError()
    {
        var source =
            "resource pg 'Microsoft.DBforPostgreSQL/servers@2017-12-01' = { " +
            "properties: { sslEnforcement: 'Enabled' } }";
        Assert.DoesNotContain(Lint(source).Issues, d => d.Code == "SEC023");
    }

    [Fact]
    public void SEC023_MySqlMissingSsl_ReportsError()
    {
        var source =
            "resource db 'Microsoft.DBforMySQL/servers@2017-12-01' = { " +
            "properties: { administratorLogin: 'mysqlAdmin' } }";
        Assert.Contains(Lint(source).Issues, d => d.Code == "SEC023");
    }

    // ── SEC016 MIXED LOCATIONS ────────────────────────────────

    [Fact]
    public void SEC016_MixedHardcodedAndParamLocations_ReportsWarning()
    {
        var source =
            "param location string\n" +
            "resource sa 'Microsoft.Storage/storageAccounts@2023-01-01' = { location: 'eastus' }\n" +
            "resource sql 'Microsoft.Sql/servers@2023-02-01-preview' = { location: location }";
        Assert.Contains(Lint(source).Issues, d => d.Code == "SEC016");
    }

    [Fact]
    public void SEC016_AllHardcodedLocations_NoWarning()
    {
        var source =
            "resource sa 'Microsoft.Storage/storageAccounts@2023-01-01' = { location: 'eastus' }\n" +
            "resource sql 'Microsoft.Sql/servers@2023-02-01-preview' = { location: 'eastus' }";
        Assert.DoesNotContain(Lint(source).Issues, d => d.Code == "SEC016");
    }
}
