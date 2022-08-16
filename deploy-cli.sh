#!/bin/bash
az acr login -n acaimageregistry
rg=azure-container-app-test
app=producer-containerapp-internal
tag=${DOCKER_TAG:-$(date +%s%3N)}
echo "Tag is: $tag"
baseimage=acaimageregistry.azurecr.io/producer
warmuphost=http://localhost:5080
echourl=https://producer-containerapp-internal.braveriver-d262f7a1.westeurope.azurecontainerapps.io/api/echo/ping

# Retag and push the image to the registry
docker tag baseapp:latest $baseimage:$tag
docker tag baseapp:latest $baseimage:latest
docker push $baseimage:$tag
docker push $baseimage:latest

echo "Get latest active revision name"
latest_revision=$(az containerapp show -n $app -g $rg --query properties.latestRevisionName -o tsv)

echo "Set multiple revision mode"
az containerapp revision set-mode -n $app -g $rg --mode multiple

echo "Redirect traffic to active revision $latest_revision"
az containerapp ingress traffic set -n $app -g $rg --revision-weight $latest_revision=100 &> /dev/null

echo "Create new revision from image $image"
az containerapp update -n $app -g $rg -i $image &> /dev/null

echo "Warmup new revision at $warmuphost/warmup/$app"
health_response_status=$(curl -m 180 --write-out "%{http_code}\n" -s $warmuphost/warmup/$app --output /dev/null)

if [ $health_response_status = "200" ]; then
    echo "Warmup complete, redirect traffic to new revision"
    az containerapp ingress traffic set -n $app -g $rg --revision-weight latest=100 &> /dev/null

    echo "Set single revision mode"
    az containerapp revision set-mode -n $app -g $rg --mode single
    exit 0
else
    echo "Warmup failed with status code $health_response_status"
    echo "Reset single revision mode"
    az containerapp revision set-mode -n $app -g $rg --mode single
    exit 1
fi