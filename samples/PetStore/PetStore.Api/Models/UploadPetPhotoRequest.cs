namespace PetStore.Api.Models;

public class UploadPetPhotoRequest
{
    public required IFormFile Photo { get; init; }
    public IFormFile? Thumbnail { get; init; }
    public string? Description { get; init; }
}
