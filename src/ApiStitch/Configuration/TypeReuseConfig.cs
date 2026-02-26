namespace ApiStitch.Configuration;

/// <summary>
/// Configuration for type reuse via x-apistitch-type vendor extensions.
/// Types are only reused if they match an include pattern. Exclude patterns override includes.
/// If no include patterns are configured, all types are generated (no reuse).
/// </summary>
public class TypeReuseConfig
{
    /// <summary>Glob patterns for namespaces to include for reuse (e.g., "SampleApi.Models.*"). Required for any reuse to occur.</summary>
    public List<string> IncludeNamespaces { get; init; } = [];

    /// <summary>Exact fully-qualified type names to include for reuse.</summary>
    public List<string> IncludeTypes { get; init; } = [];

    /// <summary>Glob patterns for namespaces to exclude from reuse (overrides includes).</summary>
    public List<string> ExcludeNamespaces { get; init; } = [];

    /// <summary>Exact fully-qualified type names to exclude from reuse (overrides includes).</summary>
    public List<string> ExcludeTypes { get; init; } = [];

    /// <summary>Namespace prefix replacements applied to reused type names (e.g., "SampleApi.Models" -> "Consumer.SharedModels").</summary>
    public Dictionary<string, string> NamespaceMap { get; init; } = [];

    internal bool HasIncludeRules => IncludeNamespaces.Count > 0 || IncludeTypes.Count > 0;
}
