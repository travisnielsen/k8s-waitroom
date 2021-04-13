param location string
param nameprefix string


resource appsvc 'Microsoft.Web/serverfarms@2020-06-01' = {
  name: '${nameprefix}-serviceplan'
  location: resourceGroup().location
  sku: {
    name: 'S1'
  }
}

resource webapp 'Microsoft.Web/sites@2020-06-01' = {
  name: '${nameprefix}'
  location: resourceGroup().location
  kind: 'app'
  dependsOn: [
    appsvc
  ]
  properties: {
    serverFarmId: appsvc.id
  }
}
