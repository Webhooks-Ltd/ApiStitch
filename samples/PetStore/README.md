# PetStore Sample

End-to-end demonstration of ApiStitch's type reuse workflow: a producer API annotates its OpenAPI spec with CLR type metadata, and a consumer client reuses shared domain types instead of generating duplicates.

## Project structure

```
PetStore/
  PetStore.SharedModels/        Shared domain types (Pet, PetStatus, Owner)
  PetStore.Api/                 ASP.NET Core minimal API (producer)
    Models/CreatePetRequest.cs  API-specific DTO (not shared)
  PetStore.Client/              Console app (consumer)
    Generated/                  ApiStitch output
  PetStore.Api.json             Auto-generated OpenAPI spec
  openapi-stitch.yaml           ApiStitch configuration
```

### PetStore.SharedModels

Contains domain types shared between producer and consumer:

- `Pet` -- core entity with Id, Name, Tag, Status
- `PetStatus` -- enum (Available, Pending, Adopted)
- `Owner` -- entity with Id, Name, Email

Both the API and the Client reference this project directly.

### PetStore.Api

ASP.NET Core minimal API with two tag groups:

- **Pets** -- `ListPets`, `GetPet`, `CreatePet`
- **Owners** -- `ListOwners`, `GetOwner`

The API references `ApiStitch.OpenApi` and calls `AddApiStitchTypeInfo()` at startup. This writes `x-apistitch-type` extensions into the OpenAPI spec at build time, embedding the fully qualified CLR type name for each schema (e.g., `"x-apistitch-type": "PetStore.SharedModels.Pet"`).

`CreatePetRequest` lives in `PetStore.Api.Models` -- it is an API-specific DTO that is not shared with consumers.

The csproj sets `OpenApiGenerateDocumentsOnBuild` to emit `PetStore.Api.json` automatically during build.

### PetStore.Client

Console app that consumes the generated client. Uses `IServiceCollection` to register the generated clients and calls API methods through typed interfaces.

## Configuration

The `openapi-stitch.yaml` at the sample root configures generation:

```yaml
spec: PetStore.Api.json
namespace: PetStore.Client.Generated
clientName: PetStoreApi
typeReuse:
  includeNamespaces:
    - "PetStore.SharedModels.*"
```

The `includeNamespaces` pattern tells ApiStitch to reuse any type whose `x-apistitch-type` falls under `PetStore.SharedModels`. Types outside that namespace are generated fresh.

## What gets generated

The `PetStore.Client/Generated/` directory contains:

| File | Purpose |
|------|---------|
| `CreatePetRequest.cs` | Generated model -- `PetStore.Api.Models.*` does not match the include pattern |
| `IPetStoreApiPetsClient.cs` | Interface for Pets tag operations |
| `PetStoreApiPetsClient.cs` | Typed HttpClient implementation for Pets |
| `IPetStoreApiOwnersClient.cs` | Interface for Owners tag operations |
| `PetStoreApiOwnersClient.cs` | Typed HttpClient implementation for Owners |
| `PetStoreApiServiceCollectionExtensions.cs` | `AddPetStoreApi()` DI registration |
| `PetStoreApiClientOptions.cs` | Options (base URL, timeout, default headers) |
| `PetStoreApiJsonOptions.cs` | System.Text.Json serialization config |
| `GeneratedJsonContext.cs` | Source-generated `JsonSerializerContext` (AOT compatible) |
| `ApiException.cs` | Exception type for non-success responses |

## What is NOT generated

These types are reused directly from `PetStore.SharedModels`:

- `Pet` (`PetStore.SharedModels.Pet`)
- `PetStatus` (`PetStore.SharedModels.PetStatus`)
- `Owner` (`PetStore.SharedModels.Owner`)

The generated code references them by their full namespace. For example, `ListPetsAsync` returns `IReadOnlyList<PetStore.SharedModels.Pet>` -- no duplicate `Pet` class is generated.

## Multi-tag client generation

The API defines two tags (`Pets` and `Owners`), so ApiStitch generates a separate typed client per tag:

- `IPetStoreApiPetsClient` / `PetStoreApiPetsClient` -- `ListPetsAsync`, `GetPetAsync`, `CreatePetAsync`
- `IPetStoreApiOwnersClient` / `PetStoreApiOwnersClient` -- `ListOwnersAsync`, `GetOwnerAsync`

Both are registered automatically by `AddPetStoreApi()`.

## How to run

From the repository root:

```bash
# 1. Build the API (generates PetStore.Api.json at build time)
dotnet build samples/PetStore/PetStore.Api

# 2. Generate client code
cd samples/PetStore
dotnet run --project ../../src/ApiStitch.Cli -- generate --config openapi-stitch.yaml --output PetStore.Client/Generated

# 3. Start the API
dotnet run --project PetStore.Api &

# 4. Run the client
dotnet run --project PetStore.Client
```

The client connects to `http://localhost:5000`, lists pets and owners, and creates a new pet.
