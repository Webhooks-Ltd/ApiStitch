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

    /// <summary>Content type (e.g. "application/json").</summary>
    public required string ContentType { get; init; }
}
