param name string
param location string

resource signalr 'Microsoft.SignalRService/signalR@2023-02-01' = {
  name: name
  location: location
  sku: {
    name: 'Standard_S1'
    tier: 'Standard'
    capacity: 1
  }
  kind: 'SignalR'
  properties: {
    features: [
      { flag: 'ServiceMode', value: 'Default' }
      { flag: 'EnableConnectivityLogs', value: 'true' }
    ]
    cors: {
      allowedOrigins: ['*']   // tighten to dashboard URL in production
    }
  }
}

output connectionString string = signalr.listKeys().primaryConnectionString
output name             string = signalr.name
