namespace PetStore.Api.Models;

public class PetAvatar
{
    public required byte[] ImageData { get; init; }
    public required string MimeType { get; init; }
}
