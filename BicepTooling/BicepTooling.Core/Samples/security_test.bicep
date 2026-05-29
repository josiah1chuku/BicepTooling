// ============================================================
// FILE: security_test.bicep
// WHAT: Test file covering all 10 security rules.
//       Run: dotnet run -- check Samples/security_test.bicep
//       Expected: SEC001, SEC002, SEC004, SEC006, SEC007,
//                 SEC008, SEC009, SEC010 should all fire.
// ============================================================

param location string = 'eastus'
param storageAccountName string
param keyVaultName string
param sqlServerName string
param sqlAdminPassword string  // SEC005 would fire if this were output
param vnetName string
param appServiceName string
param appServicePlanId string

// ── STORAGE: missing HTTPS + TLS (SEC001, SEC002) ──────────
resource badStorage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    // Missing: supportsHttpsTrafficOnly: true  → SEC001
    // Missing: minimumTlsVersion: 'TLS1_2'    → SEC002
  }
}

// ── KEY VAULT: missing soft delete (SEC006) ────────────────
resource badKeyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    // Missing: softDeleteRetentionInDays  → SEC006
    // Missing: enablePurgeProtection      → SEC006
  }
}

// ── SQL SERVER: missing TLS version (SEC007) ───────────────
resource badSqlServer 'Microsoft.Sql/servers@2023-02-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: 'sqladmin'
    administratorLoginPassword: sqlAdminPassword
    // Missing: minimalTlsVersion: '1.2'   → SEC007
    // Missing: publicNetworkAccess: 'Disabled'
  }
}

// ── VIRTUAL NETWORK: missing DDoS protection (SEC008) ──────
resource badVnet 'Microsoft.Network/virtualNetworks@2023-04-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: ['10.0.0.0/16']
    }
    // Missing: enableDdosProtection: true  → SEC008
    // Missing: ddosProtectionPlan
  }
}

// ── APP SERVICE: missing HTTPS only (SEC009) ───────────────
resource badApp 'Microsoft.Web/sites@2023-01-01' = {
  name: appServiceName
  location: location
  properties: {
    serverFarmId: appServicePlanId
    // Missing: httpsOnly: true             → SEC009
    siteConfig: {
      // Missing: minTlsVersion: '1.2'      → SEC009
    }
  }
}

// ── ROLE ASSIGNMENT: Owner role (SEC010) ───────────────────
resource badRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, 'owner-assignment')
  properties: {
    // Owner role GUID — overly broad for PS systems → SEC010
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '8e3af657-a8ff-443c-a75c-2fe8c4bcb635'
    )
    principalId: 'some-principal-id'
  }
}

// ── OUTPUT: sensitive name (SEC005) ────────────────────────
output storageConnectionString string = badStorage.id
// ^ SEC005 fires because name contains 'connectionstring'
