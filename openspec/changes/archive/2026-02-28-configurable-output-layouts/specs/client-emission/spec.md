## MODIFIED Requirements

### Requirement: Emit one file per generated type

The emitter SHALL produce one C# file per generated type.

Shared infrastructure files SHALL include:
- `ApiException.cs`
- `{ClientName}ClientOptions.cs`
- `{ClientName}JsonOptions.cs`
- `{ClientName}ServiceCollectionExtensions.cs`

For each tag group, emitter SHALL produce:
- interface file (`I{ClientName}{Tag}Client.cs` or `I{ClientName}Client.cs` for default tag)
- implementation file (`{ClientName}{Tag}Client.cs` or `{ClientName}Client.cs` for default tag)

When output style is `TypedClientStructured`, relative paths SHALL be foldered as follows:
- `Contracts/` for interfaces
- `Clients/` for client implementations and `FileResponse.cs`
- `Infrastructure/` for `ApiException.cs` and `ProblemDetails.cs`
- `Configuration/` for options/json/service-collection/extensions support files

When output style is `TypedClientFlat`, the same files SHALL be emitted at output root.

#### Scenario: single-tag API with structured layout
- **WHEN** spec has one tag group and output style is `TypedClientStructured`
- **THEN** generated files include `Contracts/ITestApiPetsClient.cs`, `Clients/TestApiPetsClient.cs`, and shared files under `Configuration/` and `Infrastructure/`

#### Scenario: multi-tag API with structured layout
- **WHEN** spec has `Pets` and `Store` tag groups and output style is `TypedClientStructured`
- **THEN** generated files include `Contracts/ITestApiPetsClient.cs`, `Clients/TestApiPetsClient.cs`, `Contracts/ITestApiStoreClient.cs`, `Clients/TestApiStoreClient.cs`
- **THEN** shared files are emitted once under their mapped folders

#### Scenario: single-tag API with flat layout
- **WHEN** spec has one tag group and output style is `TypedClientFlat`
- **THEN** generated files include `ITestApiPetsClient.cs`, `TestApiPetsClient.cs`, and shared files at output root

#### Scenario: file inventory deterministic ordering
- **WHEN** generation runs repeatedly with same input
- **THEN** file relative paths and order are stable and deterministic within selected layout style
