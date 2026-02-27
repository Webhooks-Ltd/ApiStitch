#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PetStore.Client.Generated;

[GeneratedCode("ApiStitch", null)]
internal sealed partial class PetStoreApiPetsClient : IPetStoreApiPetsClient
{
    private const string HttpClientName = "PetStoreApi";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions;

    public PetStoreApiPetsClient(IHttpClientFactory httpClientFactory, PetStoreApiJsonOptions jsonOptions)
    {
        _httpClientFactory = httpClientFactory;
        _jsonOptions = jsonOptions.Options;
    }

    public async Task CreatePetAsync(CreatePetRequest body, CancellationToken cancellationToken = default)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"pets");
        request.Content = JsonContent.Create(body, mediaType: null, _jsonOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<FileResponse> DownloadPetPhotoAsync(int id, CancellationToken cancellationToken = default)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"pets/{Uri.EscapeDataString(id.ToString())}/photo");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
            return await FileResponse.CreateAsync(response, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    public async Task GetPetAsync(int id, CancellationToken cancellationToken = default)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"pets/{Uri.EscapeDataString(id.ToString())}");
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PetStore.SharedModels.Pet>> ListPetsAsync(CancellationToken cancellationToken = default)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"pets");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null
            && !mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            && !mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase))
        {
            using var errorStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var errorReader = new StreamReader(errorStream);
            var errorBuffer = new char[8192];
            var errorRead = await errorReader.ReadAsync(errorBuffer, cancellationToken).ConfigureAwait(false);
            var errorBody = new string(errorBuffer, 0, errorRead);
            throw new ApiException(
                response.StatusCode,
                $"Expected application/json response but received {mediaType}. Body: {errorBody}",
                response.Headers);
        }
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<PetStore.SharedModels.Pet>>(
            _jsonOptions, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<IReadOnlyList<PetStore.SharedModels.Pet>> SearchPetsAsync(PetStore.SharedModels.PetStatus? status = null, IReadOnlyList<string>? tags = null, CancellationToken cancellationToken = default)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var queryParams = new List<KeyValuePair<string, string?>>();
        if (status is not null)
            queryParams.Add(new KeyValuePair<string, string?>("status", status.Value.ToString()));
        if (tags is not null)
        {
            foreach (var item in tags)
            {
                queryParams.Add(new KeyValuePair<string, string?>("tags", item.ToString()));
            }
        }
        var queryString = BuildQueryString(queryParams);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"pets/search{queryString}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null
            && !mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            && !mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase))
        {
            using var errorStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var errorReader = new StreamReader(errorStream);
            var errorBuffer = new char[8192];
            var errorRead = await errorReader.ReadAsync(errorBuffer, cancellationToken).ConfigureAwait(false);
            var errorBody = new string(errorBuffer, 0, errorRead);
            throw new ApiException(
                response.StatusCode,
                $"Expected application/json response but received {mediaType}. Body: {errorBody}",
                response.Headers);
        }
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<PetStore.SharedModels.Pet>>(
            _jsonOptions, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task UploadPetPhotoAsync(int id, Stream photo, string photoFileName, Stream? thumbnail = null, string? thumbnailFileName = null, string? description = null, CancellationToken cancellationToken = default)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"pets/{Uri.EscapeDataString(id.ToString())}/photo");
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(photo), "photo", photoFileName);
        if (thumbnail is not null)
            content.Add(new StreamContent(thumbnail), "thumbnail", thumbnailFileName ?? "file");
        if (description is not null)
            content.Add(new StringContent(description.ToString()), "description");
        request.Content = content;
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildQueryString(List<KeyValuePair<string, string?>> parameters)
    {
        if (parameters.Count == 0)
            return string.Empty;

        var sb = new StringBuilder("?");
        for (var i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Value is null) continue;
            if (sb.Length > 1) sb.Append('&');
            sb.Append(Uri.EscapeDataString(parameters[i].Key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(parameters[i].Value!));
        }

        return sb.Length == 1 ? string.Empty : sb.ToString();
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        string? body = null;
        ProblemDetails? problem = null;

        if (response.Content.Headers.ContentLength is not 0)
        {
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            var buffer = new char[8192];
            var read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            body = new string(buffer, 0, read);

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is "application/problem+json" or "application/json")
            {
                try
                {
                    problem = JsonSerializer.Deserialize<ProblemDetails>(body, _jsonOptions);
                }
                catch (JsonException)
                {
                }
            }
        }

        throw new ApiException(response.StatusCode, body, response.Headers, problem);
    }
}
