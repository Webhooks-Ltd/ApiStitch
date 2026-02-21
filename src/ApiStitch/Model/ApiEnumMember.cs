namespace ApiStitch.Model;

public class ApiEnumMember
{
    public required string Name { get; init; }
    public required string CSharpName { get; init; }
    public string? Description { get; init; }
}
