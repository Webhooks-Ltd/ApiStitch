namespace ApiStitch.Diagnostics;

/// <summary>
/// Diagnostic codes emitted during operation transformation (AS400 series).
/// </summary>
public static class DiagnosticCodes
{
    /// <summary>Warning: operation is missing operationId, method name was derived from HTTP method + path.</summary>
    public const string MissingOperationId = "AS400";

    /// <summary>Warning: unsupported inline schema in operation parameter or response (only $ref and inline primitives supported).</summary>
    public const string UnsupportedInlineSchema = "AS401";

    /// <summary>Warning: cookie parameter skipped (not supported in MMVP).</summary>
    public const string CookieParameterSkipped = "AS402";

    /// <summary>Warning: method name collision within a tag, deduplicated with numeric suffix.</summary>
    public const string MethodNameCollision = "AS403";

    /// <summary>Warning: unsupported content type (e.g. application/xml).</summary>
    public const string UnsupportedContentType = "AS404";

    // AS405 retired (was UnsupportedQueryParameterStyle, removed when explode:false support added)

    /// <summary>Warning: unsupported HTTP method, operation skipped.</summary>
    public const string UnsupportedHttpMethod = "AS406";

    /// <summary>Warning: unsupported parameter style/explode combination.</summary>
    public const string UnsupportedParameterStyleCombination = "AS407";

    /// <summary>Info: multipart encoding references unknown property name.</summary>
    public const string UnknownEncodingProperty = "AS408";

    /// <summary>Info: multiple content types available, one selected over others.</summary>
    public const string ContentTypeNegotiated = "AS409";

    /// <summary>Info: type reused from external assembly (not generated).</summary>
    public const string TypeReused = "AS500";

    /// <summary>Info: type excluded from reuse by configuration, will be generated.</summary>
    public const string TypeExcludedFromReuse = "AS501";
}
