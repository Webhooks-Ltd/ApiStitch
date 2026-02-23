namespace ApiStitch.Model;

/// <summary>
/// Represents a parameter on an API operation (path, query, or header).
/// </summary>
public class ApiParameter
{
    /// <summary>Original parameter name from the OpenAPI spec.</summary>
    public required string Name { get; init; }

    /// <summary>C# parameter name (camelCase).</summary>
    public required string CSharpName { get; init; }

    /// <summary>Where this parameter appears in the HTTP request.</summary>
    public required ParameterLocation Location { get; init; }

    /// <summary>Schema describing the parameter type.</summary>
    public required ApiSchema Schema { get; init; }

    /// <summary>Whether the parameter is required.</summary>
    public bool IsRequired { get; init; }

    /// <summary>Optional description from the OpenAPI spec.</summary>
    public string? Description { get; init; }
}
