targetScope = 'resourceGroup'

@description('Environment name - used as suffix on all resources')
param environment string = 'prod'

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Unique suffix to avoid naming collisions')
param suffix string = uniqueString(resourceGroup().id)

// ── Module: Service Bus ────────────────────────────────────────────────────────
module serviceBus 'modules/servicebus.bicep' = {
  name: 'servicebus'
  params: {
    namespaceName: 'sysmon-sb-${environment}-${suffix}'
    location: location
    queueNames: ['metrics-queue', 'logs-queue', 'training-queue']
  }
}

// ── Module: Cosmos DB ──────────────────────────────────────────────────────────
module cosmos 'modules/cosmos.bicep' = {
  name: 'cosmos'
  params: {
    accountName: 'sysmon-cosmos-${environment}-${suffix}'
    location: location
    databaseName: 'SystemMonitor'
    containerNames: ['metrics', 'logs', 'anomalies', 'trainingData']
  }
}

// ── Module: Storage (for ML model blobs) ──────────────────────────────────────
module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    accountName: 'sysmonstorage${suffix}'
    location: location
    containerNames: ['ai-models']
  }
}

// ── Module: Azure SignalR ──────────────────────────────────────────────────────
module signalr 'modules/signalr.bicep' = {
  name: 'signalr'
  params: {
    name: 'sysmon-signalr-${environment}-${suffix}'
    location: location
  }
}

// ── Module: Application Insights ──────────────────────────────────────────────
module appInsights 'modules/appinsights.bicep' = {
  name: 'appinsights'
  params: {
    name: 'sysmon-ai-${environment}-${suffix}'
    location: location
  }
}

// ── Module: AKS Cluster ────────────────────────────────────────────────────────
module aks 'modules/aks.bicep' = {
  name: 'aks'
  params: {
    clusterName: 'sysmon-aks-${environment}-${suffix}'
    location: location
    nodeCount: 3
    nodeVmSize: 'Standard_D2s_v3'
  }
}

// ── Module: Key Vault ─────────────────────────────────────────────────────────
module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    name:        'sysmon-kv-${environment}-${suffix}'
    location:    location
    aksObjectId: aks.outputs.principalId
  }
}

// ── Outputs (used by CI/CD to configure app secrets) ─────────────────────────
output serviceBusConnectionString string = serviceBus.outputs.connectionString
output cosmosEndpoint              string = cosmos.outputs.endpoint
output cosmosKey                   string = cosmos.outputs.primaryKey
output storageConnectionString     string = storage.outputs.connectionString
output signalrConnectionString     string = signalr.outputs.connectionString
output appInsightsConnectionString string = appInsights.outputs.connectionString
output aksClusterName              string = aks.outputs.clusterName
output aksResourceGroup            string = resourceGroup().name
