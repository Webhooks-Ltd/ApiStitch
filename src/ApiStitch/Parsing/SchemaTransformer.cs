using ApiStitch.Diagnostics;
using ApiStitch.Model;
using Microsoft.OpenApi.Models;

namespace ApiStitch.Parsing;

public class SchemaTransformer
{
    private readonly List<Diagnostic> _diagnostics = [];
    private readonly Dictionary<OpenApiSchema, ApiSchema> _schemaMap = new(ReferenceEqualityComparer.Instance);
    private readonly List<ApiSchema> _allSchemas = [];
    private readonly HashSet<string> _usedNames = new(StringComparer.Ordinal);
    private readonly Dictionary<OpenApiSchema, string> _componentSchemaNames = new(ReferenceEqualityComparer.Instance);

    public (ApiSpecification Specification, IReadOnlyList<Diagnostic> Diagnostics) Transform(OpenApiDocument document)
    {
        var componentSchemas = document.Components?.Schemas ?? new Dictionary<string, OpenApiSchema>();

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

        return (spec, _diagnostics);
    }

    private ApiSchema TransformSchema(OpenApiSchema openApiSchema, string originalName, string source)
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
        else if (openApiSchema.Type == "array")
        {
            schema = TransformArray(openApiSchema, resolvedName, originalName, source);
        }
        else if (openApiSchema.Enum is { Count: > 0 })
        {
            schema = TransformEnum(openApiSchema, resolvedName, originalName, source);
        }
        else if (openApiSchema.Type == "object" || openApiSchema.Properties is { Count: > 0 })
        {
            schema = TransformObject(openApiSchema, resolvedName, originalName, source);
        }
        else
        {
            schema = TransformPrimitive(openApiSchema, resolvedName, originalName, source);
        }

