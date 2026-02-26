namespace SampleApi.Models;

public class CreatePetRequest
{
    public required string Name { get; init; }
    public string? Tag { get; init; }
}
