#!/usr/bin/env bash
# Part 1 demo – standalone containers, NO docker compose
set -euo pipefail
U=${1:?dockerhub username}
docker network create cnc-net 2>/dev/null || true
docker run -d --name azurite --network cnc-net -p 10000:10000 -p 10001:10001 -p 10002:10002 $U/coffeenchill-azurite:v1.0
docker run -d --name functions --network cnc-net -p 7071:80 \
  -e AzureWebJobsStorage="DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1;QueueEndpoint=http://azurite:10001/devstoreaccount1;TableEndpoint=http://azurite:10002/devstoreaccount1;" \
  -e FilesStorage="${FILES_CONNECTION:?set FILES_CONNECTION to an Azure Storage account connection string}" \
  $U/coffeenchill-functions:v1.0
echo "Functions on http://localhost:7071/api/menu"
