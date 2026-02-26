using System.Text;
using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.Model;
using Microsoft.OpenApi;

namespace ApiStitch.Parsing;

/// <summary>
/// Transforms OpenAPI path operations into <see cref="ApiOperation"/> instances.
/// </summary>
public class OperationTransformer
{
    private readonly IReadOnlyDictionary<IOpenApiSchema, ApiSchema> _schemaMap;
    private readonly ApiStitchConfig _config;
    private readonly List<Diagnostic> _diagnostics = [];
    private string _clientName = null!;

    private OperationTransformer(IReadOnlyDictionary<IOpenApiSchema, ApiSchema> schemaMap, ApiStitchConfig config)
    {
        _schemaMap = schemaMap;
        _config = config;
    }

    /// <summary>
    /// Transforms all operations in the OpenAPI document into <see cref="ApiOperation"/> instances.
    /// </summary>
    public static (IReadOnlyList<ApiOperation> Operations, string ClientName, IReadOnlyList<Diagnostic> Diagnostics) Transform(
        OpenApiDocument document,
        IReadOnlyDictionary<IOpenApiSchema, ApiSchema> schemaMap,
        ApiStitchConfig config)
    {
        var transformer = new OperationTransformer(schemaMap, config);
        return transformer.TransformAll(document);
    }

    private static IOpenApiSchema ResolveRef(IOpenApiSchema schema) => OpenApiSchemaHelpers.ResolveRef(schema);
    private static JsonSchemaType? GetBaseType(IOpenApiSchema schema) => OpenApiSchemaHelpers.GetBaseType(schema);
    private static bool IsNullable(IOpenApiSchema schema) => OpenApiSchemaHelpers.IsNullable(schema);

    private (IReadOnlyList<ApiOperation> Operations, string ClientName, IReadOnlyList<Diagnostic> Diagnostics) TransformAll(OpenApiDocument document)
    {
        _clientName = DeriveClientName(document);
        var operations = new List<ApiOperation>();

        if (document.Paths is not null)
        {
            foreach (var (path, pathItem) in document.Paths)
            {
                var relativePath = path.TrimStart('/');

                foreach (var (operationType, operation) in pathItem.Operations ?? [])
                {
                    var httpMethod = MapHttpMethod(operationType.Method);
                    if (httpMethod is null)
                    {
                        _diagnostics.Add(new Diagnostic(
                            DiagnosticSeverity.Warning,
                            DiagnosticCodes.UnsupportedHttpMethod,
                            $"Unsupported HTTP method '{operationType.Method}' on '/{relativePath}'. Operation skipped.",
                            $"#/paths/{relativePath}"));
                        continue;
                    }

                    var merged = MergeParameters(pathItem.Parameters, operation.Parameters);
                    var results = TransformOperation(relativePath, httpMethod.Value, operation, merged);
                    operations.AddRange(results);
                }
            }
        }

        DeduplicateMethodNames(operations);

        return (operations, _clientName, _diagnostics);
    }

    private string DeriveClientName(OpenApiDocument document)
    {
        if (!string.IsNullOrWhiteSpace(_config.ClientName))
            return _config.ClientName!;

        var title = document.Info?.Title;
        if (string.IsNullOrWhiteSpace(title))
            return "ApiClient";

        var pascal = NamingHelper.ToPascalCase(title!);
        var cleaned = CleanNonAlphanumeric(pascal);

        if (cleaned.Length == 0)
            return "ApiClient";

        if (!cleaned.EndsWith("Api", StringComparison.Ordinal))
            cleaned += "Api";

        return cleaned;
    }

