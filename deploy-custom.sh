#!/bin/bash
az acr login -n acaimageregistry

rg=azure-container-app-test
app=producer-containerapp-internal
baseimage=acaimageregistry.azurecr.io/producer
tag=${DOCKER_TAG:-$(date +%s%3N)}
host=localhost:5080
echo "Tag is: $tag"
url=http://$host/resource-groups/$rg/apps/$app/deploy

# Retag and push the image to the registry
docker tag baseapp:latest $baseimage:$tag
docker tag baseapp:latest $baseimage:latest
docker push $baseimage:$tag
docker push $baseimage:latest

echo "Starting deployment at: $url"
curl --get --data-urlencode imageName=$baseimage:$tag $url
# curl --silent --fail --get --data-encode imageName=$baseimage:$tag $url