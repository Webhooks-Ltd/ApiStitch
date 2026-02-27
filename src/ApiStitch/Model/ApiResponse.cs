namespace ApiStitch.Model;

/// <summary>
/// Represents the success response of an API operation.
/// </summary>
public class ApiResponse
{
    /// <summary>Schema describing the response body type, or null for no-content responses.</summary>
    public ApiSchema? Schema { get; init; }

    /// <summary>HTTP status code (e.g. 200, 204).</summary>
    public required int StatusCode { get; init; }

    /// <summary>Content kind category for this response, or null for no-content responses.</summary>
    public ContentKind? ContentKind { get; init; }

    /// <summary>Original media type string (e.g. "application/json"), or null for no-content responses.</summary>
    public string? MediaType { get; init; }

    // Note: ContentKind and MediaType should be null when Schema is null (no body)
    /// <summary>Whether this response has a body.</summary>
    public bool HasBody => Schema is not null;
}
