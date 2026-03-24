param accountName string
param location string
param databaseName string
param containerNames array

resource account 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: accountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [{
      locationName: location
      failoverPriority: 0
      isZoneRedundant: false
    }]
    capabilities: [{ name: 'EnableServerless' }]
    enableAutomaticFailover: false
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  parent: account
  name: databaseName
  properties: {
    resource: { id: databaseName }
  }
}

// Partition key varies by container — use /agentId for telemetry, /id for training
var partitionKeys = {
  metrics:      '/agentId'
  logs:         '/agentId'
  anomalies:    '/agentId'
  trainingData: '/id'
}

resource containers 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = [for c in containerNames: {
  parent: database
  name: c
  properties: {
    resource: {
      id: c
      partitionKey: {
        paths: [contains(partitionKeys, c) ? partitionKeys[c] : '/id']
        kind: 'Hash'
      }
      indexingPolicy: {
        automatic: true
        indexingMode: 'consistent'
        includedPaths: [{ path: '/*' }]
        excludedPaths: [{ path: '/"_etag"/?' }]
      }
      // TTL: metrics/logs auto-expire after 30 days to control costs
      defaultTtl: c == 'metrics' || c == 'logs' ? 2592000 : -1
    }
  }
}]

output endpoint    string = account.properties.documentEndpoint
output primaryKey  string = account.listKeys().primaryMasterKey
output accountName string = account.name