        _schemaMap[openApiSchema] = schema;
        _allSchemas.Add(schema);
        return schema;
    }

    private ApiSchema TransformObject(OpenApiSchema openApiSchema, string name, string originalName, string source)
    {
        var schema = new ApiSchema
        {
            Name = name,
            OriginalName = originalName,
            Kind = SchemaKind.Object,
            Description = openApiSchema.Description,
            IsNullable = openApiSchema.Nullable,
            IsDeprecated = openApiSchema.Deprecated,
            HasAdditionalProperties = openApiSchema.AdditionalProperties != null,
            AdditionalPropertiesSchema = openApiSchema.AdditionalProperties?.Type != null
                ? GetOrTransformPropertySchema(openApiSchema.AdditionalProperties, name, "additionalProperties", source)
                : null,
            Source = source,
        };

        _schemaMap[openApiSchema] = schema;

        var required = new HashSet<string>(openApiSchema.Required ?? (ISet<string>)new HashSet<string>(), StringComparer.Ordinal);
        var properties = new List<ApiProperty>();

        foreach (var (propName, propSchema) in openApiSchema.Properties ?? Enumerable.Empty<KeyValuePair<string, OpenApiSchema>>())
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

    private ApiSchema TransformEnum(OpenApiSchema openApiSchema, string name, string originalName, string source)
    {
        if (openApiSchema.Type != "string")
        {
            _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "AS200",
                $"Schema '{originalName}' is a {openApiSchema.Type} enum. Only string enums are supported; treating as primitive type.",
                source));
            return TransformPrimitive(openApiSchema, name, originalName, source);
        }

        var members = openApiSchema.Enum
            .Select(e =>
            {
                var value = (e as Microsoft.OpenApi.Any.OpenApiString)?.Value ?? e.ToString() ?? "";
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
            IsNullable = openApiSchema.Nullable,
            IsDeprecated = openApiSchema.Deprecated,
            Source = source,
        };
    }

    private ApiSchema TransformArray(OpenApiSchema openApiSchema, string name, string originalName, string source)
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
            IsNullable = openApiSchema.Nullable,
            IsDeprecated = openApiSchema.Deprecated,
            Source = source,
        };
    }

    private ApiSchema TransformPrimitive(OpenApiSchema openApiSchema, string name, string originalName, string source)
    {
        var primitiveType = MapPrimitiveType(openApiSchema.Type, openApiSchema.Format, originalName, source);

        return new ApiSchema
        {
            Name = name,
            OriginalName = originalName,
            Kind = SchemaKind.Primitive,
            Description = openApiSchema.Description,
            PrimitiveType = primitiveType,
            IsNullable = openApiSchema.Nullable,
            IsDeprecated = openApiSchema.Deprecated,
            Source = source,
        };
    }

    private ApiSchema TransformAllOf(OpenApiSchema openApiSchema, string name, string originalName, string source)
    {
        var schema = new ApiSchema
        {
            Name = name,
            OriginalName = originalName,
            Kind = SchemaKind.Object,
            Description = openApiSchema.Description,
            IsNullable = openApiSchema.Nullable,
            IsDeprecated = openApiSchema.Deprecated,
            Source = source,
        };

        _schemaMap[openApiSchema] = schema;

        var mergedProperties = new Dictionary<string, ApiProperty>(StringComparer.Ordinal);
        var mergedRequired = new HashSet<string>(openApiSchema.Required ?? (ISet<string>)new HashSet<string>(), StringComparer.Ordinal);
        var refTargets = new List<ApiSchema>();
        var hasInlineProperties = false;

        foreach (var allOfEntry in openApiSchema.AllOf)
        {
            if (allOfEntry.Reference != null)
            {
                var refSchema = GetOrTransformPropertySchema(allOfEntry, name, allOfEntry.Reference.Id, source);
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

                foreach (var req in allOfEntry.Required ?? (ISet<string>)new HashSet<string>())
                    mergedRequired.Add(req);
            }
            else
            {
                foreach (var req in allOfEntry.Required ?? (ISet<string>)new HashSet<string>())
                    mergedRequired.Add(req);

                if (allOfEntry.Properties is { Count: > 0 })
                    hasInlineProperties = true;

                foreach (var (propName, propSchema) in allOfEntry.Properties ?? Enumerable.Empty<KeyValuePair<string, OpenApiSchema>>())
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

    private ApiSchema GetOrTransformPropertySchema(OpenApiSchema propSchema, string parentName, string propertyName, string source)
    {
        if (_schemaMap.TryGetValue(propSchema, out var existing))
            return existing;

        if (_componentSchemaNames.TryGetValue(propSchema, out var componentName))
            return TransformSchema(propSchema, componentName, $"#/components/schemas/{componentName}");

        if (propSchema.Type == "object" && propSchema.Properties is { Count: > 0 } && propSchema.Reference == null)
        {
            var hoistedName = $"{parentName}{NamingHelper.ToPascalCase(propertyName)}";
            var resolvedName = NamingHelper.ResolveCollision(hoistedName, _usedNames);

            if (resolvedName != hoistedName)
            {
                _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "AS202",
                    $"Inline schema name '{hoistedName}' collides with existing schema. Renamed to '{resolvedName}'.",
                    source));
            }

            var schema = TransformObject(propSchema, resolvedName, propertyName, source);
            _schemaMap[propSchema] = schema;
            _allSchemas.Add(schema);
            return schema;
        }

        if (propSchema.AllOf is { Count: > 0 } && propSchema.Reference == null)
        {
            var hoistedName = $"{parentName}{NamingHelper.ToPascalCase(propertyName)}";
            var resolvedName = NamingHelper.ResolveCollision(hoistedName, _usedNames);
            var schema = TransformAllOf(propSchema, resolvedName, propertyName, source);
            _schemaMap[propSchema] = schema;
            _allSchemas.Add(schema);
            return schema;
        }

        if (propSchema.Type == "array")
        {
            ApiSchema? itemSchema = null;
            if (propSchema.Items != null)
                itemSchema = GetOrTransformPropertySchema(propSchema.Items, parentName, $"{propertyName}Item", $"{source}/items");

            var arraySchema = new ApiSchema
            {
                Name = $"{parentName}{NamingHelper.ToPascalCase(propertyName)}",
                OriginalName = propertyName,
                Kind = SchemaKind.Array,
                ArrayItemSchema = itemSchema,
                IsNullable = propSchema.Nullable,
                IsDeprecated = propSchema.Deprecated,
                Source = source,
            };
            _schemaMap[propSchema] = arraySchema;
            return arraySchema;
        }

        if (propSchema.Enum is { Count: > 0 } && propSchema.Reference == null)
        {
            var hoistedName = $"{parentName}{NamingHelper.ToPascalCase(propertyName)}";
            var resolvedName = NamingHelper.ResolveCollision(hoistedName, _usedNames);
            var schema = TransformEnum(propSchema, resolvedName, propertyName, source);
            _schemaMap[propSchema] = schema;
            _allSchemas.Add(schema);
            return schema;
        }

        var primitiveType = MapPrimitiveType(propSchema.Type, propSchema.Format, propertyName, source);
        var primitiveSchema = new ApiSchema
        {
            Name = propertyName,
            OriginalName = propertyName,
            Kind = SchemaKind.Primitive,
            PrimitiveType = primitiveType,
            IsNullable = propSchema.Nullable,
            IsDeprecated = propSchema.Deprecated,
            Description = propSchema.Description,
            Source = source,
        };
        _schemaMap[propSchema] = primitiveSchema;
        return primitiveSchema;
    }

    private PrimitiveType MapPrimitiveType(string? type, string? format, string context, string source)
    {
        return (type, format) switch
        {
            ("string", "date-time") => Model.PrimitiveType.DateTimeOffset,
            ("string", "date") => Model.PrimitiveType.DateOnly,
            ("string", "time") => Model.PrimitiveType.TimeOnly,
            ("string", "duration") => Model.PrimitiveType.TimeSpan,
            ("string", "uuid") => Model.PrimitiveType.Guid,
            ("string", "uri") => Model.PrimitiveType.Uri,
            ("string", "byte") => Model.PrimitiveType.ByteArray,
            ("string", "binary") => Model.PrimitiveType.ByteArray,
            ("string", null or "") => Model.PrimitiveType.String,
            ("string", _) => HandleUnknownFormat(format!, context, source),
            ("integer", "int64") => Model.PrimitiveType.Int64,
            ("integer", _) => Model.PrimitiveType.Int32,
            ("number", "float") => Model.PrimitiveType.Float,
            ("number", "decimal") => Model.PrimitiveType.Decimal,
            ("number", _) => Model.PrimitiveType.Double,
            ("boolean", _) => Model.PrimitiveType.Bool,
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
