#!/bin/bash

docker tag $1 registry.digitalocean.com/mpaige-container-registry/btt-server:latest
docker tag $1 registry.digitalocean.com/mpaige-container-registry/btt-server:v$2

docker image ls

docker push registry.digitalocean.com/mpaige-container-registry/btt-server:latest
docker push registry.digitalocean.com/mpaige-container-registry/btt-server:v$2
