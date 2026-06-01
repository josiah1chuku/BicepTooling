namespace BicepTooling.CLI;

// Generates synthetic Bicep files that deliberately trigger the seven
// security rules that have zero or near-zero real-world examples.
// 50 variations per rule (different names, locations, values) are produced
// so the ML model learns the pattern rather than memorising one file.
public static class SyntheticDataGenerator
{
    private static readonly string[] Locations =
    [
        "eastus", "westus", "eastus2", "westus2",
        "northeurope", "westeurope", "southeastasia",
        "australiaeast", "uksouth", "japaneast",
    ];

    // Owner and Contributor GUIDs + name variants to vary SEC010 files
    private static readonly (string Guid, string Name)[] BroadRoles =
    [
        ("8e3af657-a8ff-443c-a75c-2fe8c4bcb635", "owner"),
        ("b24988ac-6180-42a0-ab88-20f7382dd24c", "contributor"),
        ("8e3af657-a8ff-443c-a75c-2fe8c4bcb635", "Owner"),
        ("b24988ac-6180-42a0-ab88-20f7382dd24c", "Contributor"),
    ];

    private static readonly string[] SecretParamNames =
    [
        "adminPassword", "dbPassword", "apiKey", "secretKey",
        "accessKey", "connectionString", "adminPwd", "servicePassword",
        "credentialToken", "primaryKey",
    ];

    private static readonly string[] SecretValues =
    [
        "P@ssw0rd123", "Admin1Pass!", "SecretKey2Az", "Password456!",
        "Access7Key8", "Conn9ection0Str", "S3cur3Pass!", "T0k3nValue9",
        "Cr3d3ntial5!", "Primary1Key2",
    ];

    private static readonly string[] WeakAdminNames =
    [
        "admin", "administrator", "root", "azureuser",
        "user", "guest", "test", "admin",
    ];

    private static readonly string[] StorageNames =
    [
        "storage", "blobstore", "datastore", "filestore",
        "archivestore", "backupstore", "logstore", "mediastore",
    ];

    private static readonly (string A, string B)[] LocationPairs =
    [
        ("eastus",       "westus"),
        ("eastus2",      "northeurope"),
        ("westus2",      "southeastasia"),
        ("uksouth",      "japaneast"),
        ("eastus",       "australiaeast"),
        ("westus",       "eastus2"),
        ("northeurope",  "westus2"),
        ("southeastasia","uksouth"),
    ];

    // ── ENTRY POINT ──────────────────────────────────────────
    public static void Generate(string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        ConsoleUI.Banner("Synthetic Data Generator");
        Console.WriteLine();
        ConsoleUI.Tip("  Generating 50 variations per zero/low-positive rule.");
        Console.WriteLine();

        int total = 0;
        total += GenerateRule("SEC003", outputDir, 50, GenerateSec003);
        total += GenerateRule("SEC010", outputDir, 50, GenerateSec010);
        total += GenerateRule("SEC013", outputDir, 50, GenerateSec013);
        total += GenerateRule("SEC015", outputDir, 50, GenerateSec015);
        total += GenerateRule("SEC020", outputDir, 50, GenerateSec020);
        total += GenerateRule("SEC021", outputDir, 50, GenerateSec021);
        total += GenerateRule("SEC022", outputDir, 50, GenerateSec022);

        Console.WriteLine();
        ConsoleUI.Success($"✓  Generated {total} synthetic files → {outputDir}");
        Console.WriteLine();
        ConsoleUI.Tip($"  Now run:  dotnet run -- export {outputDir}");
    }

    private static int GenerateRule(string rule, string dir, int n, Func<int, string> gen)
    {
        for (int i = 0; i < n; i++)
        {
            var content  = gen(i);
            var filename = $"synth__{rule}_{i:D3}.bicep";
            File.WriteAllText(Path.Combine(dir, filename), content);
        }
        ConsoleUI.PassOk(rule, $"{n} files");
        return n;
    }

    private static string L(int i)  => Locations[i % Locations.Length];
    private static string SN(int i) => StorageNames[i % StorageNames.Length];

    // ── SEC003: public blob access enabled ───────────────────
    // Vary: storage name, location, sku tier
    private static string GenerateSec003(int i)
    {
        var skus = new[] { "Standard_LRS", "Standard_GRS", "Standard_ZRS", "Premium_LRS" };
        return $@"param location string = '{L(i)}'
resource {SN(i)}{i} 'Microsoft.Storage/storageAccounts@2023-01-01' = {{
  location: location
  sku: {{ name: '{skus[i % skus.Length]}' }}
  kind: 'StorageV2'
  properties: {{
    allowBlobPublicAccess: true
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }}
}}";
    }

