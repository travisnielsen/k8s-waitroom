# Deployment

This page outlines the steps to deploy the solution to Azure. You must have the following prerequisites installed and working in your development environment:

* [Azure Bicep](https://github.com/Azure/bicep/releases)
* [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
* [kubectl](https://kubernetes.io/docs/tasks/tools/) (version 1.19+)
* [istioctl](https://docs.microsoft.com/en-us/azure/aks/servicemesh-istio-install?pivots=client-operating-system-linux)

## AKS Cluster

Run the following commands to deploy the cluster.

```bash
bicep build deploy-aks.bicep
az login
az group create --name waitroom-demo --location centralus
az deployment group create --resource-group waitroom-demo --template-file deploy-aks.json --parameters dnsPrefix={your_prefix_here}
```

Next, download connection information so that you can connect with the cluster via `kubectl`:

```bash
az aks get-credentials --resource-group waitroom-demo --name waitroom
```

## Istio

Ensure you can communicate with the Run the following comamnds

```bash
istioctl operator init

# confirm success
kubectl get all -n istio-operator

kubectl create ns istio-system
kubectl apply -f istio.aks.yaml

#confirm success
kubectl get all -n istio-system
```

## Services and Ingress Configuration

