#!/bin/bash

# publish locally in release mode, then put the output into a docker container

yarn prod

docker build -t btt-client -f Dockerfile-release .