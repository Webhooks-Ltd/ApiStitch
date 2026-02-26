namespace ApiStitch.Configuration;

/// <summary>
/// Configuration for the ApiStitch code generation pipeline.
/// </summary>
public class ApiStitchConfig
{
    /// <summary>Path to the OpenAPI spec file.</summary>
    public required string Spec { get; init; }

    /// <summary>C# namespace for generated types.</summary>
    public string Namespace { get; init; } = "ApiStitch.Generated";

    /// <summary>Output directory for generated files.</summary>
    public string OutputDir { get; init; } = "./Generated";

    /// <summary>Output style for client code generation.</summary>
    public OutputStyle OutputStyle { get; init; } = OutputStyle.TypedClient;

    /// <summary>Optional client name override. When null, derived from the spec's info.title.</summary>
    public string? ClientName { get; init; }

    /// <summary>Configuration for type reuse via x-apistitch-type vendor extensions.</summary>
    public TypeReuseConfig TypeReuse { get; init; } = new();
}
