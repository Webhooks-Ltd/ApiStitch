using System.Reflection;
using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.Generation;
using ApiStitch.Model;
using ApiStitch.TypeMapping;
using Scriban;
using Scriban.Runtime;

namespace ApiStitch.Emission;

public class ScribanModelEmitter : IModelEmitter
{
    private readonly Template _recordTemplate;
    private readonly Template _enumTemplate;
    private readonly Template _contextTemplate;

    public ScribanModelEmitter()
    {
        _recordTemplate = LoadTemplate("Record.sbn-cs");
        _enumTemplate = LoadTemplate("Enum.sbn-cs");
        _contextTemplate = LoadTemplate("JsonSerializerContext.sbn-cs");
    }

    public EmissionResult Emit(ApiSpecification spec, ApiStitchConfig config)
    {
        var files = new List<GeneratedFile>();
        var diagnostics = new List<Diagnostic>();
        var typeNames = new List<string>();

        foreach (var schema in spec.Schemas.OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            if (schema.IsExternal)
            {
                if (schema.Kind is SchemaKind.Object or SchemaKind.Enum)
                    typeNames.Add(schema.CSharpTypeName!);
                continue;
            }

            switch (schema.Kind)
            {
                case SchemaKind.Object:
                    files.Add(EmitRecord(schema, spec, config, diagnostics));
                    typeNames.Add(schema.Name);
                    break;
                case SchemaKind.Enum:
                    files.Add(EmitEnum(schema, config));
                    typeNames.Add(schema.Name);
                    break;
            }
        }

        if (spec.Operations.Count > 0)
            typeNames.Add("ProblemDetails");

        files.Add(EmitJsonContext(typeNames, spec, config));

        files.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.Ordinal));

        return new EmissionResult(files, diagnostics);
    }

    private GeneratedFile EmitRecord(ApiSchema schema, ApiSpecification spec, ApiStitchConfig config, List<Diagnostic> diagnostics)
    {
        var hasCollections = schema.Properties.Any(p =>
            p.Schema.Kind == SchemaKind.Array ||
            (p.Schema.CSharpTypeName?.StartsWith("IReadOnlyList<", StringComparison.Ordinal) ?? false));

        var hasTypedAdditionalProperties = schema.AdditionalPropertiesSchema != null;

        if (hasTypedAdditionalProperties)
        {
            diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "AS205",
                $"Schema '{schema.OriginalName}' has typed additionalProperties. Approximated as Dictionary<string, JsonElement> because [JsonExtensionData] only supports JsonElement.",
                schema.Source));
        }

        var properties = schema.Properties.Select(p => new
        {
            json_name = p.Name,
            csharp_name = p.CSharpName,
            type_name = GetPropertyTypeName(p),
            is_required = p.IsRequired,
            is_nullable = p.IsNullable,
            is_deprecated = p.IsDeprecated,
            type_is_reference = IsReferenceType(p),
        }).ToList();

        var model = new ScriptObject();
        model.Add("namespace", config.Namespace);
        model.Add("name", schema.Name);
        model.Add("base_name", schema.BaseSchema?.CSharpTypeName);
        model.Add("is_sealed", schema.BaseSchema != null);
        model.Add("is_deprecated", schema.IsDeprecated);
        model.Add("has_additional_properties", schema.HasAdditionalProperties);
        model.Add("additional_properties_comment", hasTypedAdditionalProperties
            ? "Typed additionalProperties approximated as Dictionary<string, JsonElement>. [JsonExtensionData] only supports JsonElement."
            : null);
        model.Add("has_collections", hasCollections);
        model.Add("properties", properties);

        var context = new TemplateContext();
        context.PushGlobal(model);

        var content = _recordTemplate.Render(context).TrimEnd() + "\n";
        return new GeneratedFile($"{schema.Name}.cs", content);
    }

    private GeneratedFile EmitEnum(ApiSchema schema, ApiStitchConfig config)
    {
        var members = schema.EnumValues.Select(m => new
        {
            wire_value = m.Name,
            csharp_name = m.CSharpName,
        }).ToList();

        var model = new ScriptObject();
        model.Add("namespace", config.Namespace);
        model.Add("name", schema.Name);
        model.Add("is_deprecated", schema.IsDeprecated);
        model.Add("members", members);

        var context = new TemplateContext();
        context.PushGlobal(model);

        var content = _enumTemplate.Render(context).TrimEnd() + "\n";
        return new GeneratedFile($"{schema.Name}.cs", content);
    }

    private GeneratedFile EmitJsonContext(List<string> typeNames, ApiSpecification spec, ApiStitchConfig config)
    {
        var lastSegment = config.Namespace.Split('.').Last();
        var contextName = $"{lastSegment}JsonContext";

        var collectionTypes = spec.CollectionTypes
            .Select(s => s.CSharpTypeName ?? CSharpTypeMapper.MapSchema(s))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var model = new ScriptObject();
        model.Add("namespace", config.Namespace);
        model.Add("context_name", contextName);
        model.Add("type_names", typeNames.OrderBy(n => n, StringComparer.Ordinal).ToList());
        model.Add("collection_types", collectionTypes);
        model.Add("has_collections", collectionTypes.Count > 0);

        var context = new TemplateContext();
        context.PushGlobal(model);

        var content = _contextTemplate.Render(context).TrimEnd() + "\n";
        return new GeneratedFile($"{contextName}.cs", content);
    }

    private static string GetPropertyTypeName(ApiProperty property)
    {
        var schema = property.Schema;
        var typeName = schema.CSharpTypeName ?? CSharpTypeMapper.MapSchema(schema);

        if (property.IsNullable && IsReferenceType(property))
            return typeName + "?";

        return typeName;
    }

    private static bool IsReferenceType(ApiProperty property)
    {
        var schema = property.Schema;
        if (schema.Kind == SchemaKind.Array) return true;
        if (schema.Kind == SchemaKind.Object) return true;
        if (schema.Kind == SchemaKind.Primitive)
        {
            return schema.PrimitiveType switch
            {
                PrimitiveType.String => true,
                PrimitiveType.Uri => true,
                PrimitiveType.ByteArray => true,
                _ => false,
            };
        }
        return false;
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
