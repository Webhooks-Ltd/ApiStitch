using Microsoft.OpenApi;

namespace ApiStitch.Parsing;

internal static class OpenApiSchemaHelpers
{
    internal static IOpenApiSchema ResolveRef(IOpenApiSchema schema) =>
        schema is OpenApiSchemaReference schemaRef ? schemaRef.Target! : schema;

    internal static JsonSchemaType? GetBaseType(IOpenApiSchema schema) =>
        schema.Type.HasValue ? (schema.Type.Value & ~JsonSchemaType.Null) : null;

    internal static bool IsNullable(IOpenApiSchema schema) =>
        schema.Type?.HasFlag(JsonSchemaType.Null) == true;
}
