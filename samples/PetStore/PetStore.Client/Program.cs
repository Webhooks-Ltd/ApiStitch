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

// ── List pets ──
Console.WriteLine("=== Pets ===");
var pets = await petsClient.ListPetsAsync();
foreach (var pet in pets)
    Console.WriteLine($"  [{pet.Id}] {pet.Name} (tag: {pet.Tag}, status: {pet.Status})");

// ── Create a pet ──
Console.WriteLine("\nCreating pet 'Buddy'...");
await petsClient.CreatePetAsync(new CreatePetRequest { Name = "Buddy", Tag = "dog" });

// ── List again to see the new pet ──
Console.WriteLine("\n=== Pets (after create) ===");
pets = await petsClient.ListPetsAsync();
foreach (var pet in pets)
    Console.WriteLine($"  [{pet.Id}] {pet.Name} (tag: {pet.Tag}, status: {pet.Status})");

// ── Get single pet ──
Console.WriteLine("\nFetching pet 1...");
await petsClient.GetPetAsync(1);
Console.WriteLine("  OK (no response body for this endpoint)");

// ── List owners ──
Console.WriteLine("\n=== Owners ===");
var owners = await ownersClient.ListOwnersAsync();
foreach (var owner in owners)
    Console.WriteLine($"  [{owner.Id}] {owner.Name} ({owner.Email ?? "no email"})");

// ── Get single owner ──
Console.WriteLine("\nFetching owner 1...");
var alice = await ownersClient.GetOwnerAsync(1);
Console.WriteLine($"  {alice.Name} — {alice.Email}");
