using CoffeeNChill.Gateway.Dtos;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

/// <summary>Staff operational documents (staff-docs file share). Admin/Staff only.</summary>
[ApiController]
[Route("api/documents")]
[Authorize(Roles = Roles.AdminOrStaff)]
public class DocumentsController : ProxyControllerBase
{
    private readonly IFunctionsClient _fn;
    public DocumentsController(IFunctionsClient fn) => _fn = fn;

    /// <summary>Upload a document (multipart/form-data, field name "file").</summary>
    /// <response code="201">Uploaded.</response>
    /// <response code="400">No file.</response>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50_000_000)]
    [ProducesResponseType(typeof(StaffDocumentDto), 201)]
    [ProducesResponseType(401), ProducesResponseType(403)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest(new ErrorDto("No file supplied."));
        try { return await Relay(await _fn.UploadAsync("api/documents/upload", file)); } catch (HttpRequestException e) { return Downstream(e); }
    }

    /// <summary>List documents with name, size and last modified date.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StaffDocumentDto>), 200)]
    public async Task<IActionResult> List()
    { try { return await Relay(await _fn.GetAsync("api/documents")); } catch (HttpRequestException e) { return Downstream(e); } }

    /// <summary>Download a document by file name.</summary>
    /// <response code="200">File stream.</response>
    /// <response code="404">Not found.</response>
    [HttpGet("download/{fileName}")]
    [ProducesResponseType(typeof(FileResult), 200), ProducesResponseType(404)]
    public async Task<IActionResult> Download(string fileName)
    {
        try
        {
            var resp = await _fn.GetAsync($"api/documents/download/{Uri.EscapeDataString(fileName)}");
            if (!resp.IsSuccessStatusCode) return await Relay(resp);
            var type = resp.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            return File(await resp.Content.ReadAsStreamAsync(), type, fileName);
        }
        catch (HttpRequestException e) { return Downstream(e); }
    }

    /// <summary>Delete a document.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="404">Not found.</response>
    [HttpDelete("{fileName}")]
    [ProducesResponseType(204), ProducesResponseType(404)]
    public async Task<IActionResult> Delete(string fileName)
    { try { return await Relay(await _fn.DeleteAsync($"api/documents/{Uri.EscapeDataString(fileName)}")); } catch (HttpRequestException e) { return Downstream(e); } }
}
