using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

/// <summary>Relays the downstream Functions response (status code + body) back to the caller.</summary>
public abstract class ProxyControllerBase : ControllerBase
{
    protected async Task<IActionResult> Relay(HttpResponseMessage resp)
    {
        var status = (int)resp.StatusCode;
        if (status == 204) return NoContent();
        var body = await resp.Content.ReadAsStringAsync();
        return new ContentResult
        {
            StatusCode = status,
            Content = body,
            ContentType = resp.Content.Headers.ContentType?.ToString() ?? "application/json"
        };
    }

    protected IActionResult Downstream(Exception ex) =>
        StatusCode(502, new Dtos.ErrorDto($"Functions host unavailable: {ex.Message}"));
}
