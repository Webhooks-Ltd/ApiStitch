namespace ApiStitch.Model;

/// <summary>
/// Per-property encoding metadata for multipart request bodies.
/// </summary>
public sealed class MultipartEncoding
{
    /// <summary>Content type for this multipart property (e.g. "application/json").</summary>
    public required string ContentType { get; init; }
}
