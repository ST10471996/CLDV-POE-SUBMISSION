using Azure;
using Azure.Data.Tables;

namespace CoffeeNChill.Functions.Models;

/// <summary>MenuItems table entity. PartitionKey = Category, RowKey = SKU.</summary>
public class MenuItemEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

public record CreateMenuItemRequest(string Category, string Sku, string Name, string? Description, double Price, bool IsAvailable = true);
public record UpdateMenuItemRequest(double? Price, bool? IsAvailable, string? Name, string? Description);

public record MenuItemDto(string Category, string Sku, string Name, string Description, double Price, bool IsAvailable)
{
    public static MenuItemDto From(MenuItemEntity e) =>
        new(e.PartitionKey, e.RowKey, e.Name, e.Description, e.Price, e.IsAvailable);
}

public record StaffDocumentDto(string FileName, long SizeBytes, DateTimeOffset? LastModified);

/// <summary>Queue message contract (order-processing-queue).</summary>
public class OrderMessage
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<string> SelectedItemSKUs { get; set; } = new();
    public double TotalPrice { get; set; }
    public DateTime OrderTimestamp { get; set; }
}

/// <summary>Orders table entity. PartitionKey = OrderDate (yyyy-MM-dd), RowKey = OrderId.</summary>
public class OrderEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;
    public string CustomerName { get; set; } = string.Empty;
    public string SelectedItemSKUs { get; set; } = string.Empty; // comma separated
    public double TotalPrice { get; set; }
    public DateTime OrderTimestamp { get; set; }
    public string Status { get; set; } = OrderStatus.Received;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

public record OrderDto(string OrderDate, string OrderId, string CustomerName, string[] SelectedItemSKUs,
    double TotalPrice, DateTime OrderTimestamp, string Status)
{
    public static OrderDto From(OrderEntity e) => new(e.PartitionKey, e.RowKey, e.CustomerName,
        e.SelectedItemSKUs.Split(',', StringSplitOptions.RemoveEmptyEntries), e.TotalPrice, e.OrderTimestamp, e.Status);
}

public record UpdateOrderStatusRequest(string OrderDate, string OrderId, string Status);

public static class OrderStatus
{
    public const string Received = "Received";
    public const string Preparing = "Preparing";
    public const string Ready = "Ready";
    public const string Collected = "Collected";
    public static readonly string[] Lifecycle = { Received, Preparing, Ready, Collected };
    public static bool IsValid(string s) => Lifecycle.Contains(s);
}

public static class StorageNames
{
    public const string MenuTable = "MenuItems";
    public const string OrdersTable = "Orders";
    public const string DocsShare = "staff-docs";
    public const string OrderQueue = "order-processing-queue";
}