    private List<ApiOperation> TransformOperation(
        string path,
        ApiHttpMethod httpMethod,
        OpenApiOperation operation,
        IReadOnlyList<IOpenApiParameter> mergedParameters)
    {
        var operationId = operation.OperationId;
        var specPath = $"#/paths/{path}/{httpMethod.ToString().ToLowerInvariant()}";

        if (string.IsNullOrWhiteSpace(operationId))
        {
            operationId = DeriveOperationId(httpMethod, path);
            _diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                DiagnosticCodes.MissingOperationId,
                $"Operation '{httpMethod.ToString().ToUpperInvariant()} /{path}' has no operationId. Derived name: '{operationId}'. Consider adding operationId to the spec.",
                specPath));
        }

        var methodName = ToMethodName(operationId);

        var (parameters, paramDiags) = TransformParameters(mergedParameters, specPath);
        if (paramDiags != null)
            _diagnostics.AddRange(paramDiags);

        var (requestBody, bodySkip) = TransformRequestBody(operation.RequestBody, specPath);
        if (bodySkip)
            return [];

        var (successResponse, responseSkip) = TransformSuccessResponse(operation.Responses, specPath);
        if (responseSkip)
            return [];

        var tags = operation.Tags?.Select(t => t.Name).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!).ToList()
                   ?? new List<string>();

        if (tags.Count == 0)
            tags = [_clientName];

        var results = new List<ApiOperation>();
        foreach (var tag in tags)
        {
            results.Add(new ApiOperation
            {
                OperationId = operationId,
                Path = path,
                HttpMethod = httpMethod,
                Tag = tag,
                CSharpMethodName = methodName,
                Parameters = parameters,
                RequestBody = requestBody,
                SuccessResponse = successResponse,
                IsDeprecated = operation.Deprecated,
                Description = operation.Description,
            });
        }

        return results;
    }

    private (IReadOnlyList<ApiParameter> Parameters, List<Diagnostic>? Diagnostics) TransformParameters(
        IReadOnlyList<IOpenApiParameter> parameters,
        string specPath)
    {
        var result = new List<ApiParameter>();
        var diagnostics = new List<Diagnostic>();

        foreach (var param in parameters)
        {
            if (param.In == Microsoft.OpenApi.ParameterLocation.Cookie)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    DiagnosticCodes.CookieParameterSkipped,
                    $"Cookie parameter '{param.Name}' skipped. Cookie parameters are not supported.",
                    specPath));
                continue;
            }

            var location = param.In switch
            {
                Microsoft.OpenApi.ParameterLocation.Path => Model.ParameterLocation.Path,
                Microsoft.OpenApi.ParameterLocation.Query => Model.ParameterLocation.Query,
                Microsoft.OpenApi.ParameterLocation.Header => Model.ParameterLocation.Header,
                _ => (Model.ParameterLocation?)null
            };

            if (location is null)
                continue;

            if (location == Model.ParameterLocation.Query && param.Schema is not null && GetBaseType(ResolveRef(param.Schema)) == JsonSchemaType.Array)
            {
                if (param.Explode == false)
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticSeverity.Warning,
                        DiagnosticCodes.UnsupportedQueryParameterStyle,
                        $"Query parameter '{param.Name}' uses non-explode style. Only explode: true is supported.",
                        specPath));
                    continue;
                }
            }

            var schema = ResolveParameterSchema(param.Schema, param.Name ?? "", specPath);
            if (schema is null)
                continue;

            var isRequired = location == Model.ParameterLocation.Path || param.Required;

            result.Add(new ApiParameter
            {
                Name = param.Name ?? "",
                CSharpName = ToCamelCase(NamingHelper.ToPascalCase(param.Name ?? "")),
                Location = location.Value,
                Schema = schema,
                IsRequired = isRequired,
                Description = param.Description,
            });
        }

        return (result, diagnostics.Count > 0 ? diagnostics : null);
    }

    private ApiSchema? ResolveParameterSchema(IOpenApiSchema? schema, string paramName, string specPath)
    {
        if (schema is null)
            return null;

        var resolved = ResolveRef(schema);
        if (_schemaMap.TryGetValue(resolved, out var mapped))
            return mapped;

        if (resolved.AllOf is { Count: > 0 })
        {
            _diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                DiagnosticCodes.UnsupportedInlineSchema,
                $"Parameter '{paramName}' uses an inline allOf schema. Only $ref and inline primitives are supported.",
                specPath));
            return null;
        }

        if (GetBaseType(resolved) == JsonSchemaType.Object && resolved.Properties is { Count: > 0 })
        {
            _diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                DiagnosticCodes.UnsupportedInlineSchema,
                $"Parameter '{paramName}' uses an inline complex object schema. Only $ref and inline primitives are supported.",
                specPath));
            return null;
        }

        if (GetBaseType(resolved) == JsonSchemaType.Array)
        {
            var itemSchema = resolved.Items != null ? ResolveParameterSchema(resolved.Items, paramName, specPath) : null;
            return new ApiSchema
            {
                Name = paramName,
                OriginalName = paramName,
                Kind = SchemaKind.Array,
                ArrayItemSchema = itemSchema,
                Source = specPath,
            };
        }

        var primitiveType = MapInlinePrimitive(resolved);
        return new ApiSchema
        {
            Name = paramName,
            OriginalName = paramName,
            Kind = SchemaKind.Primitive,
            PrimitiveType = primitiveType,
            IsNullable = IsNullable(resolved),
            Source = specPath,
        };
    }

    private (ApiRequestBody? Body, bool SkipOperation) TransformRequestBody(IOpenApiRequestBody? requestBody, string specPath)
    {
        if (requestBody is null)
            return (null, false);

        if (requestBody.Content is null || requestBody.Content.Count == 0)
        {
            if (requestBody.Required)
                return (null, true);
            return (null, false);
        }

        var jsonContent = requestBody.Content
            .FirstOrDefault(c => c.Key.Equals("application/json", StringComparison.OrdinalIgnoreCase));

        if (jsonContent.Value is null)
        {
            if (requestBody.Content.Count > 0)
            {
                var contentType = requestBody.Content.First().Key;
                _diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    DiagnosticCodes.UnsupportedContentType,
                    $"Request body content type '{contentType}' is not supported. Only application/json is supported.",
                    specPath));
            }

            if (requestBody.Required)
                return (null, true);

            return (null, false);
        }

        var schema = ResolveBodySchema(jsonContent.Value.Schema, specPath);
        if (schema is null)
        {
            _diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                DiagnosticCodes.UnsupportedInlineSchema,
                "Request body uses an inline complex schema. Only $ref and inline array of $ref are supported.",
                specPath));
            return (null, true);
        }

        return (new ApiRequestBody
        {
            Schema = schema,
            IsRequired = requestBody.Required,
            ContentType = "application/json",
        }, false);
    }

    private ApiSchema? ResolveBodySchema(IOpenApiSchema? schema, string specPath)
    {
        if (schema is null)
            return null;

        var resolved = ResolveRef(schema);
        if (_schemaMap.TryGetValue(resolved, out var mapped))
            return mapped;

        if (GetBaseType(resolved) == JsonSchemaType.Array && resolved.Items != null)
        {
            var resolvedItems = ResolveRef(resolved.Items);
            if (_schemaMap.TryGetValue(resolvedItems, out var itemMapped))
            {
                return new ApiSchema
                {
                    Name = $"{itemMapped.Name}List",
                    OriginalName = itemMapped.OriginalName,
                    Kind = SchemaKind.Array,
                    ArrayItemSchema = itemMapped,
                    Source = specPath,
                };
            }

            var itemPrimitive = MapInlinePrimitive(resolvedItems);
            if (itemPrimitive is not null)
            {
                var baseType = GetBaseType(resolvedItems);
                var typeName = baseType?.ToString().ToLowerInvariant() ?? "object";
                var primSchema = new ApiSchema
                {
                    Name = typeName,
                    OriginalName = typeName,
                    Kind = SchemaKind.Primitive,
                    PrimitiveType = itemPrimitive,
                    Source = specPath,
                };
                return new ApiSchema
                {
                    Name = "List",
                    OriginalName = "array",
                    Kind = SchemaKind.Array,
                    ArrayItemSchema = primSchema,
                    Source = specPath,
                };
            }
        }

        return null;
    }

    private (ApiResponse? Response, bool SkipOperation) TransformSuccessResponse(OpenApiResponses? responses, string specPath)
    {
        if (responses is null || responses.Count == 0)
            return (null, false);

        var successResponses = responses
            .Where(r => int.TryParse(r.Key, out var code) && code >= 200 && code < 300)
            .OrderBy(r => int.Parse(r.Key))
            .ToList();

        if (successResponses.Count == 0)
            return (null, false);

        foreach (var (statusCodeStr, response) in successResponses)
        {
            var statusCode = int.Parse(statusCodeStr);

            if (response.Content is null || response.Content.Count == 0)
            {
                return (new ApiResponse
                {
                    StatusCode = statusCode,
                    ContentType = string.Empty,
                    Schema = null,
                }, false);
            }

            var jsonContent = response.Content
                .FirstOrDefault(c => c.Key.Equals("application/json", StringComparison.OrdinalIgnoreCase));

            if (jsonContent.Value is null)
                continue;

            var schema = ResolveResponseSchema(jsonContent.Value.Schema, specPath);
            if (schema is null && jsonContent.Value.Schema is not null)
            {
                _diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    DiagnosticCodes.UnsupportedInlineSchema,
                    $"Response for status {statusCode} uses an inline complex schema. Only $ref and inline array of $ref are supported.",
                    specPath));
                return (null, true);
            }

            return (new ApiResponse
            {
                StatusCode = statusCode,
                ContentType = "application/json",
                Schema = schema,
            }, false);
        }

        var firstCode = int.Parse(successResponses.First().Key);
        return (new ApiResponse
        {
            StatusCode = firstCode,
            ContentType = string.Empty,
            Schema = null,
        }, false);
    }

    private ApiSchema? ResolveResponseSchema(IOpenApiSchema? schema, string specPath)
    {
        if (schema is null)
            return null;

        var resolved = ResolveRef(schema);
        if (_schemaMap.TryGetValue(resolved, out var mapped))
            return mapped;

        if (GetBaseType(resolved) == JsonSchemaType.Array && resolved.Items != null)
        {
            var resolvedItems = ResolveRef(resolved.Items);
            if (_schemaMap.TryGetValue(resolvedItems, out var itemMapped))
            {
                return new ApiSchema
                {
                    Name = $"{itemMapped.Name}List",
                    OriginalName = itemMapped.OriginalName,
                    Kind = SchemaKind.Array,
                    ArrayItemSchema = itemMapped,
                    Source = specPath,
                };
            }

            var itemPrimitive = MapInlinePrimitive(resolvedItems);
            if (itemPrimitive is not null)
            {
                var baseType = GetBaseType(resolvedItems);
                var typeName = baseType?.ToString().ToLowerInvariant() ?? "object";
                var primSchema = new ApiSchema
                {
                    Name = typeName,
                    OriginalName = typeName,
                    Kind = SchemaKind.Primitive,
                    PrimitiveType = itemPrimitive,
                    Source = specPath,
                };
                return new ApiSchema
                {
                    Name = "List",
                    OriginalName = "array",
                    Kind = SchemaKind.Array,
                    ArrayItemSchema = primSchema,
                    Source = specPath,
                };
            }
        }

        return null;
    }

    private void DeduplicateMethodNames(List<ApiOperation> operations)
    {
        var byTag = operations.GroupBy(o => o.Tag, StringComparer.Ordinal);

        foreach (var group in byTag)
        {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var op in group)
            {
                if (seen.TryGetValue(op.CSharpMethodName, out var count))
                {
                    var newName = $"{op.CSharpMethodName}{count + 1}";
                    _diagnostics.Add(new Diagnostic(
                        DiagnosticSeverity.Warning,
                        DiagnosticCodes.MethodNameCollision,
                        $"Method name '{op.CSharpMethodName}' collides within tag '{op.Tag}'. Renamed to '{newName}'.",
                        $"#/paths/{op.Path}"));

                    var mutable = op;
                    seen[op.CSharpMethodName] = count + 1;
                    var index = operations.IndexOf(op);
                    operations[index] = new ApiOperation
                    {
                        OperationId = op.OperationId,
                        Path = op.Path,
                        HttpMethod = op.HttpMethod,
                        Tag = op.Tag,
                        CSharpMethodName = newName,
                        Parameters = op.Parameters,
                        RequestBody = op.RequestBody,
                        SuccessResponse = op.SuccessResponse,
                        IsDeprecated = op.IsDeprecated,
                        Description = op.Description,
                    };
                }
                else
                {
                    seen[op.CSharpMethodName] = 1;
                }
            }
        }
    }

    private static IReadOnlyList<IOpenApiParameter> MergeParameters(
        IList<IOpenApiParameter>? pathLevel,
        IList<IOpenApiParameter>? operationLevel)
    {
        if (pathLevel is null or { Count: 0 })
            return operationLevel?.ToList() ?? [];

        if (operationLevel is null or { Count: 0 })
            return pathLevel.ToList();

        var opKeys = new HashSet<(string?, Microsoft.OpenApi.ParameterLocation?)>(
            operationLevel.Select(p => (p.Name, p.In)));

        var merged = new List<IOpenApiParameter>(operationLevel);
        foreach (var pathParam in pathLevel)
        {
            if (!opKeys.Contains((pathParam.Name, pathParam.In)))
                merged.Add(pathParam);
        }

        return merged;
    }

    private static string DeriveOperationId(ApiHttpMethod method, string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        sb.Append(method.ToString());

        foreach (var segment in segments)
        {
            if (segment.StartsWith('{') && segment.EndsWith('}'))
            {
                var paramName = segment[1..^1];
                sb.Append("By");
                sb.Append(NamingHelper.ToPascalCase(paramName));
            }
            else
            {
                sb.Append(NamingHelper.ToPascalCase(segment));
            }
        }

        return sb.ToString();
    }

    private static string ToMethodName(string operationId)
    {
        var pascal = NamingHelper.ToPascalCase(operationId);
        var cleaned = CleanNonAlphanumeric(pascal);

        if (cleaned.EndsWith("Async", StringComparison.Ordinal))
            return cleaned;

        return cleaned + "Async";
    }

    private static string ToCamelCase(string pascal)
    {
        if (string.IsNullOrEmpty(pascal))
            return pascal;

        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    private static string CleanNonAlphanumeric(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
        }
        return sb.ToString();
    }

    private static ApiHttpMethod? MapHttpMethod(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => ApiHttpMethod.Get,
            "POST" => ApiHttpMethod.Post,
            "PUT" => ApiHttpMethod.Put,
            "DELETE" => ApiHttpMethod.Delete,
            "PATCH" => ApiHttpMethod.Patch,
            "HEAD" => ApiHttpMethod.Head,
            "OPTIONS" => ApiHttpMethod.Options,
            _ => null,
        };
    }

    private static PrimitiveType? MapInlinePrimitive(IOpenApiSchema schema)
    {
        var baseType = GetBaseType(schema);

        if (baseType == JsonSchemaType.Object && schema.Properties is { Count: > 0 })
            return null;
        if (schema.AllOf is { Count: > 0 })
            return null;

        return (baseType, schema.Format) switch
        {
            (JsonSchemaType.String, "date-time") => PrimitiveType.DateTimeOffset,
            (JsonSchemaType.String, "date") => PrimitiveType.DateOnly,
            (JsonSchemaType.String, "time") => PrimitiveType.TimeOnly,
            (JsonSchemaType.String, "duration") => PrimitiveType.TimeSpan,
            (JsonSchemaType.String, "uuid") => PrimitiveType.Guid,
            (JsonSchemaType.String, "uri") => PrimitiveType.Uri,
            (JsonSchemaType.String, "byte") => PrimitiveType.ByteArray,
            (JsonSchemaType.String, "binary") => PrimitiveType.ByteArray,
            (JsonSchemaType.String, _) => PrimitiveType.String,
            (JsonSchemaType.Integer, "int64") => PrimitiveType.Int64,
            (JsonSchemaType.Integer, _) => PrimitiveType.Int32,
            (JsonSchemaType.Number, "float") => PrimitiveType.Float,
            (JsonSchemaType.Number, "decimal") => PrimitiveType.Decimal,
            (JsonSchemaType.Number, _) => PrimitiveType.Double,
            (JsonSchemaType.Boolean, _) => PrimitiveType.Bool,
            _ => PrimitiveType.String,
        };
    }
}
