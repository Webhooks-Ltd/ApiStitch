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

    internal static bool HasUnrepresentableCompositionKeywords(IOpenApiSchema schema) =>
        schema.OneOf is { Count: > 0 }
        || schema.AnyOf is { Count: > 0 }
        || schema.Not is not null;
}
