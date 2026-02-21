using ApiStitch.Diagnostics;

namespace ApiStitch.Model;

public class ApiSchema
{
    public required string Name { get; set; }
    public required string OriginalName { get; init; }
    public string? Description { get; init; }
    public required SchemaKind Kind { get; init; }
    public IReadOnlyList<ApiProperty> Properties { get; set; } = [];
    public IReadOnlyList<ApiEnumMember> EnumValues { get; init; } = [];
    public ApiSchema? ArrayItemSchema { get; init; }
    public ApiSchema? BaseSchema { get; internal set; }
    public PrimitiveType? PrimitiveType { get; init; }
    public bool IsNullable { get; init; }
    public bool IsDeprecated { get; init; }
    public bool HasAdditionalProperties { get; init; }
    public ApiSchema? AdditionalPropertiesSchema { get; init; }
    public string? CSharpTypeName { get; set; }
    public string? Source { get; init; }
    public List<Diagnostic> Diagnostics { get; } = [];
    internal ApiSchema? AllOfRefTarget { get; set; }
    internal bool HasAllOfInlineProperties { get; set; }
}
