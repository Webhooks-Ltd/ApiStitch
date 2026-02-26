using System.Text.Json.Nodes;
using ApiStitch.Diagnostics;
using ApiStitch.Model;
using Microsoft.OpenApi;

namespace ApiStitch.Parsing;

public class SchemaTransformer
{
    private readonly List<Diagnostic> _diagnostics = [];
    private readonly Dictionary<IOpenApiSchema, ApiSchema> _schemaMap = new(ReferenceEqualityComparer.Instance);
    private readonly List<ApiSchema> _allSchemas = [];
    private readonly HashSet<string> _usedNames = new(StringComparer.Ordinal);
    private readonly Dictionary<IOpenApiSchema, string> _componentSchemaNames = new(ReferenceEqualityComparer.Instance);

    private static IOpenApiSchema ResolveRef(IOpenApiSchema schema) => OpenApiSchemaHelpers.ResolveRef(schema);
    private static JsonSchemaType? GetBaseType(IOpenApiSchema schema) => OpenApiSchemaHelpers.GetBaseType(schema);
    private static bool IsNullable(IOpenApiSchema schema) => OpenApiSchemaHelpers.IsNullable(schema);

    /// <summary>
    /// Transforms OpenAPI component schemas into the ApiStitch semantic model.
    /// </summary>
    /// <returns>The specification, a schema map for operation resolution, and diagnostics.</returns>
    public (ApiSpecification Specification, IReadOnlyDictionary<IOpenApiSchema, ApiSchema> SchemaMap, IReadOnlyList<Diagnostic> Diagnostics) Transform(OpenApiDocument document)
    {
        var componentSchemas = document.Components?.Schemas ?? new Dictionary<string, IOpenApiSchema>();

        var sortedSchemas = componentSchemas
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToList();

        foreach (var (name, schema) in sortedSchemas)
            _componentSchemaNames[schema] = name;

        foreach (var (originalName, openApiSchema) in sortedSchemas)
        {
            TransformSchema(openApiSchema, originalName, $"#/components/schemas/{originalName}");
        }

        DetectCircularReferences();

        var metadata = new ApiSpecificationMetadata
        {
            Title = document.Info?.Title,
            Version = document.Info?.Version,
            Description = document.Info?.Description
        };

        var spec = new ApiSpecification
        {
            Schemas = _allSchemas,
            Operations = [],
            Metadata = metadata
        };

        return (spec, _schemaMap, _diagnostics);
    }

