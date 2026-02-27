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

    private static Model.ParameterStyle MapParameterStyle(Microsoft.OpenApi.ParameterStyle? openApiStyle, Model.ParameterLocation location)
    {
        if (openApiStyle is null)
        {
            return location switch
            {
                Model.ParameterLocation.Query => Model.ParameterStyle.Form,
                Model.ParameterLocation.Path => Model.ParameterStyle.Simple,
                Model.ParameterLocation.Header => Model.ParameterStyle.Simple,
                _ => Model.ParameterStyle.Form,
            };
        }

        return openApiStyle.Value switch
        {
            Microsoft.OpenApi.ParameterStyle.Form => Model.ParameterStyle.Form,
            Microsoft.OpenApi.ParameterStyle.Simple => Model.ParameterStyle.Simple,
            Microsoft.OpenApi.ParameterStyle.DeepObject => Model.ParameterStyle.DeepObject,
            Microsoft.OpenApi.ParameterStyle.PipeDelimited => Model.ParameterStyle.PipeDelimited,
            Microsoft.OpenApi.ParameterStyle.SpaceDelimited => Model.ParameterStyle.SpaceDelimited,
            _ => Model.ParameterStyle.Form,
        };
    }

    private static bool ResolveExplode(bool openApiExplode, Microsoft.OpenApi.ParameterStyle? openApiStyle, Model.ParameterLocation location)
    {
        // Microsoft.OpenApi v3.x applies OpenAPI spec defaults internally:
        // Form/Cookie → true, others → false (when not explicitly set).
        // So param.Explode already has the correct value when style is set.
        if (openApiStyle is not null)
            return openApiExplode;

        // When style is not set, apply location-based defaults.
        return location switch
        {
            Model.ParameterLocation.Query => true,
            _ => false,
        };
    }

    private static bool IsUnsupportedStyleCombination(Model.ParameterStyle style, bool explode)
    {
        return style switch
        {
            Model.ParameterStyle.DeepObject when !explode => true,
            Model.ParameterStyle.PipeDelimited => true,
            Model.ParameterStyle.SpaceDelimited => true,
            _ => false,
        };
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

            var style = MapParameterStyle(param.Style, location.Value);
            var explode = ResolveExplode(param.Explode, param.Style, location.Value);

            if (param.Style is Microsoft.OpenApi.ParameterStyle.Matrix
                or Microsoft.OpenApi.ParameterStyle.Label
                or Microsoft.OpenApi.ParameterStyle.Cookie)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    DiagnosticCodes.UnsupportedParameterStyleCombination,
                    $"Parameter '{param.Name}' uses unsupported style '{param.Style}'. Parameter skipped.",
                    specPath));
                continue;
            }

            if (IsUnsupportedStyleCombination(style, explode))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    DiagnosticCodes.UnsupportedParameterStyleCombination,
                    $"Parameter '{param.Name}' uses unsupported style/explode combination ({style}/{(explode ? "explode" : "no-explode")}). Parameter skipped.",
                    specPath));
                continue;
            }

            var isDeepObject = style == Model.ParameterStyle.DeepObject;
            var schema = ResolveParameterSchema(param.Schema, param.Name ?? "", specPath, allowInlineObject: isDeepObject);
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
                Style = style,
                Explode = explode,
            });
        }

        return (result, diagnostics.Count > 0 ? diagnostics : null);
    }

    private ApiSchema? ResolveParameterSchema(IOpenApiSchema? schema, string paramName, string specPath, bool allowInlineObject = false)
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
            if (allowInlineObject)
                return BuildInlineObjectSchema(resolved, paramName, specPath);

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

    private static readonly string[] RequestContentTypePreference =
    [
        "application/json",
        "multipart/form-data",
        "application/x-www-form-urlencoded",
        "application/octet-stream",
        "text/plain",
    ];

    private static ContentKind? MapContentKind(string mediaType)
    {
        if (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase))
            return ContentKind.Json;
        if (mediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            return ContentKind.FormUrlEncoded;
        if (mediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return ContentKind.MultipartFormData;
        if (mediaType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            || mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return ContentKind.OctetStream;
        if (mediaType.Equals("text/plain", StringComparison.OrdinalIgnoreCase))
            return ContentKind.PlainText;
        return null;
    }

    private (string MediaType, ContentKind Kind, IOpenApiMediaType Value)? SelectContentType(
        IDictionary<string, IOpenApiMediaType> content,
        string[] preferenceOrder,
        string specPath)
    {
        var selected = (string?)null;
        ContentKind? selectedKind = null;
        IOpenApiMediaType? selectedValue = null;
        var skipped = new List<string>();

        foreach (var preferred in preferenceOrder)
        {
            var match = content.FirstOrDefault(c =>
                c.Key.Equals(preferred, StringComparison.OrdinalIgnoreCase)
                || (preferred == "application/json" && c.Key.EndsWith("+json", StringComparison.OrdinalIgnoreCase)));
            if (match.Value is not null && selected is null)
            {
                selected = match.Key;
                selectedKind = MapContentKind(match.Key);
                selectedValue = match.Value;
            }
            else if (match.Value is not null)
            {
                skipped.Add(match.Key);
            }
        }

        if (selected is null)
        {
            foreach (var (key, value) in content)
            {
                var kind = MapContentKind(key);
                if (kind is not null && selected is null)
                {
                    selected = key;
                    selectedKind = kind;
                    selectedValue = value;
                }
                else if (kind is not null)
                {
                    skipped.Add(key);
                }
            }
        }

        if (selected is not null && skipped.Count > 0)
        {
            _diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Info,
                DiagnosticCodes.ContentTypeNegotiated,
                $"Multiple content types available [{string.Join(", ", content.Keys)}]. Selected {selected}.",
                specPath));
        }

        if (selected is not null && selectedKind is not null && selectedValue is not null)
            return (selected, selectedKind.Value, selectedValue);

        return null;
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

        var pick = SelectContentType(requestBody.Content, RequestContentTypePreference, specPath);

        if (pick is null)
        {
            var contentType = requestBody.Content.First().Key;
            _diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                DiagnosticCodes.UnsupportedContentType,
                $"Request body content type '{contentType}' is not supported.",
                specPath));

            if (requestBody.Required)
                return (null, true);
            return (null, false);
        }

        var (mediaType, contentKind, mediaTypeObj) = pick.Value;

        return contentKind switch
        {
            ContentKind.Json => TransformJsonRequestBody(mediaTypeObj, requestBody.Required, mediaType, specPath),
            ContentKind.FormUrlEncoded => TransformFormRequestBody(mediaTypeObj, requestBody.Required, mediaType, specPath),
            ContentKind.MultipartFormData => TransformMultipartRequestBody(mediaTypeObj, requestBody.Required, mediaType, specPath),
            ContentKind.OctetStream => TransformOctetStreamRequestBody(requestBody.Required, mediaType),
            ContentKind.PlainText => TransformPlainTextRequestBody(requestBody.Required, mediaType),
            _ => (null, false),
        };
    }

    private (ApiRequestBody? Body, bool SkipOperation) TransformJsonRequestBody(
        IOpenApiMediaType mediaTypeObj, bool isRequired, string mediaType, string specPath)
    {
        var schema = ResolveBodySchema(mediaTypeObj.Schema, specPath);
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
            IsRequired = isRequired,
            ContentKind = ContentKind.Json,
            MediaType = mediaType,
        }, false);
    }

    private (ApiRequestBody? Body, bool SkipOperation) TransformFormRequestBody(
        IOpenApiMediaType mediaTypeObj, bool isRequired, string mediaType, string specPath)
    {
        if (mediaTypeObj.Schema is null)
            return (null, false);

        var resolved = ResolveRef(mediaTypeObj.Schema);

        if (resolved.Properties is null or { Count: 0 })
            return (null, false);

        foreach (var (propName, propSchema) in resolved.Properties)
        {
            var resolvedProp = ResolveRef(propSchema);
            if (_schemaMap.ContainsKey(resolvedProp) ||
                (GetBaseType(resolvedProp) == JsonSchemaType.Object && resolvedProp.Properties is { Count: > 0 }))
            {
                _diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    DiagnosticCodes.UnsupportedInlineSchema,
                    $"Form-encoded property '{propName}' references a complex object. Only scalar types are supported in form bodies.",
                    specPath));
                if (isRequired)
                    return (null, true);
                return (null, false);
            }
        }

        var formSchema = BuildInlineObjectSchema(resolved, "formBody", specPath);

        return (new ApiRequestBody
        {
            Schema = formSchema,
            IsRequired = isRequired,
            ContentKind = ContentKind.FormUrlEncoded,
            MediaType = mediaType,
        }, false);
    }

    private (ApiRequestBody? Body, bool SkipOperation) TransformMultipartRequestBody(
        IOpenApiMediaType mediaTypeObj, bool isRequired, string mediaType, string specPath)
    {
        if (mediaTypeObj.Schema is null)
            return (null, false);

        var resolved = ResolveRef(mediaTypeObj.Schema);

        if (resolved.Properties is null or { Count: 0 })
            return (null, false);

        var multipartSchema = BuildInlineObjectSchema(resolved, "multipartBody", specPath, isBinaryContext: true);

        IReadOnlyDictionary<string, MultipartEncoding>? encodings = null;
        if (mediaTypeObj.Encoding is { Count: > 0 })
        {
            var dict = new Dictionary<string, MultipartEncoding>(StringComparer.Ordinal);
            foreach (var (propName, enc) in mediaTypeObj.Encoding)
            {
                if (resolved.Properties?.ContainsKey(propName) != true)
                {
                    _diagnostics.Add(new Diagnostic(
                        DiagnosticSeverity.Info,
                        DiagnosticCodes.UnknownEncodingProperty,
                        $"Multipart encoding references unknown property '{propName}'.",
                        specPath));
                    continue;
                }

                if (!string.IsNullOrEmpty(enc.ContentType))
                    dict[propName] = new MultipartEncoding { ContentType = enc.ContentType! };
            }
            if (dict.Count > 0)
                encodings = dict;
        }

        return (new ApiRequestBody
        {
            Schema = multipartSchema,
            IsRequired = isRequired,
            ContentKind = ContentKind.MultipartFormData,
            MediaType = mediaType,
            PropertyEncodings = encodings,
        }, false);
    }

    private static (ApiRequestBody? Body, bool SkipOperation) TransformOctetStreamRequestBody(bool isRequired, string mediaType)
    {
        var schema = new ApiSchema
        {
            Name = "body",
            OriginalName = "body",
            Kind = SchemaKind.Primitive,
            PrimitiveType = PrimitiveType.Stream,
        };

        return (new ApiRequestBody
        {
            Schema = schema,
            IsRequired = isRequired,
            ContentKind = ContentKind.OctetStream,
            MediaType = mediaType,
        }, false);
    }

    private static (ApiRequestBody? Body, bool SkipOperation) TransformPlainTextRequestBody(bool isRequired, string mediaType)
    {
        var schema = new ApiSchema
        {
            Name = "body",
            OriginalName = "body",
            Kind = SchemaKind.Primitive,
            PrimitiveType = PrimitiveType.String,
        };

        return (new ApiRequestBody
        {
            Schema = schema,
            IsRequired = isRequired,
            ContentKind = ContentKind.PlainText,
            MediaType = mediaType,
        }, false);
    }

    private ApiSchema BuildInlineObjectSchema(IOpenApiSchema resolved, string name, string specPath, bool isBinaryContext = false)
    {
        var requiredSet = resolved.Required;
        var properties = new List<ApiProperty>();

        if (resolved.Properties is null)
            return new ApiSchema
            {
                Name = name,
                OriginalName = name,
                Kind = SchemaKind.Object,
                Properties = [],
                Source = specPath,
            };

        foreach (var (propName, propSchema) in resolved.Properties)
        {
            var resolvedProp = ResolveRef(propSchema);

            // ASP.NET 10 emits oneOf: [null, $ref] for nullable reference types.
            // Unwrap to the non-null $ref for schema resolution.
            if (resolvedProp.OneOf is { Count: 2 })
            {
                var nonNull = resolvedProp.OneOf.FirstOrDefault(s =>
                    s.Type != JsonSchemaType.Null);
                if (nonNull is not null)
                    resolvedProp = ResolveRef(nonNull);
            }

            ApiSchema propApiSchema;

            if (_schemaMap.TryGetValue(resolvedProp, out var mapped))
            {
                var isBinarySchema = mapped.PrimitiveType == PrimitiveType.ByteArray
                    || (resolvedProp.Format is "binary" && GetBaseType(resolvedProp) == JsonSchemaType.String);
                if (isBinaryContext && isBinarySchema)
                {
                    propApiSchema = new ApiSchema
                    {
                        Name = propName,
                        OriginalName = propName,
                        Kind = SchemaKind.Primitive,
                        PrimitiveType = PrimitiveType.Stream,
                        Source = specPath,
                    };
                }
                else
                {
                    propApiSchema = mapped;
                }
            }
            else if (isBinaryContext && GetBaseType(resolvedProp) == JsonSchemaType.String
                     && resolvedProp.Format is "binary")
            {
                propApiSchema = new ApiSchema
                {
                    Name = propName,
                    OriginalName = propName,
                    Kind = SchemaKind.Primitive,
                    PrimitiveType = PrimitiveType.Stream,
                    Source = specPath,
                };
            }
            else if (GetBaseType(resolvedProp) == JsonSchemaType.Array)
            {
                var itemSchema = resolvedProp.Items != null ? ResolveParameterSchema(resolvedProp.Items, propName, specPath) : null;
                propApiSchema = new ApiSchema
                {
                    Name = propName,
                    OriginalName = propName,
                    Kind = SchemaKind.Array,
                    ArrayItemSchema = itemSchema,
                    Source = specPath,
                };
            }
            else
            {
                var prim = MapInlinePrimitive(resolvedProp);
                propApiSchema = new ApiSchema
                {
                    Name = propName,
                    OriginalName = propName,
                    Kind = SchemaKind.Primitive,
                    PrimitiveType = prim ?? PrimitiveType.String,
                    IsNullable = IsNullable(resolvedProp),
                    Source = specPath,
                };
            }

            properties.Add(new ApiProperty
            {
                Name = propName,
                CSharpName = NamingHelper.ToPascalCase(propName),
                Schema = propApiSchema,
                IsRequired = requiredSet?.Contains(propName) == true,
            });
        }

        return new ApiSchema
        {
            Name = name,
            OriginalName = name,
            Kind = SchemaKind.Object,
            Properties = properties,
            Source = specPath,
        };
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

    private static readonly string[] ResponseContentTypePreference =
    [
        "application/json",
        "application/octet-stream",
        "application/pdf",
        "text/plain",
    ];

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
                    Schema = null,
                }, false);
            }

            var pick = SelectContentType(response.Content, ResponseContentTypePreference, specPath);
            if (pick is null)
            {
                var unsupportedType = response.Content.First().Key;
                _diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    DiagnosticCodes.UnsupportedContentType,
                    $"Response content type '{unsupportedType}' is not supported.",
                    specPath));
                continue;
            }

            var (mediaType, contentKind, mediaTypeObj) = pick.Value;

            switch (contentKind)
            {
                case ContentKind.Json:
                {
                    var schema = ResolveResponseSchema(mediaTypeObj.Schema, specPath);
                    if (schema is null && mediaTypeObj.Schema is not null)
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
                        ContentKind = ContentKind.Json,
                        MediaType = mediaType,
                        Schema = schema,
                    }, false);
                }
                case ContentKind.OctetStream:
                {
                    var streamSchema = new ApiSchema
                    {
                        Name = "response",
                        OriginalName = "response",
                        Kind = SchemaKind.Primitive,
                        PrimitiveType = PrimitiveType.Stream,
                        Source = specPath,
                    };

                    return (new ApiResponse
                    {
                        StatusCode = statusCode,
                        ContentKind = ContentKind.OctetStream,
                        MediaType = mediaType,
                        Schema = streamSchema,
                    }, false);
                }
                case ContentKind.PlainText:
                {
                    var stringSchema = new ApiSchema
                    {
                        Name = "response",
                        OriginalName = "response",
                        Kind = SchemaKind.Primitive,
                        PrimitiveType = PrimitiveType.String,
                        Source = specPath,
                    };

                    return (new ApiResponse
                    {
                        StatusCode = statusCode,
                        ContentKind = ContentKind.PlainText,
                        MediaType = mediaType,
                        Schema = stringSchema,
                    }, false);
                }
                default:
                    continue;
            }
        }

        var firstCode = int.Parse(successResponses.First().Key);
        return (new ApiResponse
        {
            StatusCode = firstCode,
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
