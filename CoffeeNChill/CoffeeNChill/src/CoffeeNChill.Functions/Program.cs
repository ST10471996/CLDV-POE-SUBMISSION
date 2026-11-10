using Azure.Data.Tables;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// CoffeeNChill Azure Functions host (isolated worker, .NET 8)
// Part 1: Menu (Table) + Staff documents (File Share)
// Part 2: Order queue producer + queue-triggered processor (Queue + Orders Table)
// Part 3: Called by the ASP.NET Core gateway (order status updates, document delete)
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        var conn = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? "UseDevelopmentStorage=true";

        services.AddSingleton(new TableServiceClient(conn));
        // Azurite does not emulate Azure Files, so the staff-docs share can use a separate real account
        var filesConn = Environment.GetEnvironmentVariable("FilesStorage");
        services.AddSingleton(new ShareServiceClient(string.IsNullOrWhiteSpace(filesConn) ? conn : filesConn));
        // Base64 encoding matches what the QueueTrigger binding expects by default
        services.AddSingleton(new QueueServiceClient(conn, new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.Base64
        }));
    })
    .Build();

host.Run();
