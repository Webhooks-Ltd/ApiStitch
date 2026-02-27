using Microsoft.Extensions.DependencyInjection;
using PetStore.Client.Generated;
using PetStore.SharedModels;

var services = new ServiceCollection();

services.AddPetStoreApi(options =>
{
    options.BaseAddress = new Uri("http://localhost:5000");
});

var provider = services.BuildServiceProvider();

var petsClient = provider.GetRequiredService<IPetStoreApiPetsClient>();
var ownersClient = provider.GetRequiredService<IPetStoreApiOwnersClient>();
var systemClient = provider.GetRequiredService<IPetStoreApiSystemClient>();

// ── Health check (text/plain response → Task<string>) ──
Console.WriteLine("=== Health Check ===");
var health = await systemClient.GetHealthAsync();
Console.WriteLine($"  Status: {health}");

// ── List pets (JSON response with Content-Type validation + Accept header) ──
Console.WriteLine("\n=== Pets ===");
var pets = await petsClient.ListPetsAsync();
foreach (var pet in pets)
    Console.WriteLine($"  [{pet.Id}] {pet.Name} (tag: {pet.Tag}, status: {pet.Status})");

// ── Create a pet (JSON request body) ──
Console.WriteLine("\nCreating pet 'Buddy'...");
await petsClient.CreatePetAsync(new CreatePetRequest { Name = "Buddy", Tag = "dog" });

// ── Search pets with array query params (explode: true → repeated key=value) ──
Console.WriteLine("\n=== Search by tags: dog, cat ===");
var filtered = await petsClient.SearchPetsAsync(tags: ["dog", "cat"]);
foreach (var pet in filtered)
    Console.WriteLine($"  [{pet.Id}] {pet.Name} ({pet.Tag})");

// ── Search with enum query param ──
Console.WriteLine("\n=== Search by status: Available ===");
var available = await petsClient.SearchPetsAsync(status: PetStatus.Available);
foreach (var pet in available)
    Console.WriteLine($"  [{pet.Id}] {pet.Name} ({pet.Status})");

// ── Upload pet photo (multipart/form-data → binary + optional binary + text parts) ──
Console.WriteLine("\nUploading photo for pet 1...");
var photoBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // fake JPEG header
var thumbBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }; // fake thumbnail
await petsClient.UploadPetPhotoAsync(
    id: 1,
    photo: new MemoryStream(photoBytes),
    photoFileName: "fido.jpg",
    thumbnail: new MemoryStream(thumbBytes),
    thumbnailFileName: "fido-thumb.jpg",
    description: "Fido at the park");
Console.WriteLine("  Upload complete");

// ── Set pet avatar (JSON body with byte[] → base64 encoded) ──
Console.WriteLine("\nSetting avatar for pet 1...");
await petsClient.SetPetAvatarAsync(id: 1, new PetAvatar
{
    ImageData = [0x89, 0x50, 0x4E, 0x47], // PNG magic bytes
    MimeType = "image/png",
});
Console.WriteLine("  Avatar set (byte[] serialized as base64 in JSON)");

// ── Download pet photo (octet-stream response → FileResponse with streaming) ──
Console.WriteLine("\nDownloading photo for pet 1...");
await using var fileResponse = await petsClient.DownloadPetPhotoAsync(id: 1);
Console.WriteLine($"  Content-Type: {fileResponse.ContentType}");
Console.WriteLine($"  File name: {fileResponse.FileName}");
Console.WriteLine($"  Content length: {fileResponse.ContentLength}");

// ── List owners (JSON with type reuse → PetStore.SharedModels.Owner) ──
Console.WriteLine("\n=== Owners ===");
var owners = await ownersClient.ListOwnersAsync();
foreach (var owner in owners)
    Console.WriteLine($"  [{owner.Id}] {owner.Name} ({owner.Email ?? "no email"})");

// ── Error handling with ProblemDetails ──
Console.WriteLine("\n=== Error Handling ===");
try
{
    await petsClient.GetPetAsync(999);
}
catch (ApiException ex) when (ex.Problem is not null)
{
    Console.WriteLine($"  ProblemDetails: {ex.Problem.Title} ({ex.Problem.Status})");
}
catch (ApiException ex)
{
    Console.WriteLine($"  API error: {ex.StatusCode} — {ex.ResponseBody?[..Math.Min(100, ex.ResponseBody.Length)]}");
}
