using System.Reflection;
using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.Generation;
using ApiStitch.Model;
using ApiStitch.Parsing;
using ApiStitch.TypeMapping;
using Scriban;
using Scriban.Runtime;

namespace ApiStitch.Emission;

/// <summary>
/// Emits typed HttpClient wrapper code using Scriban templates.
/// </summary>
public class ScribanClientEmitter : IClientEmitter
{
    private readonly Template _interfaceTemplate;
    private readonly Template _implementationTemplate;
    private readonly Template _exceptionTemplate;
    private readonly Template _optionsTemplate;
    private readonly Template _jsonOptionsTemplate;
    private readonly Template _diRegistrationTemplate;
    private readonly Template _enumExtensionsTemplate;
    private readonly Template _fileResponseTemplate;
    private readonly Template _problemDetailsTemplate;

    /// <summary>
    /// Creates a new client emitter, loading all templates from embedded resources.
    /// </summary>
    public ScribanClientEmitter()
    {
        _interfaceTemplate = LoadTemplate("ClientInterface.sbn-cs");
        _implementationTemplate = LoadTemplate("ClientImplementation.sbn-cs");
        _exceptionTemplate = LoadTemplate("ApiException.sbn-cs");
        _optionsTemplate = LoadTemplate("ClientOptions.sbn-cs");
        _jsonOptionsTemplate = LoadTemplate("JsonOptionsWrapper.sbn-cs");
        _diRegistrationTemplate = LoadTemplate("DiRegistration.sbn-cs");
        _enumExtensionsTemplate = LoadTemplate("EnumExtensions.sbn-cs");
        _fileResponseTemplate = LoadTemplate("FileResponse.sbn-cs");
        _problemDetailsTemplate = LoadTemplate("ProblemDetails.sbn-cs");
    }

