# Wait Room Demo Services

## Build, Run, and Publish with Docker

Use the following comamnds to build a container image and run locally. Be sure to replace [docker_id] with your own information.

```bash
docker build -t [docker_id]/sessionregulator:0.0.1 .
docker run -d -P [docker_id]/sessionregulator:0.0.1
docker ps
```

Docker will automatically assign the service a random port for your workstation. This is shown in the PORTS section of `docker ps`.

Finally, you can publish the container image with the following command:

```bash
docker push [docker_id]/sessionregulator:0.0.1
```
