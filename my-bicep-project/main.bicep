targetScope = 'subscription'

param location string = 'centralindia'
param resourceGroupName string = 'rg-enterprise-prod'
param vmName string = 'vm-app-server'
param adminUsername string = 'azureuser'

@secure()
param adminPassword string

// 1. Establish the Target Resource Group Container
resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

// 2. Orchestrate Custom Governance Policies across the Subscription
module governancePolicy './modules/policy.bicep' = {
  name: 'deploy-governance-policy'
}

// 3. Orchestrate Enterprise Host Infrastructure inside the target Resource Group Scope
module infrastructure './modules/infrastructure.bicep' = {
  name: 'deploy-app-infrastructure'
  scope: rg // Declares cross-scope translation down to Resource Group context
  params: {
    location: location
    vmName: vmName
    adminUsername: adminUsername
    adminPassword: adminPassword
    // Shortened unique name generation ensuring total length remains under 24 characters
    keyVaultName: 'kvent${uniqueString(rg.id)}'
  }
}
