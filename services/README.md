# Wait Room Demo Services

This page outlines the steps to locally build and test the services that are part of this demo and deploy them to the AKS environment configured earlier. You must have recent versions of the following prerequisites installed and working in your development environment:

* [Docker Desktop](https://www.docker.com/products/docker-desktop)
* [Visual Studio Code](https://code.visualstudio.com/)
* [.NET 5 SDK](https://dotnet.microsoft.com/download/dotnet/5.0) - for local debugging
* [Node JS (v14 LTS)](https://nodejs.org/en/download/) - for local debugging

It is assumed all tools outlined in the [deployment section](../deployment/README.md) are installed and working.

## Debug Services Locally

TBD

## Build and Run Docker Containers

The proxy service requires environment variables when running inside a container. Create a new file called `env.txt` and populate it with the values that match your environment:

```shell
DATAPROTECTION_KEY_URI=https://[your_vault_name].vault.azure.net/keys/dataprotection/[your_key_id]
DATAPROTECTION_STORAGE_CONTAINER_URI=https://[your_storage_acct_name].blob.core.windows.net/proxyservice/keys.xml
AZURE_CLIENT_ID=[your_sp_client_id]
AZURE_TENANT_ID=[your_tenant_id]
AZURE_CLIENT_SECRET=[your_sp_secret]
RATE_LIMIT_ENABLED=false
SESSION_WINDOW_DURATION_SECS=60
SESSION_BLOCK_DURATION_SECS=60
MAX_NEW_SESSIONS_IN_WINDOW=3
HTML_FILENAME=waitroom.html
WAITROOM_RESPONSE_CODE=429
```

Use the following comamnds to build a container image and run locally. Be sure to replace `[docker_id]` with your own information.

Navigate to the `ProxyService` folder and run the following commands to build and run the code:

```bash
docker build -t [docker_id]/proxyservice:0.0.1 .
docker run -d -P --env-file env.txt [docker_id]/proxyservice:0.0.1
docker ps
```

Navigate to the `AuthService` folder and run the following:

```bash
docker build -t [docker_id]/authservice:0.0.1 .
docker run -d -P [docker_id]/authservice:0.0.1
docker ps
```

> NOTE: Docker will automatically assign the service a random port for your workstation. This is shown in the PORTS section of the `docker ps` command results.

You should now be able to test accessing the AuthService via the ProxyService via `https://localhost:5001:[port_assigned_by_docker]`

## Publish Docker Images

You can optionally publish these Docker images to docker.io by running the following commands:

```bash
docker push [docker_id]/proxyservice:0.0.1
docker push [docker_id]/authservice:0.0.1
```

## Deploy to Kubernetes

Both services are deployed to Kubernetes using manifest files. Modify [services.yaml](../deployment/services.yaml) hosted in the `deployment` folder to fit any environment requirements you may have. Most aspects of the Virtual Wait Room are controlled via the environment variables witin the `proxy-service`. Pay attention to those values and adjust them as necessary. Additionally, you may need to update the container image locations. Once you have the contents of this file updated, deploy using the following command:

```bash
kubectl apply -f services.yaml
```

Finally, deploy the Istio ingress configuration:

```bash
kubectl apply -f ingress.yaml
```

You should now be able to see a response when nativating to the demo cluster public URL documented during the [cluster infrastructure setup](../deployment/README.md). Example: `http://[your_name].centralus.cloudapp.azure.com/auth/`
