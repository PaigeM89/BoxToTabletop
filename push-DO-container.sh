#!/bin/bash

# pushes containers to my private digital ocean regsitry
docker tag $1 registry.digitalocean.com/mpaige-container-registry/$1
docker push registry.digitalocean.com/mpaige-container-registry/$1