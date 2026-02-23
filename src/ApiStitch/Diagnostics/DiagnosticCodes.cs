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

    /// <summary>Warning: unsupported content type (only application/json supported in MMVP).</summary>
    public const string UnsupportedContentType = "AS404";

    /// <summary>Warning: unsupported query parameter style (only explode: true supported).</summary>
    public const string UnsupportedQueryParameterStyle = "AS405";
}
