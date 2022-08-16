#!/bin/bash
az acr login -n acaimageregistry
tag=$(date +%s%3N)
echo "Building image with tag: $tag"

export DOCKER_TAG=$tag

# Build docker image
echo "docker build -f Sample.Producer/Dockerfile -t baseapp --build-arg Version='0.1.2-$tag' ."
docker build -f Sample.Producer/Dockerfile -t baseapp --build-arg Version="0.1.2-$tag" .