    // ── SEC010: overly broad role assignment ─────────────────
    // Vary: role (owner/contributor), principal-like identifier, resource name
    private static string GenerateSec010(int i)
    {
        var (guid, roleName) = BroadRoles[i % BroadRoles.Length];
        var principalId = $"{i:D8}-abcd-{i % 9999:D4}-efgh-{i * 7 + 100000:D12}";
        return $@"param location string = '{L(i)}'
resource roleAssignment{i} 'Microsoft.Authorization/roleAssignments@2022-04-01' = {{
  location: location
  properties: {{
    roleDefinitionId: '{guid}'
    principalId: '{principalId}'
    principalType: 'ServicePrincipal'
    description: 'Assigns {roleName} to automation pipeline'
  }}
}}";
    }

    // ── SEC013: hardcoded secret as param default ────────────
    // Vary: param name, secret value, surrounding context
    private static string GenerateSec013(int i)
    {
        var paramName = SecretParamNames[i % SecretParamNames.Length];
        var secret    = SecretValues[i % SecretValues.Length];
        return $@"param location string = '{L(i)}'
param {paramName} string = '{secret}'
resource vm{i} 'Microsoft.Compute/virtualMachines@2023-03-01' = {{
  location: location
  properties: {{
    osProfile: {{
      adminUsername: 'dispatchAdmin{i}'
      {paramName}: {paramName}
    }}
    securityProfile: {{ encryptionAtHost: true }}
  }}
}}";
    }

    // ── SEC015: inconsistent hardcoded locations ──────────────
    // Vary: location pairs, resource types
    private static string GenerateSec015(int i)
    {
        var pair = LocationPairs[i % LocationPairs.Length];
        var (locA, locB) = (pair.A, pair.B);
        var storageTypes = new[] { "Standard_LRS", "Standard_GRS" };
        return $@"resource {SN(i)}{i} 'Microsoft.Storage/storageAccounts@2023-01-01' = {{
  location: '{locA}'
  sku: {{ name: '{storageTypes[i % 2]}' }}
  kind: 'StorageV2'
  properties: {{
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }}
}}
resource vnet{i} 'Microsoft.Network/virtualNetworks@2023-04-01' = {{
  location: '{locB}'
  properties: {{
    addressSpace: {{ addressPrefixes: ['10.{i % 256}.0.0/16'] }}
  }}
}}";
    }

    // ── SEC020: weak admin username ───────────────────────────
    // Vary: admin name, VM name, location
    private static string GenerateSec020(int i)
    {
        var weakAdmin = WeakAdminNames[i % WeakAdminNames.Length];
        return $@"param location string = '{L(i)}'
resource vm{i} 'Microsoft.Compute/virtualMachines@2023-03-01' = {{
  location: location
  properties: {{
    osProfile: {{
      adminUsername: '{weakAdmin}'
      computerName: 'vm-{i:D4}'
    }}
    securityProfile: {{ encryptionAtHost: true }}
    storageProfile: {{
      osDisk: {{ createOption: 'FromImage' }}
    }}
  }}
}}";
    }

    // ── SEC021: Linux password auth not disabled ──────────────
    // Vary: bool value (false / missing), VM name, location
    private static string GenerateSec021(int i)
    {
        // Alternate between explicit false and omitting the property entirely
        var linuxConfig = (i % 2 == 0)
            ? "linuxConfiguration: { disablePasswordAuthentication: false }"
            : "linuxConfiguration: { ssh: { publicKeys: [] } }";

        return $@"param location string = '{L(i)}'
resource vm{i} 'Microsoft.Compute/virtualMachines@2023-03-01' = {{
  location: location
  properties: {{
    osProfile: {{
      adminUsername: 'dispatchAdmin{i}'
      computerName: 'linux-vm-{i:D4}'
      {linuxConfig}
    }}
    securityProfile: {{ encryptionAtHost: true }}
  }}
}}";
    }

    // ── SEC022: SQL Database missing backup redundancy ────────
    // Vary: collation, edition, SQL server name
    private static string GenerateSec022(int i)
    {
        var collations = new[]
        {
            "SQL_Latin1_General_CP1_CI_AS",
            "Latin1_General_CI_AS",
            "SQL_Latin1_General_CP1_CS_AS",
            "French_CI_AS",
        };
        var editions = new[] { "Basic", "Standard", "Premium", "GeneralPurpose" };

        return $@"param location string = '{L(i)}'
resource sqlServer{i} 'Microsoft.Sql/servers@2023-02-01-preview' = {{
  location: location
  properties: {{
    administratorLogin: 'sqladmin{i}'
    minimalTlsVersion: '1.2'
  }}
}}
resource sqlDb{i} 'Microsoft.Sql/servers/databases@2023-02-01-preview' = {{
  location: location
  properties: {{
    collation: '{collations[i % collations.Length]}'
    edition: '{editions[i % editions.Length]}'
  }}
}}";
    }
}
