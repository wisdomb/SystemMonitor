param clusterName string
param location string
param nodeCount int = 3
param nodeVmSize string = 'Standard_D2s_v3'

resource aks 'Microsoft.ContainerService/managedClusters@2023-07-01' = {
  name: clusterName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    dnsPrefix: clusterName
    enableRBAC: true
    agentPoolProfiles: [
      {
        name: 'system'
        count: nodeCount
        vmSize: nodeVmSize
        osType: 'Linux'
        mode: 'System'
        enableAutoScaling: true
        minCount: 2
        maxCount: 10
        nodeTaints: []
      }
    ]
    networkProfile: {
      networkPlugin: 'azure'
      loadBalancerSku: 'standard'
    }
    addonProfiles: {
      azureKeyvaultSecretsProvider: {
        enabled: true
        config: {
          enableSecretRotation: 'true'
          rotationPollInterval: '2m'
        }
      }
      omsagent: {
        enabled: false  // use App Insights instead
      }
    }
  }
}

output clusterName  string = aks.name
output fqdn         string = aks.properties.fqdn
output principalId  string = aks.identity.principalId
