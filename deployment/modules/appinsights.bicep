param name string
param logAnalyticsId string

resource appInsights 'Microsoft.Insights/components@2020-02-02-preview' = {
  name: name
  location: resourceGroup().location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsId
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

output id string = appInsights.id
output key string = appInsights.properties.InstrumentationKey
