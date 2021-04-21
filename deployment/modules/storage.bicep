param accountName string
param containerName string
param tags object

resource storageAccount 'Microsoft.Storage/storageAccounts@2019-06-01' = {
  name: accountName
  location: resourceGroup().location
  sku: {
    name: 'Standard_LRS'
    tier: 'Standard'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    accessTier: 'Hot'
  }
  tags: tags
}

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2019-06-01' = {
  name: '${storageAccount.name}/default/${containerName}'
}

resource roleAssignmentName 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(storageAccount.id, 'ba92f5b4-2d11-453d-a403-e96b0029c9fe', dataFactory.name)
  dependsOn: [
    storageAccount
  ]
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: reference(dataFactory.id, '2018-06-01', 'full').identity.principalId
  }
  scope: storageAccount
}

output id string = storageAccount.id
output name string = storageAccount.name
output apiVersion string = storageAccount.apiVersion
