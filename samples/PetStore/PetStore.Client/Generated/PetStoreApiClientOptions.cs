#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;

namespace PetStore.Client.Generated;

/// <summary>
/// Configuration options for the PetStoreApi HTTP client.
/// </summary>
[GeneratedCode("ApiStitch", null)]
public sealed class PetStoreApiClientOptions
{
    private Uri? _baseAddress;

    /// <summary>
    /// Base address for API requests. A trailing slash is enforced automatically.
    /// </summary>
    public Uri? BaseAddress
    {
        get => _baseAddress;
        set => _baseAddress = value is not null && !value.AbsoluteUri.EndsWith('/')
            ? new Uri(value.AbsoluteUri + "/")
            : value;
    }

    /// <summary>
    /// Request timeout. When null, the HttpClient default is used.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Default headers applied to every request.
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; } = [];
}
