namespace SampleApi.Models;

public class Pet
{
    public required int Id { get; init; }
    public required string Name { get; set; }
    public string? Tag { get; set; }
    public required PetStatus Status { get; set; }
}
