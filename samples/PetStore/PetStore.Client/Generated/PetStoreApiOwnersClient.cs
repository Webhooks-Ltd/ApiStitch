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
internal sealed partial class PetStoreApiOwnersClient : IPetStoreApiOwnersClient
{
    private const string HttpClientName = "PetStoreApi";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions;

    public PetStoreApiOwnersClient(IHttpClientFactory httpClientFactory, PetStoreApiJsonOptions jsonOptions)
    {
        _httpClientFactory = httpClientFactory;
        _jsonOptions = jsonOptions.Options;
    }

    public async Task<PetStore.SharedModels.Owner> GetOwnerAsync(int id, CancellationToken cancellationToken = default)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"Owners/{Uri.EscapeDataString(id.ToString())}");
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
        return (await response.Content.ReadFromJsonAsync<PetStore.SharedModels.Owner>(
            _jsonOptions, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<IReadOnlyList<PetStore.SharedModels.Owner>> ListOwnersAsync(CancellationToken cancellationToken = default)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"Owners");
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
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<PetStore.SharedModels.Owner>>(
            _jsonOptions, cancellationToken).ConfigureAwait(false))!;
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