    private ApiSchema TransformSchema(IOpenApiSchema openApiSchema, string originalName, string source)
    {
        if (_schemaMap.TryGetValue(openApiSchema, out var existing))
            return existing;

        var pascalName = NamingHelper.ToPascalCase(originalName);
        var resolvedName = NamingHelper.ResolveCollision(pascalName, _usedNames);

        if (resolvedName != pascalName)
        {
            _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "AS203",
                $"Name collision: '{originalName}' → '{pascalName}' conflicts with existing schema. Renamed to '{resolvedName}'.",
                source));
        }

        ApiSchema schema;

        if (openApiSchema.AllOf is { Count: > 0 })
        {
            schema = TransformAllOf(openApiSchema, resolvedName, originalName, source);
        }
        else if (GetBaseType(openApiSchema) == JsonSchemaType.Array)
        {
            schema = TransformArray(openApiSchema, resolvedName, originalName, source);
        }
        else if (openApiSchema.Enum is { Count: > 0 })
        {
            schema = TransformEnum(openApiSchema, resolvedName, originalName, source);
        }
        else if (GetBaseType(openApiSchema) == JsonSchemaType.Object || openApiSchema.Properties is { Count: > 0 })
        {
            schema = TransformObject(openApiSchema, resolvedName, originalName, source);
        }
        else
        {
            schema = TransformPrimitive(openApiSchema, resolvedName, originalName, source);
        }

        if (openApiSchema.Extensions?.TryGetValue("x-apistitch-type", out var ext) == true
            && ext is JsonNodeExtension jsonExt
            && jsonExt.Node is JsonValue jsonValue
            && jsonValue.TryGetValue(out string? strValue)
            && !string.IsNullOrWhiteSpace(strValue))
        {
            schema.VendorTypeHint = strValue;
        }

        _schemaMap[openApiSchema] = schema;
        _allSchemas.Add(schema);
        return schema;
    }

    private ApiSchema TransformObject(IOpenApiSchema openApiSchema, string name, string originalName, string source)
    {
        var schema = new ApiSchema
        {
            Name = name,
            OriginalName = originalName,
            Kind = SchemaKind.Object,
            Description = openApiSchema.Description,
            IsNullable = IsNullable(openApiSchema),
            IsDeprecated = openApiSchema.Deprecated,
            HasAdditionalProperties = openApiSchema.AdditionalProperties != null,
            AdditionalPropertiesSchema = openApiSchema.AdditionalProperties?.Type != null
                ? GetOrTransformPropertySchema(openApiSchema.AdditionalProperties, name, "additionalProperties", source)
                : null,
            Source = source,
        };

        _schemaMap[openApiSchema] = schema;

        var required = new HashSet<string>(openApiSchema.Required ?? new HashSet<string>(), StringComparer.Ordinal);
        var properties = new List<ApiProperty>();

        foreach (var (propName, propSchema) in openApiSchema.Properties ?? new Dictionary<string, IOpenApiSchema>())
        {
            var propApiSchema = GetOrTransformPropertySchema(propSchema, name, propName, $"{source}/properties/{propName}");
            properties.Add(new ApiProperty
            {
                Name = propName,
                CSharpName = NamingHelper.ToPascalCase(propName),
                Schema = propApiSchema,
                IsRequired = required.Contains(propName),
                IsDeprecated = propSchema.Deprecated,
                Description = propSchema.Description,
                DefaultValue = propSchema.Default?.ToString(),
            });
        }

        schema.Properties = properties;

        return schema;
    }

    private ApiSchema TransformEnum(IOpenApiSchema openApiSchema, string name, string originalName, string source)
    {
        if (GetBaseType(openApiSchema) != JsonSchemaType.String)
        {
            _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "AS200",
                $"Schema '{originalName}' is a {GetBaseType(openApiSchema)} enum. Only string enums are supported; treating as primitive type.",
                source));
            return TransformPrimitive(openApiSchema, name, originalName, source);
        }

        var members = openApiSchema.Enum!
            .Select(e =>
            {
                var value = (e as JsonValue)?.ToString() ?? e?.ToString() ?? "";
                return new ApiEnumMember
                {
                    Name = value,
                    CSharpName = NamingHelper.ToPascalCase(value),
                    Description = null,
                };
            })
            .ToList();

        return new ApiSchema
        {
            Name = name,
            OriginalName = originalName,
            Kind = SchemaKind.Enum,
            Description = openApiSchema.Description,
            EnumValues = members,
            IsNullable = IsNullable(openApiSchema),
            IsDeprecated = openApiSchema.Deprecated,
            Source = source,
        };
    }

    private ApiSchema TransformArray(IOpenApiSchema openApiSchema, string name, string originalName, string source)
    {
        ApiSchema? itemSchema = null;
        if (openApiSchema.Items != null)
        {
            itemSchema = GetOrTransformPropertySchema(openApiSchema.Items, name, "Item", $"{source}/items");
        }

        return new ApiSchema
        {
            Name = name,
            OriginalName = originalName,
            Kind = SchemaKind.Array,
            Description = openApiSchema.Description,
            ArrayItemSchema = itemSchema,
            IsNullable = IsNullable(openApiSchema),
            IsDeprecated = openApiSchema.Deprecated,
            Source = source,
        };
    }

    private ApiSchema TransformPrimitive(IOpenApiSchema openApiSchema, string name, string originalName, string source)
    {
        var primitiveType = MapPrimitiveType(openApiSchema.Type, openApiSchema.Format, originalName, source);

        return new ApiSchema
        {
            Name = name,
            OriginalName = originalName,
            Kind = SchemaKind.Primitive,
            Description = openApiSchema.Description,
            PrimitiveType = primitiveType,
            IsNullable = IsNullable(openApiSchema),
            IsDeprecated = openApiSchema.Deprecated,
            Source = source,
        };
    }

    private ApiSchema TransformAllOf(IOpenApiSchema openApiSchema, string name, string originalName, string source)
    {
        var schema = new ApiSchema
        {
            Name = name,
            OriginalName = originalName,
            Kind = SchemaKind.Object,
            Description = openApiSchema.Description,
            IsNullable = IsNullable(openApiSchema),
            IsDeprecated = openApiSchema.Deprecated,
            Source = source,
        };

        _schemaMap[openApiSchema] = schema;

        var mergedProperties = new Dictionary<string, ApiProperty>(StringComparer.Ordinal);
        var mergedRequired = new HashSet<string>(openApiSchema.Required ?? new HashSet<string>(), StringComparer.Ordinal);
        var refTargets = new List<ApiSchema>();
        var hasInlineProperties = false;

        foreach (var allOfEntry in openApiSchema.AllOf!)
        {
            var resolvedEntry = ResolveRef(allOfEntry);
            if (_componentSchemaNames.TryGetValue(resolvedEntry, out var refId))
            {
                var refSchema = GetOrTransformPropertySchema(allOfEntry, name, refId, source);
                refTargets.Add(refSchema);
                foreach (var prop in refSchema.Properties)
                {
                    if (mergedProperties.ContainsKey(prop.Name))
                    {
                        _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "AS201",
                            $"Property '{prop.Name}' from allOf entry conflicts with existing property in schema '{originalName}'. Keeping last.",
                            source));
                    }
                    mergedProperties[prop.Name] = prop;
                }

                foreach (var req in allOfEntry.Required ?? new HashSet<string>())
                    mergedRequired.Add(req);
            }
            else
            {
                foreach (var req in allOfEntry.Required ?? new HashSet<string>())
                    mergedRequired.Add(req);

                if (allOfEntry.Properties is { Count: > 0 })
                    hasInlineProperties = true;

                foreach (var (propName, propSchema) in allOfEntry.Properties ?? new Dictionary<string, IOpenApiSchema>())
                {
                    var propApiSchema = GetOrTransformPropertySchema(propSchema, name, propName, $"{source}/allOf/properties/{propName}");
                    if (mergedProperties.ContainsKey(propName))
                    {
                        _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "AS201",
                            $"Property '{propName}' from allOf entry conflicts with existing property in schema '{originalName}'. Keeping last.",
                            source));
                    }
                    mergedProperties[propName] = new ApiProperty
                    {
                        Name = propName,
                        CSharpName = NamingHelper.ToPascalCase(propName),
                        Schema = propApiSchema,
                        IsRequired = mergedRequired.Contains(propName),
                        IsDeprecated = propSchema.Deprecated,
                        Description = propSchema.Description,
                        DefaultValue = propSchema.Default?.ToString(),
                    };
                }
            }
        }

        foreach (var prop in mergedProperties.Values)
        {
            if (mergedRequired.Contains(prop.Name))
                prop.IsRequired = true;
        }

        schema.Properties = mergedProperties.Values.ToList();

        if (refTargets.Count == 1)
        {
            schema.AllOfRefTarget = refTargets[0];
            schema.HasAllOfInlineProperties = hasInlineProperties;
        }

        return schema;
    }

    private ApiSchema GetOrTransformPropertySchema(IOpenApiSchema propSchema, string parentName, string propertyName, string source)
    {
        var resolved = ResolveRef(propSchema);

        if (_schemaMap.TryGetValue(resolved, out var existing))
            return existing;

        if (_componentSchemaNames.TryGetValue(resolved, out var componentName))
            return TransformSchema(resolved, componentName, $"#/components/schemas/{componentName}");

        if (GetBaseType(resolved) == JsonSchemaType.Object && resolved.Properties is { Count: > 0 } && !_componentSchemaNames.ContainsKey(resolved))
        {
            var hoistedName = $"{parentName}{NamingHelper.ToPascalCase(propertyName)}";
            var resolvedName = NamingHelper.ResolveCollision(hoistedName, _usedNames);

            if (resolvedName != hoistedName)
            {
                _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "AS202",
                    $"Inline schema name '{hoistedName}' collides with existing schema. Renamed to '{resolvedName}'.",
                    source));
            }

            var schema = TransformObject(resolved, resolvedName, propertyName, source);
            _schemaMap[resolved] = schema;
            _allSchemas.Add(schema);
            return schema;
        }

        if (resolved.AllOf is { Count: > 0 } && !_componentSchemaNames.ContainsKey(resolved))
        {
            var hoistedName = $"{parentName}{NamingHelper.ToPascalCase(propertyName)}";
            var resolvedName = NamingHelper.ResolveCollision(hoistedName, _usedNames);
            var schema = TransformAllOf(resolved, resolvedName, propertyName, source);
            _schemaMap[resolved] = schema;
            _allSchemas.Add(schema);
            return schema;
        }

        if (GetBaseType(resolved) == JsonSchemaType.Array)
        {
            ApiSchema? itemSchema = null;
            if (resolved.Items != null)
                itemSchema = GetOrTransformPropertySchema(resolved.Items, parentName, $"{propertyName}Item", $"{source}/items");

            var arraySchema = new ApiSchema
            {
                Name = $"{parentName}{NamingHelper.ToPascalCase(propertyName)}",
                OriginalName = propertyName,
                Kind = SchemaKind.Array,
                ArrayItemSchema = itemSchema,
                IsNullable = IsNullable(resolved),
                IsDeprecated = resolved.Deprecated,
                Source = source,
            };
            _schemaMap[resolved] = arraySchema;
            return arraySchema;
        }

        if (resolved.Enum is { Count: > 0 } && !_componentSchemaNames.ContainsKey(resolved))
        {
            var hoistedName = $"{parentName}{NamingHelper.ToPascalCase(propertyName)}";
            var resolvedName = NamingHelper.ResolveCollision(hoistedName, _usedNames);
            var schema = TransformEnum(resolved, resolvedName, propertyName, source);
            _schemaMap[resolved] = schema;
            _allSchemas.Add(schema);
            return schema;
        }

        var primitiveType = MapPrimitiveType(resolved.Type, resolved.Format, propertyName, source);
        var primitiveSchema = new ApiSchema
        {
            Name = propertyName,
            OriginalName = propertyName,
            Kind = SchemaKind.Primitive,
            PrimitiveType = primitiveType,
            IsNullable = IsNullable(resolved),
            IsDeprecated = resolved.Deprecated,
            Description = resolved.Description,
            Source = source,
        };
        _schemaMap[resolved] = primitiveSchema;
        return primitiveSchema;
    }

    private PrimitiveType MapPrimitiveType(JsonSchemaType? type, string? format, string context, string source)
    {
        var baseType = type.HasValue ? (type.Value & ~JsonSchemaType.Null) : (JsonSchemaType?)null;
        return (baseType, format) switch
        {
            (JsonSchemaType.String, "date-time") => Model.PrimitiveType.DateTimeOffset,
            (JsonSchemaType.String, "date") => Model.PrimitiveType.DateOnly,
            (JsonSchemaType.String, "time") => Model.PrimitiveType.TimeOnly,
            (JsonSchemaType.String, "duration") => Model.PrimitiveType.TimeSpan,
            (JsonSchemaType.String, "uuid") => Model.PrimitiveType.Guid,
            (JsonSchemaType.String, "uri") => Model.PrimitiveType.Uri,
            (JsonSchemaType.String, "byte") => Model.PrimitiveType.ByteArray,
            (JsonSchemaType.String, "binary") => Model.PrimitiveType.ByteArray,
            (JsonSchemaType.String, null or "") => Model.PrimitiveType.String,
            (JsonSchemaType.String, _) => HandleUnknownFormat(format!, context, source),
            (JsonSchemaType.Integer, "int64") => Model.PrimitiveType.Int64,
            (JsonSchemaType.Integer, _) => Model.PrimitiveType.Int32,
            (JsonSchemaType.Number, "float") => Model.PrimitiveType.Float,
            (JsonSchemaType.Number, "decimal") => Model.PrimitiveType.Decimal,
            (JsonSchemaType.Number, _) => Model.PrimitiveType.Double,
            (JsonSchemaType.Boolean, _) => Model.PrimitiveType.Bool,
            _ => Model.PrimitiveType.String,
        };
    }

    private PrimitiveType HandleUnknownFormat(string format, string context, string source)
    {
        _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "AS204",
            $"Unknown string format '{format}' on '{context}'. Treated as string.",
            source));
        return Model.PrimitiveType.String;
    }

    private void DetectCircularReferences()
    {
        var visited = new HashSet<ApiSchema>(ReferenceEqualityComparer.Instance);
        var inStack = new HashSet<ApiSchema>(ReferenceEqualityComparer.Instance);

        foreach (var schema in _allSchemas.Where(s => s.Kind == SchemaKind.Object).OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            if (!visited.Contains(schema))
                DfsDetectCycles(schema, visited, inStack);
        }
    }

    private void DfsDetectCycles(ApiSchema schema, HashSet<ApiSchema> visited, HashSet<ApiSchema> inStack)
    {
        visited.Add(schema);
        inStack.Add(schema);

        foreach (var property in schema.Properties)
        {
            var targetSchema = property.Schema;
            if (targetSchema.Kind == SchemaKind.Array)
                targetSchema = targetSchema.ArrayItemSchema;

            if (targetSchema == null || targetSchema.Kind != SchemaKind.Object)
                continue;

            if (inStack.Contains(targetSchema))
            {
                property.IsRequired = false;
                _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "AS003",
                    $"Circular reference detected between '{schema.Name}' and '{targetSchema.Name}'. Property '{schema.Name}.{property.CSharpName}' relaxed to optional to break cycle.",
                    schema.Source));
                continue;
            }

            if (!visited.Contains(targetSchema))
                DfsDetectCycles(targetSchema, visited, inStack);
        }

        inStack.Remove(schema);
    }
}
