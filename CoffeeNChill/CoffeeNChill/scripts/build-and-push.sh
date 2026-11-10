#!/usr/bin/env bash
# Usage: ./scripts/build-and-push.sh <dockerhub_username> <tag e.g. v1.0|v2.0|v3.0>
set -euo pipefail
U=$1; T=${2:-v3.0}
docker build -t $U/coffeenchill-functions:$T src/CoffeeNChill.Functions
docker build -t $U/coffeenchill-gateway:$T  src/CoffeeNChill.Gateway
docker build -t $U/coffeenchill-azurite:$T  docker/azurite
docker push $U/coffeenchill-functions:$T
docker push $U/coffeenchill-gateway:$T
docker push $U/coffeenchill-azurite:$T
