#!/bin/bash

# Build in release mode and put the binaries in a dotnet runtime image.

export DOTNET_RUNNING_IN_CONTAINER=1
dotnet publish -f net5.0 -c Release

docker build -t btt-server -f Dockerfile-release .
