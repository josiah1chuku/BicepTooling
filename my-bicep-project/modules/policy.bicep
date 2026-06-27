targetScope = 'subscription'

param policyDefinitionName string = 'deny-public-network-access-storage'
param policyAssignmentName string = 'assign-deny-public-storage'

// 1. Define the Custom Policy Rule
resource customPolicyDef 'Microsoft.Authorization/policyDefinitions@2021-06-01' = {
  name: policyDefinitionName
  properties: {
    displayName: 'Deny Storage Accounts with public network access enabled'
    policyType: 'Custom'
    mode: 'Indexed'
    description: 'This policy enforces cloud isolation by blocking the deployment of Storage Accounts that have public network access enabled.'
    metadata: {
      category: 'Storage'
      version: '1.0.0'
    }
    parameters: {
      effect: {
        type: 'String'
        metadata: {
          displayName: 'Effect'
          description: 'Enable or disable the execution of the policy'
        }
        allowedValues: [
          'Deny'
          'Audit'
          'Disabled'
        ]
        defaultValue: 'Deny'
      }
    }
    policyRule: {
      if: {
        allOf: [
          {
            field: 'type'
            equals: 'Microsoft.Storage/storageAccounts'
          }
          {
            field: 'Microsoft.Storage/storageAccounts/publicNetworkAccess'
            notEquals: 'Disabled'
          }
        ]
      }
      then: {
        effect: '[parameters(\'effect\')]'
      }
    }
  }
}

// 2. Assign the Custom Policy to the current Subscription scope
resource policyAssign 'Microsoft.Authorization/policyAssignments@2022-06-01' = {
  name: policyAssignmentName
  properties: {
    displayName: 'Enforce Private Storage Accounts Only'
    description: 'Assigned automatically via Bicep governance pipeline.'
    policyDefinitionId: customPolicyDef.id
    parameters: {
      effect: {
        value: 'Deny'
      }
    }
  }
}
