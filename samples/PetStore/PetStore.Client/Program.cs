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

Console.WriteLine("Listing pets...");
var pets = await petsClient.ListPetsAsync();
foreach (var pet in pets)
    Console.WriteLine($"  {pet.Id}: {pet.Name} ({pet.Status})");

Console.WriteLine("\nCreating a new pet...");
await petsClient.CreatePetAsync(new CreatePetRequest { Name = "Buddy", Tag = "dog" });

Console.WriteLine("\nListing owners...");
var owners = await ownersClient.ListOwnersAsync();
foreach (var owner in owners)
    Console.WriteLine($"  {owner.Id}: {owner.Name} ({owner.Email ?? "no email"})");
