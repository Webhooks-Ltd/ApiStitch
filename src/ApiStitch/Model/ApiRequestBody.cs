namespace ApiStitch.Model;

/// <summary>
/// Represents the request body of an API operation.
/// </summary>
public class ApiRequestBody
{
    /// <summary>Schema describing the request body type.</summary>
    public required ApiSchema Schema { get; init; }

    /// <summary>Whether the request body is required.</summary>
    public bool IsRequired { get; init; }

    /// <summary>Content kind category for this request body.</summary>
    public required ContentKind ContentKind { get; init; }

    /// <summary>Original media type string (e.g. "application/json").</summary>
    public required string MediaType { get; init; }

    /// <summary>Per-property encoding metadata for multipart request bodies.</summary>
    public IReadOnlyDictionary<string, MultipartEncoding>? PropertyEncodings { get; init; }
}
