using System.Reflection;
using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.Generation;
using ApiStitch.Model;
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

        var queryEnums = new HashSet<string>(StringComparer.Ordinal);
        var tagClients = new List<object>();

        foreach (var group in grouped)
        {
            var tag = group.Key;
            var isDefault = tag == clientName;
            var interfaceName = isDefault ? $"I{clientName}Client" : $"I{clientName}{tag}Client";
            var className = isDefault ? $"{clientName}Client" : $"{clientName}{tag}Client";

            var operations = group.OrderBy(o => o.CSharpMethodName, StringComparer.Ordinal).ToList();
            var opModels = BuildOperationModels(operations, queryEnums);

            files.Add(EmitInterface(ns, interfaceName, opModels));
            files.Add(EmitImplementation(ns, interfaceName, className, clientName, jsonOptionsName, opModels));

            tagClients.Add(new { interface_name = interfaceName, class_name = className });
        }

        files.Add(EmitApiException(ns));
        files.Add(EmitClientOptions(ns, clientName, optionsClassName));
        files.Add(EmitJsonOptionsWrapper(ns, clientName, jsonOptionsName, jsonContextName));
        files.Add(EmitDiRegistration(ns, clientName, optionsClassName, jsonOptionsName, tagClients));

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

            if (hasBody)
            {
                var bodyTypeName = GetTypeName(op.RequestBody!.Schema);
                allParams.Add(new
                {
                    type_name = bodyTypeName,
                    param_name = "body",
                    is_required = op.RequestBody!.IsRequired,
                    default_value = op.RequestBody!.IsRequired ? (string?)null : "null",
                });
            }

            foreach (var p in op.Parameters.Where(p => p.Location == ParameterLocation.Query).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                hasQueryParams = true;
                var pm = BuildParamModel(p, queryEnums);
                allParams.Add(pm);
                queryParams.Add(BuildQueryParamModel(p, queryEnums));
            }

            foreach (var p in op.Parameters.Where(p => p.Location == ParameterLocation.Header).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                var pm = BuildParamModel(p, queryEnums);
                allParams.Add(pm);
                headerParams.Add(BuildHeaderParamModel(p));
            }

            var returnType = hasResponseBody
                ? $"Task<{GetTypeName(op.SuccessResponse!.Schema!)}>"
                : "Task";

            var responseType = hasResponseBody ? GetTypeName(op.SuccessResponse!.Schema!) : null;

            var pathTemplate = BuildPathTemplate(op);

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
                body_param_name = hasBody ? "body" : null,
                parameters = allParams,
                query_params = queryParams,
                header_params = headerParams,
            });
        }

        return result;
    }

    private static object BuildParamModel(ApiParameter param, HashSet<string> queryEnums)
    {
        var typeName = GetTypeName(param.Schema);
        var isValueType = IsValueType(param.Schema);

        if (!param.IsRequired)
        {
            if (isValueType)
                typeName += "?";
            else
                typeName += "?";
        }

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

        return new
        {
            wire_name = param.Name,
            param_name = param.CSharpName,
            is_required = param.IsRequired,
            is_array = isArray,
            to_string_expr = toStringExpr,
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
        string jsonOptionsName, List<object> operations)
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

        return RenderTemplate(_implementationTemplate, $"{className}.cs", model);
    }

    private GeneratedFile EmitApiException(string ns)
    {
        var model = new ScriptObject();
        model.Add("namespace", ns);

        return RenderTemplate(_exceptionTemplate, "ApiException.cs", model);
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
