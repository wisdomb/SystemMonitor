param name string
param location string
param aksObjectId string    // AKS managed identity — gets secret read access

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  properties: {
    sku: {
      family: 'A'
      name:   'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization:   true     // use Azure RBAC, not legacy access policies
    enableSoftDelete:          true
    softDeleteRetentionInDays: 90
    enablePurgeProtection:     true
    networkAcls: {
      defaultAction: 'Deny'
      bypass:        'AzureServices'
    }
  }
}

// Give AKS workload identity read access to secrets
resource secretsReaderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name:  guid(keyVault.id, aksObjectId, 'Key Vault Secrets User')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6'   // Key Vault Secrets User
    )
    principalId:   aksObjectId
    principalType: 'ServicePrincipal'
  }
}

// Secrets — values are populated post-deployment via az keyvault secret set
// or by a separate secrets management pipeline, never committed to git
resource cosmosKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name:   'CosmosDb--Key'
  properties: {
    value:      'PLACEHOLDER_SET_VIA_PIPELINE'
    attributes: { enabled: true }
  }
}

resource serviceBusSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name:   'ServiceBus--ConnectionString'
  properties: {
    value:      'PLACEHOLDER_SET_VIA_PIPELINE'
    attributes: { enabled: true }
  }
}

resource signalrSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name:   'AzureSignalR--ConnectionString'
  properties: {
    value:      'PLACEHOLDER_SET_VIA_PIPELINE'
    attributes: { enabled: true }
  }
}

resource openAiSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name:   'AzureOpenAI--ApiKey'
  properties: {
    value:      'PLACEHOLDER_SET_VIA_PIPELINE'
    attributes: { enabled: true }
  }
}

output keyVaultName string = keyVault.name
output keyVaultUri  string = keyVault.properties.vaultUri
