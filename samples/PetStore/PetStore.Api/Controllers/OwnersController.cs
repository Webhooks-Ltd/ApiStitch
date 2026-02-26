using Microsoft.AspNetCore.Mvc;
using PetStore.SharedModels;

namespace PetStore.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Tags("Owners")]
public class OwnersController : ControllerBase
{
    private static readonly List<Owner> Owners =
    [
        new() { Id = 1, Name = "Alice", Email = "alice@example.com" },
        new() { Id = 2, Name = "Bob" },
    ];

    [HttpGet(Name = "ListOwners")]
    public IReadOnlyList<Owner> List() => Owners;

    [HttpGet("{id:int}", Name = "GetOwner")]
    public ActionResult<Owner> Get(int id) =>
        Owners.FirstOrDefault(o => o.Id == id) is { } owner
            ? Ok(owner)
            : NotFound();
}
