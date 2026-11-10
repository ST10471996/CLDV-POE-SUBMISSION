using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

/// <summary>Asynchronous order workflow (order-processing-queue → ProcessOrderQueue → Orders table).</summary>
[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public class OrdersController : ProxyControllerBase
{
    private static readonly string[] Statuses = { "Received", "Preparing", "Ready", "Collected" };
    private readonly IFunctionsClient _fn;
    public OrdersController(IFunctionsClient fn) => _fn = fn;

    /// <summary>Place an order onto the queue (public). Returns 202 immediately.</summary>
    /// <response code="202">Order accepted and queued.</response>
    /// <response code="400">Invalid order.</response>
    [HttpPost("queue"), AllowAnonymous]
    [ProducesResponseType(202), ProducesResponseType(typeof(ErrorDto), 400)]
    public async Task<IActionResult> Queue([FromBody] PlaceOrderDto dto)
    { try { return await Relay(await _fn.SendJsonAsync(HttpMethod.Post, "api/orders/queue", dto)); } catch (HttpRequestException e) { return Downstream(e); } }

    /// <summary>Track a single order's status (public, so students can see when it is Ready).</summary>
    /// <param name="orderDate">yyyy-MM-dd partition.</param>
    /// <param name="orderId">Order id, e.g. ORD-2026-8801.</param>
    [HttpGet("{orderDate}/{orderId}"), AllowAnonymous]
    [ProducesResponseType(typeof(OrderDto), 200), ProducesResponseType(404)]
    public async Task<IActionResult> Get(string orderDate, string orderId)
    { try { return await Relay(await _fn.GetAsync($"api/orders/{Uri.EscapeDataString(orderDate)}/{Uri.EscapeDataString(orderId)}")); } catch (HttpRequestException e) { return Downstream(e); } }

    /// <summary>List all orders, optionally filtered by date (Admin/Staff).</summary>
    [HttpGet, Authorize(Roles = Roles.AdminOrStaff)]
    [ProducesResponseType(typeof(IEnumerable<OrderDto>), 200), ProducesResponseType(401), ProducesResponseType(403)]
    public async Task<IActionResult> List([FromQuery] string? date)
    {
        var path = string.IsNullOrWhiteSpace(date) ? "api/orders" : $"api/orders?date={Uri.EscapeDataString(date)}";
        try { return await Relay(await _fn.GetAsync(path)); } catch (HttpRequestException e) { return Downstream(e); }
    }

    /// <summary>Update an order's status (Admin/Staff): Received, Preparing, Ready or Collected.</summary>
    /// <response code="200">Updated.</response>
    /// <response code="400">Invalid status.</response>
    /// <response code="404">Order not found.</response>
    [HttpPut("status"), Authorize(Roles = Roles.AdminOrStaff)]
    [ProducesResponseType(typeof(OrderDto), 200), ProducesResponseType(400), ProducesResponseType(401), ProducesResponseType(403), ProducesResponseType(404)]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateOrderStatusDto dto)
    {
        if (!Statuses.Contains(dto.Status)) return BadRequest(new ErrorDto($"Status must be one of: {string.Join(", ", Statuses)}"));
        try { return await Relay(await _fn.SendJsonAsync(HttpMethod.Put, "api/orders/status", dto)); } catch (HttpRequestException e) { return Downstream(e); }
    }
}
