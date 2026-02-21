namespace ApiStitch.Model;

public class ApiProperty
{
    public required string Name { get; init; }
    public required string CSharpName { get; init; }
    public required ApiSchema Schema { get; init; }
    public bool IsRequired { get; set; }
    public bool IsNullable => !IsRequired || Schema.IsNullable;
    public bool IsDeprecated { get; init; }
    public string? Description { get; init; }
    public string? DefaultValue { get; init; }
}
