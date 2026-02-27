using ApiStitch.OpenApi;
using Microsoft.AspNetCore.Mvc;
using PetStore.Api.Models;
using PetStore.SharedModels;

var builder = WebApplication.CreateBuilder(args);

if (!ApiStitchDetection.IsOpenApiGenerationOnly)
{
    // Heavy dependencies (database, auth, etc.) can be skipped during build-time spec generation.
}

builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddApiStitchTypeInfo());

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapControllers();

// ── Pets (Minimal API) ──

var pets = new List<Pet>
{
    new() { Id = 1, Name = "Fido", Tag = "dog", Status = PetStatus.Available },
    new() { Id = 2, Name = "Whiskers", Tag = "cat", Status = PetStatus.Adopted },
};

app.MapGet("/pets", () => pets)
    .WithName("ListPets")
    .WithTags("Pets");

app.MapGet("/pets/{id:int}", (int id) =>
    pets.FirstOrDefault(p => p.Id == id) is { } pet
        ? Results.Ok(pet)
        : Results.NotFound())
    .WithName("GetPet")
    .WithTags("Pets");

app.MapPost("/pets", (CreatePetRequest request) =>
{
    var pet = new Pet
    {
        Id = pets.Max(p => p.Id) + 1,
        Name = request.Name,
        Tag = request.Tag,
        Status = PetStatus.Available,
    };
    pets.Add(pet);
    return Results.Created($"/pets/{pet.Id}", pet);
})
    .WithName("CreatePet")
    .WithTags("Pets");

app.MapPost("/pets/{id:int}/avatar", (int id, PetAvatar avatar) => Results.Ok(avatar))
    .WithName("SetPetAvatar")
    .WithTags("Pets");

app.MapGet("/pets/{id:int}/avatar", (int id) => Results.Ok(new PetAvatar
{
    ImageData = [0xFF, 0xD8],
    MimeType = "image/jpeg",
}))
    .WithName("GetPetAvatar")
    .WithTags("Pets");

app.MapGet("/pets/search", (
    [FromQuery(Name = "tags")] string[]? tags,
    [FromQuery(Name = "status")] PetStatus? status) =>
{
    IEnumerable<Pet> result = pets;
    if (tags is { Length: > 0 })
        result = result.Where(p => p.Tag is not null && tags.Contains(p.Tag));
    if (status is not null)
        result = result.Where(p => p.Status == status);
    return result.ToList();
})
    .WithName("SearchPets")
    .WithTags("Pets");

// ── Files (demonstrates multipart upload, octet-stream download) ──

var files = new Dictionary<string, byte[]>();

app.MapPost("/pets/{id:int}/photo", async (int id, [FromForm] UploadPetPhotoRequest request) =>
{
    using var ms = new MemoryStream();
    await request.Photo.CopyToAsync(ms);
    files[$"pet-{id}"] = ms.ToArray();
    return Results.Ok(new
    {
        id,
        photoName = request.Photo.FileName,
        photoSize = ms.Length,
        hasThumbnail = request.Thumbnail is not null,
        description = request.Description,
    });
})
    .WithName("UploadPetPhoto")
    .WithTags("Pets")
    .DisableAntiforgery();

app.MapGet("/pets/{id:int}/photo", (int id) =>
{
    if (!files.TryGetValue($"pet-{id}", out var data))
        return Results.NotFound();
    return Results.File(data, "application/octet-stream", $"pet-{id}.jpg");
})
    .WithName("DownloadPetPhoto")
    .WithTags("Pets")
    .Produces<Stream>(200, "application/octet-stream");

// ── Health (demonstrates text/plain response) ──

app.MapGet("/health", () => Results.Text("healthy"))
    .WithName("GetHealth")
    .WithTags("System")
    .Produces<string>(200, "text/plain");

app.Run();
