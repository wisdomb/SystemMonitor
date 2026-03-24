param namespaceName string
param location string
param queueNames array

resource namespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {}
}

resource queues 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = [for q in queueNames: {
  parent: namespace
  name: q
  properties: {
    lockDuration:              'PT5M'
    maxSizeInMegabytes:        1024
    requiresDuplicateDetection: false
    requiresSession:           false
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: true
    enablePartitioning:        false
    maxDeliveryCount:          10
  }
}]

resource authRule 'Microsoft.ServiceBus/namespaces/AuthorizationRules@2022-10-01-preview' existing = {
  parent: namespace
  name: 'RootManageSharedAccessKey'
}

output connectionString string = authRule.listKeys().primaryConnectionString
output namespaceName string = namespace.name
