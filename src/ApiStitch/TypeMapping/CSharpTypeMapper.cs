using ApiStitch.Model;

namespace ApiStitch.TypeMapping;

public static class CSharpTypeMapper
{
    public static void MapAll(ApiSpecification specification)
    {
        foreach (var schema in specification.Schemas)
        {
            if (schema.IsExternal)
                schema.CSharpTypeName = schema.ExternalClrTypeName;
            else
                schema.CSharpTypeName = MapSchema(schema);
        }
    }

    internal static string MapSchema(ApiSchema schema)
    {
        if (schema.IsExternal)
            return schema.ExternalClrTypeName!;

        return schema.Kind switch
        {
            SchemaKind.Primitive => MapPrimitive(schema.PrimitiveType ?? PrimitiveType.String),
            SchemaKind.Enum => schema.Name,
            SchemaKind.Object => schema.Name,
            SchemaKind.Array => schema.ArrayItemSchema != null
                ? $"IReadOnlyList<{MapSchema(schema.ArrayItemSchema)}>"
                : "IReadOnlyList<object>",
            _ => "object"
        };
    }

    internal static string MapPrimitive(PrimitiveType primitiveType)
    {
        return primitiveType switch
        {
            PrimitiveType.String => "string",
            PrimitiveType.Int32 => "int",
            PrimitiveType.Int64 => "long",
            PrimitiveType.Float => "float",
            PrimitiveType.Double => "double",
            PrimitiveType.Decimal => "decimal",
            PrimitiveType.Bool => "bool",
            PrimitiveType.DateTimeOffset => "DateTimeOffset",
            PrimitiveType.DateOnly => "DateOnly",
            PrimitiveType.TimeOnly => "TimeOnly",
            PrimitiveType.TimeSpan => "TimeSpan",
            PrimitiveType.Guid => "Guid",
            PrimitiveType.Uri => "Uri",
            PrimitiveType.ByteArray => "byte[]",
            _ => "object"
        };
    }

    internal static bool IsValueType(PrimitiveType primitiveType)
    {
        return primitiveType switch
        {
            PrimitiveType.String => false,
            PrimitiveType.Uri => false,
            PrimitiveType.ByteArray => false,
            _ => true
        };
    }
}
