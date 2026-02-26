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

    public async Task GetOwnerAsync(int id, CancellationToken cancellationToken = default)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"owners/{Uri.EscapeDataString(id.ToString())}");
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PetStore.SharedModels.Owner>> ListOwnersAsync(CancellationToken cancellationToken = default)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"owners");
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<PetStore.SharedModels.Owner>>(
            _jsonOptions, cancellationToken).ConfigureAwait(false))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        string? body = null;
        if (response.Content.Headers.ContentLength is not 0)
        {
            using var reader = new StreamReader(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
            var buffer = new char[8192];
            var read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            body = new string(buffer, 0, read);
        }

        throw new ApiException(response.StatusCode, body, response.Headers);
    }
}
