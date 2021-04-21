// params
@description('The DNS prefix to use with hosted Kubernetes API server FQDN.')
param dnsPrefix string

@description('The name of the Managed Cluster resource.')
param clusterName string = 'waitroom'

@description('The object id of the service principal to be used for access to Key Vault and Blob Storage.')
param spObjectId string = 'waitroom'

param location string = resourceGroup().location

@minValue(1)
@maxValue(50)
@description('The number of nodes for the cluster. 1 Node is enough for Dev/Test and minimum 3 nodes, is recommended for Production')
param agentCount int = 2

@description('The size of the Virtual Machine.')
param agentVMSize string = 'Standard_D2_v3'

// vars
var kubernetesVersion = '1.19.7'
var subnetRef = '${vn.id}/subnets/${subnetName}'
var addressPrefix = '20.0.0.0/16'
var subnetName = 'aks'
var subnetPrefix = '20.0.0.0/24'
var virtualNetworkName = 'waitroom-demo'
var nodeResourceGroup = 'rg-${dnsPrefix}-${clusterName}'
var tags = {
  environment: 'production'
  projectCode: 'xyz'
}
var agentPoolName = 'agentpool01'

// Azure virtual network
resource vn 'Microsoft.Network/virtualNetworks@2020-06-01' = {
  name: virtualNetworkName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        addressPrefix
      ]
    }
    subnets: [
      {
        name: subnetName
        properties: {
          addressPrefix: subnetPrefix
        }
      }
    ]
  }
}

// Azure kubernetes service
resource aks 'Microsoft.ContainerService/managedClusters@2020-09-01' = {
  name: clusterName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    kubernetesVersion: kubernetesVersion
    enableRBAC: true
    dnsPrefix: dnsPrefix
    agentPoolProfiles: [
      {
        name: agentPoolName
        count: agentCount
        mode: 'System'
        vmSize: agentVMSize
        type: 'VirtualMachineScaleSets'
        osType: 'Linux'
        enableAutoScaling: false
        vnetSubnetID: subnetRef
      }
    ]
    servicePrincipalProfile: {
      clientId: 'msi'
    }
    nodeResourceGroup: nodeResourceGroup
    networkProfile: {
      networkPlugin: 'azure'
      loadBalancerSku: 'standard'
    }
  }
}


// Key Vault
module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    aadPrincipalObjectId: spObjectId
  }
}

// Storage
module storageAccount 'modules/keyvault.bicep' = {
  name: 'storage'
  params: {
    aadPrincipalObjectId: spObjectId
  }
}



output id string = aks.id
output apiServerAddress string = aks.properties.fqdn
