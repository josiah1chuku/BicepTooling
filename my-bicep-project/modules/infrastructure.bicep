targetScope = 'resourceGroup'

param location string
param vmName string
param keyVaultName string
param adminUsername string

@secure()
param adminPassword string

// Constants for native Azure RBAC roles
var keyVaultSecretsUserRoleId = '46334584-17cd-408a-b2d7-74d157399b68'

// 1. Core Network Foundation
resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: 'vnet-enterprise-prod'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [ '10.0.0.0/16' ]
    }
    subnets: [
      {
        name: 'snet-compute'
        properties: {
          addressPrefix: '10.0.1.0/24'
        }
      }
    ]
  }
}

resource nic 'Microsoft.Network/networkInterfaces@2023-11-01' = {
  name: 'nic-${vmName}'
  location: location
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig1'
        properties: {
          privateIPAllocationMethod: 'Dynamic'
          subnet: {
            id: vnet.properties.subnets[0].id
          }
        }
      }
    ]
  }
}

// 2. Compute Host featuring a System-Assigned Managed Identity
resource vm 'Microsoft.Compute/virtualMachines@2024-03-01' = {
  name: vmName
  location: location
  identity: {
    type: 'SystemAssigned' // Direct registration in Microsoft Entra ID
  }
  properties: {
    hardwareProfile: {
      vmSize: 'Standard_B2s'
    }
    osProfile: {
      computerName: vmName
      adminUsername: adminUsername
      adminPassword: adminPassword
    }
    storageProfile: {
      imageReference: {
        publisher: 'Canonical'
        offer: '0001-com-ubuntu-server-jammy'
        sku: '22_04-lts-gen2'
        version: 'latest'
      }
      osDisk: {
        createOption: 'FromImage'
        managedDisk: {
          storageAccountType: 'Premium_LRS'
        }
      }
    }
    networkProfile: {
      networkInterfaces: [
        {
          id: nic.id
        }
      ]
    }
  }
}

// 3. Azure Key Vault set to modern RBAC authorization mode
  resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    
    // CHANGE THIS LINE FROM 'Enabled' TO 'Disabled'
    publicNetworkAccess: 'Disabled' 
    
    enabledForDeployment: false
    enabledForTemplateDeployment: false
  }

}

// 4. Role Assignment linking the VM's Entra Identity to the Key Vault
resource rbacBinding 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kv.id, vm.id, keyVaultSecretsUserRoleId)
  scope: kv
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: vm.identity.principalId // Dynamically outputs the Object ID from Entra ID
    principalType: 'ServicePrincipal'
  }
}
