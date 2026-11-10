using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using CoffeeNChill.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Functions;

/// <summary>
/// Part 2 – asynchronous order workflow.
/// POST /api/orders/queue pushes a JSON message; ProcessOrderQueue consumes it,
/// writes it to the Orders table and walks it through Received → Preparing → Ready → Collected.
/// Part 3 – PUT /api/orders/status lets staff set the status manually through the gateway.
/// </summary>
public class OrderFunctions
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly QueueClient _queue;
    private readonly TableClient _orders;
    private readonly ILogger<OrderFunctions> _log;

    public OrderFunctions(QueueServiceClient queues, TableServiceClient tables, ILogger<OrderFunctions> log)
    {
        _queue = queues.GetQueueClient(StorageNames.OrderQueue);
        _queue.CreateIfNotExists();
        _orders = tables.GetTableClient(StorageNames.OrdersTable);
        _orders.CreateIfNotExists();
        _log = log;
    }

    [Function("QueueOrder")]
    public async Task<IActionResult> QueueOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders/queue")] HttpRequest req)
    {
        OrderMessage? order;
        try { order = await JsonSerializer.DeserializeAsync<OrderMessage>(req.Body, Json); }
        catch (JsonException) { return new BadRequestObjectResult(new { error = "Invalid JSON body." }); }

        if (order is null || string.IsNullOrWhiteSpace(order.CustomerName) || order.SelectedItemSKUs.Count == 0)
            return new BadRequestObjectResult(new { error = "CustomerName and at least one SelectedItemSKU are required." });
        if (order.TotalPrice < 0) return new BadRequestObjectResult(new { error = "TotalPrice cannot be negative." });

        if (string.IsNullOrWhiteSpace(order.OrderId))
            order.OrderId = $"ORD-{DateTime.UtcNow:yyyy}-{Random.Shared.Next(1000, 9999)}";
        if (order.OrderTimestamp == default) order.OrderTimestamp = DateTime.UtcNow;

        var message = JsonSerializer.Serialize(order);
        await _queue.SendMessageAsync(message);
        _log.LogInformation("Order {OrderId} queued", order.OrderId);

        // 202 Accepted: the client is not blocked while the barista prepares the order
        return new AcceptedResult($"/api/orders/{order.OrderTimestamp:yyyy-MM-dd}/{order.OrderId}",
            new { order.OrderId, OrderDate = order.OrderTimestamp.ToString("yyyy-MM-dd"), Status = "Queued" });
    }

    [Function("ProcessOrderQueue")]
    public async Task ProcessOrderQueue(
        [QueueTrigger(StorageNames.OrderQueue, Connection = "AzureWebJobsStorage")] string message)
    {
        var order = JsonSerializer.Deserialize<OrderMessage>(message, Json)
                    ?? throw new InvalidOperationException("Empty order message");

        var entity = new OrderEntity
        {
            PartitionKey = order.OrderTimestamp.ToString("yyyy-MM-dd"),
            RowKey = order.OrderId,
            CustomerName = order.CustomerName,
            SelectedItemSKUs = string.Join(',', order.SelectedItemSKUs),
            TotalPrice = order.TotalPrice,
            OrderTimestamp = DateTime.SpecifyKind(order.OrderTimestamp, DateTimeKind.Utc),
            Status = OrderStatus.Received
        };
        await _orders.UpsertEntityAsync(entity, TableUpdateMode.Replace);
        _log.LogInformation("Order {OrderId} -> {Status}", entity.RowKey, entity.Status);

        // Simulated barista lifecycle (can be switched off so staff drive it via PUT /api/orders/status)
        if (!bool.TryParse(Environment.GetEnvironmentVariable("SimulateLifecycle"), out var simulate) || !simulate) return;
        var delay = int.TryParse(Environment.GetEnvironmentVariable("LifecycleStepSeconds"), out var s) ? s : 5;

        foreach (var status in OrderStatus.Lifecycle.Skip(1))
        {
            await Task.Delay(TimeSpan.FromSeconds(delay));
            entity.Status = status;
            await _orders.UpsertEntityAsync(entity, TableUpdateMode.Merge);
            _log.LogInformation("Order {OrderId} -> {Status}", entity.RowKey, status);
        }
    }

    [Function("GetOrders")]
    public async Task<IActionResult> GetOrders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequest req)
    {
        string? date = req.Query["date"];
        var list = new List<OrderDto>();
        var query = string.IsNullOrWhiteSpace(date)
            ? _orders.QueryAsync<OrderEntity>()
            : _orders.QueryAsync<OrderEntity>(o => o.PartitionKey == date);
        await foreach (var o in query) list.Add(OrderDto.From(o));
        return new OkObjectResult(list.OrderByDescending(o => o.OrderTimestamp));
    }

    [Function("GetOrder")]
    public async Task<IActionResult> GetOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{orderDate}/{orderId}")] HttpRequest req,
        string orderDate, string orderId)
    {
        try { return new OkObjectResult(OrderDto.From((await _orders.GetEntityAsync<OrderEntity>(orderDate, orderId)).Value)); }
        catch (RequestFailedException ex) when (ex.Status == 404)
        { return new NotFoundObjectResult(new { error = "Order not found (it may still be in the queue)." }); }
    }

    [Function("UpdateOrderStatus")]
    public async Task<IActionResult> UpdateStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "orders/status")] HttpRequest req)
    {
        var body = await JsonSerializer.DeserializeAsync<UpdateOrderStatusRequest>(req.Body, Json);
        if (body is null || !OrderStatus.IsValid(body.Status))
            return new BadRequestObjectResult(new { error = $"Status must be one of: {string.Join(", ", OrderStatus.Lifecycle)}" });

        try
        {
            var entity = (await _orders.GetEntityAsync<OrderEntity>(body.OrderDate, body.OrderId)).Value;
            entity.Status = body.Status;
            await _orders.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Merge);
            return new OkObjectResult(OrderDto.From(entity));
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        { return new NotFoundObjectResult(new { error = "Order not found." }); }
    }
}
