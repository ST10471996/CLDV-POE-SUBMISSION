using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

/// <summary>Digital menu (MenuItems table via Azure Functions). Reading is public; changes need Admin/Staff.</summary>
[ApiController]
[Route("api/menu")]
[Produces("application/json")]
public class MenuController : ProxyControllerBase
{
    private readonly IFunctionsClient _fn;
    public MenuController(IFunctionsClient fn) => _fn = fn;

    /// <summary>Get the full menu (public).</summary>
    /// <response code="200">All menu items.</response>
    [HttpGet, AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), 200)]
    public async Task<IActionResult> GetAll()
    { try { return await Relay(await _fn.GetAsync("api/menu")); } catch (HttpRequestException e) { return Downstream(e); } }

    /// <summary>Get menu items in one category, e.g. "Hot Drinks" (public).</summary>
    /// <param name="category">PartitionKey category.</param>
    [HttpGet("category/{category}"), AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), 200)]
    public async Task<IActionResult> GetByCategory(string category)
    { try { return await Relay(await _fn.GetAsync($"api/menu/category/{Uri.EscapeDataString(category)}")); } catch (HttpRequestException e) { return Downstream(e); } }

    /// <summary>Add a menu item (Admin/Staff).</summary>
    /// <response code="201">Created.</response>
    /// <response code="400">Invalid body.</response>
    /// <response code="401">No/invalid token.</response>
    /// <response code="403">Role not permitted.</response>
    /// <response code="409">SKU already exists.</response>
    [HttpPost, Authorize(Roles = Roles.AdminOrStaff)]
    [ProducesResponseType(typeof(MenuItemDto), 201)]
    [ProducesResponseType(401), ProducesResponseType(403)]
    public async Task<IActionResult> Create([FromBody] CreateMenuItemDto dto)
    { try { return await Relay(await _fn.SendJsonAsync(HttpMethod.Post, "api/menu", dto)); } catch (HttpRequestException e) { return Downstream(e); } }

    /// <summary>Update price or availability (Admin/Staff).</summary>
    /// <response code="200">Updated.</response>
    /// <response code="404">Item not found.</response>
    [HttpPut("{category}/{id}"), Authorize(Roles = Roles.AdminOrStaff)]
    [ProducesResponseType(typeof(MenuItemDto), 200)]
    [ProducesResponseType(401), ProducesResponseType(403), ProducesResponseType(404)]
    public async Task<IActionResult> Update(string category, string id, [FromBody] UpdateMenuItemDto dto)
    { try { return await Relay(await _fn.SendJsonAsync(HttpMethod.Put, $"api/menu/{Uri.EscapeDataString(category)}/{Uri.EscapeDataString(id)}", dto)); } catch (HttpRequestException e) { return Downstream(e); } }

    /// <summary>Remove a menu item (Admin/Staff).</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="404">Item not found.</response>
    [HttpDelete("{category}/{id}"), Authorize(Roles = Roles.AdminOrStaff)]
    [ProducesResponseType(204), ProducesResponseType(401), ProducesResponseType(403), ProducesResponseType(404)]
    public async Task<IActionResult> Delete(string category, string id)
    { try { return await Relay(await _fn.DeleteAsync($"api/menu/{Uri.EscapeDataString(category)}/{Uri.EscapeDataString(id)}")); } catch (HttpRequestException e) { return Downstream(e); } }
}
