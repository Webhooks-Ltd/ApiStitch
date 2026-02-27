#nullable enable
using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PetStore.Client.Generated;

[GeneratedCode("ApiStitch", null)]
public sealed class FileResponse : IAsyncDisposable, IDisposable
{
    private readonly HttpResponseMessage _response;

    private FileResponse(HttpResponseMessage response, Stream content)
    {
        _response = response;
        Content = content;
        FileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        ContentType = response.Content.Headers.ContentType?.MediaType;
        ContentLength = response.Content.Headers.ContentLength;
    }

    internal static async Task<FileResponse> CreateAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return new FileResponse(response, stream);
    }

    public Stream Content { get; }
    public string? FileName { get; }
    public string? ContentType { get; }
    public long? ContentLength { get; }

    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync().ConfigureAwait(false);
        _response.Dispose();
    }

    public void Dispose()
    {
        Content.Dispose();
        _response.Dispose();
    }
}
