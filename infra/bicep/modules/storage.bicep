param accountName string
param location string
param containerNames array

resource account 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: accountName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: account
  name: 'default'
}

resource containers 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = [for c in containerNames: {
  parent: blobService
  name: c
  properties: {
    publicAccess: 'None'
  }
}]

output connectionString string = 'DefaultEndpointsProtocol=https;AccountName=${account.name};AccountKey=${account.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
output accountName      string = account.name
