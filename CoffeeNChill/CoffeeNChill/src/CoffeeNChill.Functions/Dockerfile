# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY CoffeeNChill.Functions.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /home/site/wwwroot

# ---------- runtime (official Azure Functions isolated base image) ----------
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated \
    SimulateLifecycle=true \
    LifecycleStepSeconds=5
COPY --from=build /home/site/wwwroot /home/site/wwwroot
EXPOSE 80
