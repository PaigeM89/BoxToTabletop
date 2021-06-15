#!/bin/bash

docker tag $1 registry.digitalocean.com/mpaige-container-registry/btt-client:latest
docker tag $1 registry.digitalocean.com/mpaige-container-registry/btt-client:v$2

docker image ls

docker push registry.digitalocean.com/mpaige-container-registry/btt-client:latest
docker push registry.digitalocean.com/mpaige-container-registry/btt-client:v$2
