using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace CoffeeNChill.Gateway.Services;

/// <summary>Thin proxy that forwards gateway calls to the Azure Functions host (Parts 1 &amp; 2).</summary>
public interface IFunctionsClient
{
    Task<HttpResponseMessage> GetAsync(string path);
    Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, object body);
    Task<HttpResponseMessage> DeleteAsync(string path);
    Task<HttpResponseMessage> UploadAsync(string path, IFormFile file);
}

public class FunctionsClient : IFunctionsClient
{
    private readonly HttpClient _http;
    public FunctionsClient(HttpClient http) => _http = http;

    public Task<HttpResponseMessage> GetAsync(string path) =>
        _http.GetAsync(path, HttpCompletionOption.ResponseHeadersRead);

    public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, object body) =>
        _http.SendAsync(new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) });

    public Task<HttpResponseMessage> DeleteAsync(string path) => _http.DeleteAsync(path);

    public async Task<HttpResponseMessage> UploadAsync(string path, IFormFile file)
    {
        using var form = new MultipartFormDataContent();
        var stream = new StreamContent(file.OpenReadStream());
        stream.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        form.Add(stream, "file", file.FileName);
        return await _http.PostAsync(path, form);
    }
}
