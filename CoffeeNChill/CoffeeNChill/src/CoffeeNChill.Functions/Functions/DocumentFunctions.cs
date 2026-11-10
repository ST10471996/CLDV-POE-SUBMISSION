using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using CoffeeNChill.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Functions;

/// <summary>Part 1 – staff operational documents on the staff-docs Azure File Share.</summary>
public class DocumentFunctions
{
    private readonly ShareDirectoryClient _root;
    private readonly ILogger<DocumentFunctions> _log;
    private static readonly FileExtensionContentTypeProvider Mime = new();

    public DocumentFunctions(ShareServiceClient shares, ILogger<DocumentFunctions> log)
    {
        var share = shares.GetShareClient(StorageNames.DocsShare);
        share.CreateIfNotExists();
        _root = share.GetRootDirectoryClient();
        _log = log;
    }

    [Function("UploadStaffDocument")]
    public async Task<IActionResult> Upload(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents/upload")] HttpRequest req)
    {
        if (!req.HasFormContentType)
            return new BadRequestObjectResult(new { error = "Use multipart/form-data with a 'file' field." });

        var form = await req.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
            return new BadRequestObjectResult(new { error = "No file supplied." });

        var fileName = Path.GetFileName(file.FileName);
        var client = _root.GetFileClient(fileName);
        await client.CreateAsync(file.Length);
        await using (var stream = file.OpenReadStream())
            await client.UploadAsync(stream); // streamed straight into the share

        _log.LogInformation("Uploaded {File} ({Size} bytes)", fileName, file.Length);
        return new ObjectResult(new StaffDocumentDto(fileName, file.Length, DateTimeOffset.UtcNow)) { StatusCode = 201 };
    }

    [Function("ListStaffDocuments")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents")] HttpRequest req)
    {
        var docs = new List<StaffDocumentDto>();
        await foreach (ShareFileItem item in _root.GetFilesAndDirectoriesAsync(
                           new ShareDirectoryGetFilesAndDirectoriesOptions { Traits = ShareFileTraits.Timestamps }))
        {
            if (item.IsDirectory) continue;
            docs.Add(new StaffDocumentDto(item.Name, item.FileSize ?? 0, item.Properties?.LastModified));
        }
        return new OkObjectResult(docs);
    }

    [Function("DownloadStaffDocument")]
    public async Task<IActionResult> Download(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents/download/{fileName}")] HttpRequest req,
        string fileName)
    {
        var client = _root.GetFileClient(Path.GetFileName(fileName));
        try
        {
            var download = await client.DownloadAsync();
            if (!Mime.TryGetContentType(fileName, out var type)) type = "application/octet-stream";
            return new FileStreamResult(download.Value.Content, type) { FileDownloadName = fileName };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { error = $"Document '{fileName}' not found." });
        }
    }

    [Function("DeleteStaffDocument")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "documents/{fileName}")] HttpRequest req,
        string fileName)
    {
        var resp = await _root.GetFileClient(Path.GetFileName(fileName)).DeleteIfExistsAsync();
        return resp.Value ? new NoContentResult() : new NotFoundObjectResult(new { error = "Document not found." });
    }
}