    /// <inheritdoc />
    public EmissionResult Emit(ApiSpecification spec, ApiStitchConfig config)
    {
        if (spec.Operations.Count == 0)
            return new EmissionResult([], []);

        var files = new List<GeneratedFile>();
        var diagnostics = new List<Diagnostic>();
        var clientName = spec.ClientName ?? "ApiClient";
        var ns = config.Namespace;
        var lastSegment = ns.Split('.').Last();
        var jsonContextName = $"{lastSegment}JsonContext";
        var jsonOptionsName = $"{clientName}JsonOptions";
        var optionsClassName = $"{clientName}ClientOptions";

        var grouped = spec.Operations
            .GroupBy(o => o.Tag, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        var hasProblemDetailsSupport = spec.HasProblemDetailsSupport;
        var problemDetailsSchema = hasProblemDetailsSupport
            ? spec.Schemas.FirstOrDefault(s => s.Name == "ProblemDetails")
            : null;
        var problemDetailsTypeName = problemDetailsSchema is { IsExternal: true }
            ? problemDetailsSchema.CSharpTypeName!
            : "ProblemDetails";

        var queryEnums = new HashSet<string>(StringComparer.Ordinal);
        var tagClients = new List<object>();

        foreach (var group in grouped)
        {
            var tag = group.Key;
            var isDefault = tag == clientName;
            var normalizedTag = NormalizeTagName(tag);
            var interfaceName = isDefault ? $"I{clientName}Client" : $"I{clientName}{normalizedTag}Client";
            var className = isDefault ? $"{clientName}Client" : $"{clientName}{normalizedTag}Client";

            var operations = group.OrderBy(o => o.CSharpMethodName, StringComparer.Ordinal).ToList();
            var opModels = BuildOperationModels(operations, queryEnums);

            files.Add(EmitInterface(ns, interfaceName, opModels));
            files.Add(EmitImplementation(ns, interfaceName, className, clientName, jsonOptionsName, problemDetailsTypeName, hasProblemDetailsSupport, opModels));

            tagClients.Add(new { interface_name = interfaceName, class_name = className });
        }

        if (hasProblemDetailsSupport && problemDetailsSchema is not { IsExternal: true })
            files.Add(EmitProblemDetails(ns));
        files.Add(EmitApiException(ns, problemDetailsTypeName, hasProblemDetailsSupport));
        files.Add(EmitClientOptions(ns, clientName, optionsClassName));
        files.Add(EmitJsonOptionsWrapper(ns, clientName, jsonOptionsName, jsonContextName));
        files.Add(EmitDiRegistration(ns, clientName, optionsClassName, jsonOptionsName, tagClients));

        var hasStreamResponse = spec.Operations.Any(o =>
            o.SuccessResponse?.ContentKind == ContentKind.OctetStream);
        if (hasStreamResponse)
            files.Add(EmitFileResponse(ns));

        foreach (var enumName in queryEnums.OrderBy(n => n, StringComparer.Ordinal))
        {
            var enumSchema = spec.Schemas.FirstOrDefault(s => s.Name == enumName && s.Kind == SchemaKind.Enum);
            if (enumSchema is not null)
                files.Add(EmitEnumExtensions(ns, enumSchema));
        }

        files.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.Ordinal));
        return new EmissionResult(files, diagnostics);
    }

    private List<object> BuildOperationModels(List<ApiOperation> operations, HashSet<string> queryEnums)
    {
        var result = new List<object>();

        foreach (var op in operations)
        {
            var allParams = new List<object>();
            var queryParams = new List<object>();
            var headerParams = new List<object>();
            var hasQueryParams = false;
            var hasBody = op.RequestBody is not null;
            var hasResponseBody = op.SuccessResponse?.HasBody == true;

            var pathParams = op.Parameters
                .Where(p => p.Location == ParameterLocation.Path)
                .OrderBy(p => p.Name, StringComparer.Ordinal);

            foreach (var p in pathParams)
                allParams.Add(BuildParamModel(p, queryEnums));

            var (bodyModel, bodyParams) = BuildRequestBodyModel(op.RequestBody);
            if (hasBody)
                allParams.AddRange(bodyParams);

            foreach (var p in op.Parameters.Where(p => p.Location == ParameterLocation.Query).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                hasQueryParams = true;

                if (p.Style == Model.ParameterStyle.DeepObject
                    && p.Schema.Kind == SchemaKind.Object
                    && p.Schema.CSharpTypeName is null)
                {
                    foreach (var prop in p.Schema.Properties)
                    {
                        var propTypeName = GetTypeName(prop.Schema) + "?";
                        var propParamName = ToCamelCase(prop.CSharpName);
                        allParams.Add(new
                        {
                            type_name = propTypeName,
                            param_name = propParamName,
                            is_required = false,
                            default_value = (string?)"null",
                        });
                    }
                    queryParams.Add(BuildQueryParamModel(p, queryEnums));
                }
                else
                {
                    allParams.Add(BuildParamModel(p, queryEnums));
                    queryParams.Add(BuildQueryParamModel(p, queryEnums));
                }
            }

            foreach (var p in op.Parameters.Where(p => p.Location == ParameterLocation.Header).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                var pm = BuildParamModel(p, queryEnums);
                allParams.Add(pm);
                headerParams.Add(BuildHeaderParamModel(p));
            }

            var (returnType, responseType, responseModel) = BuildResponseModel(op.SuccessResponse);

            var pathTemplate = BuildPathTemplate(op);

            var acceptHeader = op.SuccessResponse?.MediaType;

            result.Add(new
            {
                method_name = op.CSharpMethodName,
                return_type = returnType,
                response_type = responseType,
                http_method = op.HttpMethod.ToString(),
                path_template = pathTemplate,
                is_deprecated = op.IsDeprecated,
                has_body = hasBody,
                has_response_body = hasResponseBody,
                has_query_params = hasQueryParams,
                body_content_kind = bodyModel.content_kind,
                body_param_name = bodyModel.param_name,
                body_use_json_options = bodyModel.use_json_options,
                form_fields = bodyModel.form_fields,
                multipart_parts = bodyModel.multipart_parts,
                response_content_kind = responseModel.content_kind,
                response_use_json_options = responseModel.use_json_options,
                accept_header = acceptHeader,
                parameters = allParams,
                query_params = queryParams,
                header_params = headerParams,
            });
        }

        return result;
    }

    private (dynamic bodyModel, List<object> bodyParams) BuildRequestBodyModel(ApiRequestBody? requestBody)
    {
        var empty = new
        {
            content_kind = (string?)null,
            param_name = (string?)null,
            use_json_options = true,
            form_fields = (List<object>?)null,
            multipart_parts = (List<object>?)null,
        };

        if (requestBody is null)
            return (empty, []);

        // Template matches on lowercase enum name (e.g. "json", "formurlencoded")
        var contentKind = requestBody.ContentKind.ToString().ToLowerInvariant();
        var bodyParams = new List<object>();

        switch (requestBody.ContentKind)
        {
            case ContentKind.Json:
            {
                var typeName = GetTypeName(requestBody.Schema);
                var useJsonOptions = JsonSerializationCompatibility.ShouldUseGeneratedJsonOptions(requestBody.Schema);
                bodyParams.Add(new
                {
                    type_name = typeName,
                    param_name = "body",
                    is_required = requestBody.IsRequired,
                    default_value = requestBody.IsRequired ? (string?)null : "null",
                });

                return (new
                {
                    content_kind = contentKind,
                    param_name = (string?)"body",
                    use_json_options = useJsonOptions,
                    form_fields = (List<object>?)null,
                    multipart_parts = (List<object>?)null,
                }, bodyParams);
            }
            case ContentKind.FormUrlEncoded:
            {
                var formFields = new List<object>();
                foreach (var prop in requestBody.Schema.Properties)
                {
                    var typeName = GetTypeName(prop.Schema);
                    var paramName = ToCamelCase(prop.CSharpName);
                    if (!prop.IsRequired)
                        typeName += "?";
                    bodyParams.Add(new
                    {
                        type_name = typeName,
                        param_name = paramName,
                        is_required = prop.IsRequired,
                        default_value = prop.IsRequired ? (string?)null : "null",
                    });
                    formFields.Add(new
                    {
                        wire_name = prop.Name,
                        param_name = paramName,
                        is_required = prop.IsRequired,
                    });
                }

                return (new
                {
                    content_kind = contentKind,
                    param_name = (string?)null,
                    use_json_options = true,
                    form_fields = (List<object>?)formFields,
                    multipart_parts = (List<object>?)null,
                }, bodyParams);
            }
            case ContentKind.MultipartFormData:
            {
                var parts = new List<object>();
                foreach (var prop in requestBody.Schema.Properties)
                {
                    var isBinary = prop.Schema.PrimitiveType == PrimitiveType.Stream;
                    var typeName = GetTypeName(prop.Schema);
                    var paramName = ToCamelCase(prop.CSharpName);
                    var hasJsonEncoding = requestBody.PropertyEncodings?.TryGetValue(prop.Name, out var enc) == true
                        && enc.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase);
                    var useJsonOptions = !hasJsonEncoding || JsonSerializationCompatibility.ShouldUseGeneratedJsonOptions(prop.Schema);

                    if (!prop.IsRequired)
                        typeName += "?";

                    bodyParams.Add(new
                    {
                        type_name = typeName,
                        param_name = paramName,
                        is_required = prop.IsRequired,
                        default_value = prop.IsRequired ? (string?)null : "null",
                    });

                    string? fileNameParamName = null;
                    if (isBinary)
                    {
                        fileNameParamName = paramName + "FileName";
                        bodyParams.Add(new
                        {
                            type_name = prop.IsRequired ? "string" : "string?",
                            param_name = fileNameParamName,
                            is_required = prop.IsRequired,
                            default_value = prop.IsRequired ? (string?)null : "null",
                        });
                    }

                    parts.Add(new
                    {
                        wire_name = prop.Name,
                        param_name = paramName,
                        is_binary = isBinary,
                        has_json_encoding = hasJsonEncoding,
                        use_json_options = useJsonOptions,
                        file_name_param_name = fileNameParamName,
                        is_required = prop.IsRequired,
                    });
                }

                return (new
                {
                    content_kind = contentKind,
                    param_name = (string?)null,
                    use_json_options = true,
                    form_fields = (List<object>?)null,
                    multipart_parts = (List<object>?)parts,
                }, bodyParams);
            }
            case ContentKind.OctetStream:
            {
                bodyParams.Add(new
                {
                    type_name = "Stream",
                    param_name = "body",
                    is_required = true,
                    default_value = (string?)null,
                });

                return (new
                {
                    content_kind = contentKind,
                    param_name = (string?)"body",
                    use_json_options = true,
                    form_fields = (List<object>?)null,
                    multipart_parts = (List<object>?)null,
                }, bodyParams);
            }
            case ContentKind.PlainText:
            {
                bodyParams.Add(new
                {
                    type_name = "string",
                    param_name = "body",
                    is_required = requestBody.IsRequired,
                    default_value = requestBody.IsRequired ? (string?)null : "null",
                });

                return (new
                {
                    content_kind = contentKind,
                    param_name = (string?)"body",
                    use_json_options = true,
                    form_fields = (List<object>?)null,
                    multipart_parts = (List<object>?)null,
                }, bodyParams);
            }
            default:
                return (empty, []);
        }
    }

    private (string returnType, string? responseType, dynamic responseModel) BuildResponseModel(ApiResponse? successResponse)
    {
        var hasResponseBody = successResponse?.HasBody == true;
        var contentKind = successResponse?.ContentKind?.ToString().ToLowerInvariant();

        if (!hasResponseBody)
        {
            return ("Task", null, new { content_kind = (string?)null, use_json_options = true });
        }

        if (successResponse!.ContentKind == ContentKind.OctetStream)
        {
            return ("Task<FileResponse>", "FileResponse", new { content_kind = contentKind, use_json_options = true });
        }

        if (successResponse.ContentKind == ContentKind.PlainText)
        {
            return ("Task<string>", "string", new { content_kind = contentKind, use_json_options = true });
        }

        var useJsonOptions = JsonSerializationCompatibility.ShouldUseGeneratedJsonOptions(successResponse.Schema!);
        var responseType = GetTypeName(successResponse.Schema!);
        return ($"Task<{responseType}>", responseType, new { content_kind = contentKind, use_json_options = useJsonOptions });
    }

    private static string ToCamelCase(string pascal)
    {
        if (string.IsNullOrEmpty(pascal))
            return pascal;
        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    private static object BuildParamModel(ApiParameter param, HashSet<string> queryEnums)
    {
        var typeName = GetTypeName(param.Schema);
        var isValueType = IsValueType(param.Schema);

        if (!param.IsRequired)
            typeName += "?";

        if (param.Location == ParameterLocation.Query && param.Schema.Kind == SchemaKind.Enum
            && !param.Schema.IsExternal)
            queryEnums.Add(param.Schema.Name);

        if (param.Location == ParameterLocation.Query && param.Schema.Kind == SchemaKind.Array
            && param.Schema.ArrayItemSchema?.Kind == SchemaKind.Enum
            && !(param.Schema.ArrayItemSchema?.IsExternal ?? false))
            queryEnums.Add(param.Schema.ArrayItemSchema!.Name);

        return new
        {
            type_name = typeName,
            param_name = param.CSharpName,
            is_required = param.IsRequired,
            default_value = param.IsRequired ? (string?)null : "null",
        };
    }

    private static object BuildQueryParamModel(ApiParameter param, HashSet<string> queryEnums)
    {
        var isArray = param.Schema.Kind == SchemaKind.Array;
        var isEnum = param.Schema.Kind == SchemaKind.Enum;
        var isArrayOfEnum = isArray && param.Schema.ArrayItemSchema?.Kind == SchemaKind.Enum;

        var isExternalEnum = isEnum && param.Schema.IsExternal;
        var isExternalArrayOfEnum = isArrayOfEnum && (param.Schema.ArrayItemSchema?.IsExternal ?? false);

        string toStringExpr;
        if (isEnum && !isExternalEnum)
        {
            queryEnums.Add(param.Schema.Name);
            toStringExpr = $"{param.CSharpName}{(param.IsRequired ? "" : ".Value")}.ToQueryString()";
        }
        else if (isEnum && isExternalEnum)
        {
            toStringExpr = $"{param.CSharpName}{(param.IsRequired ? "" : ".Value")}.ToString()";
        }
        else if (isArrayOfEnum && !isExternalArrayOfEnum)
        {
            queryEnums.Add(param.Schema.ArrayItemSchema!.Name);
            toStringExpr = "item.ToQueryString()";
        }
        else if (isArrayOfEnum && isExternalArrayOfEnum)
        {
            toStringExpr = "item.ToString()";
        }
        else if (isArray)
        {
            toStringExpr = "item.ToString()";
        }
        else
        {
            toStringExpr = $"{param.CSharpName}{(param.IsRequired || !IsValueType(param.Schema) ? "" : ".Value")}.ToString()";
        }

        var isDeepObject = param.Style == Model.ParameterStyle.DeepObject;
        var isFlattened = isDeepObject && param.Schema.Kind == SchemaKind.Object && param.Schema.CSharpTypeName is null;
        List<object>? deepObjectProps = null;
        if (isDeepObject && param.Schema.Kind == SchemaKind.Object)
        {
            deepObjectProps = param.Schema.Properties
                .Select(p => (object)new
                {
                    wire_name = p.Name,
                    csharp_name = isFlattened ? ToCamelCase(p.CSharpName) : p.CSharpName,
                })
                .ToList();
        }

        return new
        {
            wire_name = param.Name,
            param_name = param.CSharpName,
            is_required = param.IsRequired,
            is_array = isArray,
            to_string_expr = toStringExpr,
            style = param.Style.ToString().ToLowerInvariant(),
            explode = param.Explode,
            is_deep_object = isDeepObject,
            is_flattened = isFlattened,
            deep_object_props = deepObjectProps,
        };
    }

    private static object BuildHeaderParamModel(ApiParameter param)
    {
        var toStringExpr = $"{param.CSharpName}{(param.IsRequired || !IsValueType(param.Schema) ? "" : ".Value")}.ToString()";

        return new
        {
            wire_name = param.Name,
            param_name = param.CSharpName,
            is_required = param.IsRequired,
            to_string_expr = toStringExpr,
        };
    }

    private static string BuildPathTemplate(ApiOperation op)
    {
        var path = op.Path;
        foreach (var param in op.Parameters.Where(p => p.Location == ParameterLocation.Path))
        {
            path = path.Replace(
                $"{{{param.Name}}}",
                $"{{Uri.EscapeDataString({param.CSharpName}.ToString())}}");
        }
        return path;
    }

    private GeneratedFile EmitInterface(string ns, string interfaceName, List<object> operations)
    {
        var model = new ScriptObject();
        model.Add("namespace", ns);
        model.Add("interface_name", interfaceName);
        model.Add("operations", operations);

        return RenderTemplate(_interfaceTemplate, $"{interfaceName}.cs", model);
    }

    private GeneratedFile EmitImplementation(
        string ns, string interfaceName, string className, string clientName,
        string jsonOptionsName, string problemDetailsTypeName, bool hasProblemDetailsSupport, List<object> operations)
    {
        var hasQueryMethods = operations.Any(o =>
        {
            var so = o as dynamic;
            return (bool)so.has_query_params;
        });

        var model = new ScriptObject();
        model.Add("namespace", ns);
        model.Add("interface_name", interfaceName);
        model.Add("class_name", className);
        model.Add("client_name", clientName);
        model.Add("json_options_name", jsonOptionsName);
        model.Add("operations", operations);
        model.Add("has_query_methods", hasQueryMethods);
        model.Add("has_problem_details", hasProblemDetailsSupport);
        model.Add("problem_details_type", problemDetailsTypeName);

        return RenderTemplate(_implementationTemplate, $"{className}.cs", model);
    }

    private GeneratedFile EmitApiException(string ns, string problemDetailsTypeName, bool hasProblemDetailsSupport)
    {
        var model = new ScriptObject();
        model.Add("namespace", ns);
        model.Add("problem_details_type", problemDetailsTypeName);
        model.Add("has_problem_details", hasProblemDetailsSupport);

        return RenderTemplate(_exceptionTemplate, "ApiException.cs", model);
    }

    private GeneratedFile EmitFileResponse(string ns)
    {
        var model = new ScriptObject();
        model.Add("namespace", ns);

        return RenderTemplate(_fileResponseTemplate, "FileResponse.cs", model);
    }

    private GeneratedFile EmitProblemDetails(string ns)
    {
        var model = new ScriptObject();
        model.Add("namespace", ns);

        return RenderTemplate(_problemDetailsTemplate, "ProblemDetails.cs", model);
    }

    private GeneratedFile EmitClientOptions(string ns, string clientName, string optionsClassName)
    {
        var model = new ScriptObject();
        model.Add("namespace", ns);
        model.Add("client_name", clientName);
        model.Add("options_class_name", optionsClassName);

        return RenderTemplate(_optionsTemplate, $"{optionsClassName}.cs", model);
    }

    private GeneratedFile EmitJsonOptionsWrapper(string ns, string clientName, string jsonOptionsName, string jsonContextName)
    {
        var model = new ScriptObject();
        model.Add("namespace", ns);
        model.Add("client_name", clientName);
        model.Add("json_options_name", jsonOptionsName);
        model.Add("json_context_name", jsonContextName);

        return RenderTemplate(_jsonOptionsTemplate, $"{jsonOptionsName}.cs", model);
    }

    private GeneratedFile EmitDiRegistration(
        string ns, string clientName, string optionsClassName, string jsonOptionsName, List<object> tagClients)
    {
        var extensionsClassName = $"{clientName}ServiceCollectionExtensions";

        var model = new ScriptObject();
        model.Add("namespace", ns);
        model.Add("client_name", clientName);
        model.Add("options_class_name", optionsClassName);
        model.Add("json_options_name", jsonOptionsName);
        model.Add("extensions_class_name", extensionsClassName);
        model.Add("tag_clients", tagClients);

        return RenderTemplate(_diRegistrationTemplate, $"{extensionsClassName}.cs", model);
    }

    private GeneratedFile EmitEnumExtensions(string ns, ApiSchema enumSchema)
    {
        var members = enumSchema.EnumValues.Select(m => new
        {
            wire_value = m.Name,
            csharp_name = m.CSharpName,
        }).ToList();

        var model = new ScriptObject();
        model.Add("namespace", ns);
        model.Add("enum_name", enumSchema.Name);
        model.Add("members", members);

        return RenderTemplate(_enumExtensionsTemplate, $"{enumSchema.Name}Extensions.cs", model);
    }

    private static string GetTypeName(ApiSchema schema)
    {
        return schema.CSharpTypeName ?? CSharpTypeMapper.MapSchema(schema);
    }

    private static string NormalizeTagName(string tag)
    {
        var pascal = NamingHelper.ToPascalCase(tag);
        var cleaned = new string(pascal.Where(char.IsLetterOrDigit).ToArray());

        if (cleaned.Length == 0)
            return "Tag";

        if (char.IsDigit(cleaned[0]))
            return $"Tag{cleaned}";

        return cleaned;
    }

    private static bool IsValueType(ApiSchema schema)
    {
        if (schema.Kind == SchemaKind.Enum) return true;
        if (schema.Kind == SchemaKind.Primitive && schema.PrimitiveType.HasValue)
            return CSharpTypeMapper.IsValueType(schema.PrimitiveType.Value);
        return false;
    }

    private static GeneratedFile RenderTemplate(Template template, string fileName, ScriptObject model)
    {
        var context = new TemplateContext();
        context.PushGlobal(model);
        var content = template.Render(context).TrimEnd() + "\n";
        return new GeneratedFile(fileName, content);
    }

    private static Template LoadTemplate(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return Template.Parse(reader.ReadToEnd());
    }
}
