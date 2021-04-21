param accountName string = '${uniqueString(resourceGroup().id)}' // must be globally unique
param containerName string

@description('The object id of the service principal to grant data contributor for Blob Storage.')
param aadObjectId string

// see: https://docs.microsoft.com/en-us/azure/role-based-access-control/built-in-roles
var roleDefinitions = {
  owner: '8e3af657-a8ff-443c-a75c-2fe8c4bcb635'
  contributor: 'b24988ac-6180-42a0-ab88-20f7382dd24c'
  reader: 'acdd72a7-3385-48ef-bd42-f606fba81ae7'
  blobDataContributor: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
}

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
}

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2019-06-01' = {
  name: '${storageAccount.name}/default/${containerName}'
}

resource roleAssignmentName 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(storageAccount.id, roleDefinitions.blobDataContributor, aadObjectId)
  dependsOn: [
    storageAccount
  ]
  properties: {
    roleDefinitionId: tenantResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitions.blobDataContributor)
    principalId: aadObjectId
    principalType: 'ServicePrincipal'
  }
  scope: storageAccount
}

output id string = storageAccount.id
output name string = storageAccount.name
output apiVersion string = storageAccount.apiVersion
