namespace ApiStitch.Model;

/// <summary>
/// The top-level semantic model representing an entire API specification.
/// </summary>
public record class ApiSpecification
{
    /// <summary>All schemas (models, enums) from the spec.</summary>
    public required IReadOnlyList<ApiSchema> Schemas { get; init; }

    /// <summary>All parsed API operations.</summary>
    public IReadOnlyList<ApiOperation> Operations { get; init; } = [];

    /// <summary>Collection types used in operation request/response bodies (e.g. IReadOnlyList&lt;Pet&gt;).</summary>
    public IReadOnlyList<ApiSchema> CollectionTypes { get; init; } = [];

    /// <summary>Derived client name for this API (e.g. "PetStoreApi").</summary>
    public string? ClientName { get; init; }

    /// <summary>Metadata extracted from the spec's info section.</summary>
    public ApiSpecificationMetadata? Metadata { get; init; }
}

/// <summary>
/// Metadata from the OpenAPI info section.
/// </summary>
public class ApiSpecificationMetadata
{
    /// <summary>API title.</summary>
    public string? Title { get; init; }

    /// <summary>API version.</summary>
    public string? Version { get; init; }

    /// <summary>API description.</summary>
    public string? Description { get; init; }
}
