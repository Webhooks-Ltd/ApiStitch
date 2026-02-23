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

    /// <summary>Response content type (e.g. "application/json").</summary>
    public required string ContentType { get; init; }

    /// <summary>Whether this response has a body.</summary>
    public bool HasBody => Schema is not null;
}
