using ApiStitch.Diagnostics;

namespace ApiStitch.Model;

/// <summary>
/// Represents a single API operation (an HTTP method + path combination).
/// </summary>
public class ApiOperation
{
    /// <summary>The operationId from the OpenAPI spec, or a derived identifier.</summary>
    public required string OperationId { get; init; }

    /// <summary>Relative URL path (without leading slash).</summary>
    public required string Path { get; init; }

    /// <summary>HTTP method for this operation.</summary>
    public required ApiHttpMethod HttpMethod { get; init; }

    /// <summary>Tag used for grouping operations into client classes.</summary>
    public required string Tag { get; init; }

    /// <summary>Generated C# method name (PascalCase + Async suffix).</summary>
    public required string CSharpMethodName { get; init; }

    /// <summary>Parameters for this operation (path, query, header).</summary>
    public IReadOnlyList<ApiParameter> Parameters { get; init; } = [];

    /// <summary>Request body, if any.</summary>
    public ApiRequestBody? RequestBody { get; init; }

    /// <summary>Success response (lowest 2xx with body, or 204).</summary>
    public ApiResponse? SuccessResponse { get; init; }

    /// <summary>Whether this operation is marked as deprecated in the spec.</summary>
    public bool IsDeprecated { get; init; }

    /// <summary>Optional description from the OpenAPI spec.</summary>
    public string? Description { get; init; }

    /// <summary>Diagnostics collected during transformation of this operation.</summary>
    public List<Diagnostic> Diagnostics { get; } = [];

    /// <summary>Whether this operation signals ProblemDetails support via non-success response contracts.</summary>
    public bool HasProblemDetailsSupport { get; init; }
}
