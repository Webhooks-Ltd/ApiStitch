using ApiStitch.OpenApi;
using PetStore.Api.Models;
using PetStore.SharedModels;

var builder = WebApplication.CreateBuilder(args);

if (!ApiStitchDetection.IsOpenApiGenerationOnly)
{
    // Heavy dependencies (database, auth, etc.) can be skipped during build-time spec generation.
}

builder.Services.AddOpenApi(options => options.AddApiStitchTypeInfo());

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// ── Pets ──

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

// ── Owners ──

var owners = new List<Owner>
{
    new() { Id = 1, Name = "Alice", Email = "alice@example.com" },
    new() { Id = 2, Name = "Bob" },
};

app.MapGet("/owners", () => owners)
    .WithName("ListOwners")
    .WithTags("Owners");

app.MapGet("/owners/{id:int}", (int id) =>
    owners.FirstOrDefault(o => o.Id == id) is { } owner
        ? Results.Ok(owner)
        : Results.NotFound())
    .WithName("GetOwner")
    .WithTags("Owners");

app.Run();
