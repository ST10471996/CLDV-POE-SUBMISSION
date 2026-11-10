using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Functions;

/// <summary>Part 1 – CRUD for the MenuItems Azure Table.</summary>
public class MenuFunctions
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly TableClient _table;
    private readonly ILogger<MenuFunctions> _log;

    public MenuFunctions(TableServiceClient tables, ILogger<MenuFunctions> log)
    {
        _table = tables.GetTableClient(StorageNames.MenuTable);
        _table.CreateIfNotExists();
        _log = log;
    }

    [Function("CreateMenuItem")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "menu")] HttpRequest req)
    {
        CreateMenuItemRequest? body;
        try { body = await JsonSerializer.DeserializeAsync<CreateMenuItemRequest>(req.Body, Json); }
        catch (JsonException) { return new BadRequestObjectResult(new { error = "Invalid JSON body." }); }

        if (body is null || string.IsNullOrWhiteSpace(body.Category) || string.IsNullOrWhiteSpace(body.Sku)
            || string.IsNullOrWhiteSpace(body.Name) || body.Price < 0)
            return new BadRequestObjectResult(new { error = "Category, Sku, Name and a non-negative Price are required." });

        var entity = new MenuItemEntity
        {
            PartitionKey = body.Category.Trim(),
            RowKey = body.Sku.Trim().ToUpperInvariant(),
            Name = body.Name.Trim(),
            Description = body.Description?.Trim() ?? string.Empty,
            Price = body.Price,
            IsAvailable = body.IsAvailable
        };

        try { await _table.AddEntityAsync(entity); }
        catch (RequestFailedException ex) when (ex.Status == 409)
        { return new ConflictObjectResult(new { error = $"Item {entity.RowKey} already exists in {entity.PartitionKey}." }); }

        _log.LogInformation("Menu item {Sku} created", entity.RowKey);
        return new ObjectResult(MenuItemDto.From(entity)) { StatusCode = 201 };
    }

    [Function("GetAllMenuItems")]
    public async Task<IActionResult> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu")] HttpRequest req)
    {
        var items = new List<MenuItemDto>();
        await foreach (var e in _table.QueryAsync<MenuItemEntity>()) items.Add(MenuItemDto.From(e));
        return new OkObjectResult(items);
    }

    [Function("GetMenuItemsByCategory")]
    public async Task<IActionResult> GetByCategory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu/category/{category}")] HttpRequest req,
        string category)
    {
        var items = new List<MenuItemDto>();
        await foreach (var e in _table.QueryAsync<MenuItemEntity>(x => x.PartitionKey == category))
            items.Add(MenuItemDto.From(e));
        return new OkObjectResult(items);
    }

    [Function("UpdateMenuItem")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "menu/{category}/{id}")] HttpRequest req,
        string category, string id)
    {
        UpdateMenuItemRequest? body;
        try { body = await JsonSerializer.DeserializeAsync<UpdateMenuItemRequest>(req.Body, Json); }
        catch (JsonException) { return new BadRequestObjectResult(new { error = "Invalid JSON body." }); }
        if (body is null) return new BadRequestObjectResult(new { error = "Body required." });
        if (body.Price is < 0) return new BadRequestObjectResult(new { error = "Price cannot be negative." });

        MenuItemEntity entity;
        try { entity = (await _table.GetEntityAsync<MenuItemEntity>(category, id)).Value; }
        catch (RequestFailedException ex) when (ex.Status == 404)
        { return new NotFoundObjectResult(new { error = "Menu item not found." }); }

        if (body.Price.HasValue) entity.Price = body.Price.Value;
        if (body.IsAvailable.HasValue) entity.IsAvailable = body.IsAvailable.Value;
        if (!string.IsNullOrWhiteSpace(body.Name)) entity.Name = body.Name;
        if (body.Description is not null) entity.Description = body.Description;

        await _table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
        return new OkObjectResult(MenuItemDto.From(entity));
    }

    [Function("DeleteMenuItem")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "menu/{category}/{id}")] HttpRequest req,
        string category, string id)
    {
        var resp = await _table.DeleteEntityAsync(category, id);
        return resp.Status == 404 ? new NotFoundObjectResult(new { error = "Menu item not found." }) : new NoContentResult();
    }
}
