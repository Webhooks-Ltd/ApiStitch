using ApiStitch.OpenApi;
using SampleApi.Models;

var builder = WebApplication.CreateBuilder(args);

if (!ApiStitchDetection.IsOpenApiGenerationOnly)
{
    // Heavy dependencies (database, auth, etc.) can be skipped during build-time spec generation.
    // builder.Services.AddDbContext<AppDbContext>(...);
}

builder.Services.AddOpenApi(options => options.AddApiStitchTypeInfo());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

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

app.Run();
