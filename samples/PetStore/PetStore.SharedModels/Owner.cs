namespace PetStore.SharedModels;

public class Owner
{
    public required int Id { get; init; }
    public required string Name { get; set; }
    public string? Email { get; set; }
}
