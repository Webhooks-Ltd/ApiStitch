namespace ApiStitch.Model;

public class ApiSpecification
{
    public required IReadOnlyList<ApiSchema> Schemas { get; init; }
    public IReadOnlyList<ApiOperation> Operations { get; init; } = [];
    public ApiSpecificationMetadata? Metadata { get; init; }
}

public class ApiSpecificationMetadata
{
    public string? Title { get; init; }
    public string? Version { get; init; }
    public string? Description { get; init; }
}
