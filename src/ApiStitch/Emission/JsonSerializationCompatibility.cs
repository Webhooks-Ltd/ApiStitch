using ApiStitch.Model;

namespace ApiStitch.Emission;

internal static class JsonSerializationCompatibility
{
    public static bool ShouldUseGeneratedJsonOptions(ApiSchema schema)
    {
        return !ContainsUnsupportedSourceGenerationType(schema);
    }

    public static bool ShouldGenerateJsonMetadata(ApiSchema schema)
    {
        return !ContainsUnsupportedSourceGenerationType(schema);
    }

    public static bool ContainsUnsupportedSourceGenerationType(ApiSchema schema)
    {
        return ContainsUnsupportedSourceGenerationType(schema, new HashSet<ApiSchema>(ReferenceEqualityComparer.Instance));
    }

    private static bool ContainsUnsupportedSourceGenerationType(ApiSchema schema, HashSet<ApiSchema> visited)
    {
        if (!visited.Add(schema))
            return false;

        if (schema.HasUnrepresentableComposition)
            return true;

        if (schema.ExternalTypeKind == ExternalTypeKind.JsonPatchDocument)
            return true;

        if (schema.ArrayItemSchema is not null && ContainsUnsupportedSourceGenerationType(schema.ArrayItemSchema, visited))
            return true;

        if (schema.AdditionalPropertiesSchema is not null && ContainsUnsupportedSourceGenerationType(schema.AdditionalPropertiesSchema, visited))
            return true;

        if (schema.BaseSchema is not null && ContainsUnsupportedSourceGenerationType(schema.BaseSchema, visited))
            return true;

        if (schema.AllOfRefTarget is not null && ContainsUnsupportedSourceGenerationType(schema.AllOfRefTarget, visited))
            return true;

        foreach (var property in schema.Properties)
        {
            if (ContainsUnsupportedSourceGenerationType(property.Schema, visited))
                return true;
        }

        return false;
    }
}
