namespace ApiStitch.Configuration;

/// <summary>
/// Configuration for the ApiStitch code generation pipeline.
/// </summary>
public class ApiStitchConfig
{
    /// <summary>Path to the OpenAPI spec file. Mutually exclusive with <see cref="Project"/>.</summary>
    public string? Spec { get; init; }

    /// <summary>Path to a .csproj that produces an OpenAPI spec at build time. Mutually exclusive with <see cref="Spec"/>.</summary>
    public string? Project { get; init; }

    /// <summary>C# namespace for generated types.</summary>
    public string Namespace { get; init; } = "ApiStitch.Generated";

    /// <summary>Output directory for generated files.</summary>
    public string OutputDir { get; init; } = "./Generated";

    /// <summary>Output style for client code generation.</summary>
    public OutputStyle OutputStyle { get; init; } = OutputStyle.TypedClientStructured;

    /// <summary>Optional client name override. When null, derived from the spec's info.title.</summary>
    public string? ClientName { get; init; }

    /// <summary>Configuration for type reuse via x-apistitch-type vendor extensions.</summary>
    public TypeReuseConfig TypeReuse { get; init; } = new();
}
