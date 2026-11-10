# Re-tagged Azurite image published as <dockerhub_username>/coffeenchill-azurite:v1.0
FROM mcr.microsoft.com/azure-storage/azurite:latest
EXPOSE 10000 10001 10002
CMD ["azurite", "--blobHost", "0.0.0.0", "--queueHost", "0.0.0.0", "--tableHost", "0.0.0.0", "--location", "/data", "--loose", "--skipApiVersionCheck"]
