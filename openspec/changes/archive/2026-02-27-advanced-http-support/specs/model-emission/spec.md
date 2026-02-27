## MODIFIED Requirements

### Requirement: Emit a partial JsonSerializerContext for all generated models

The system SHALL emit a single `partial class` inheriting from `JsonSerializerContext` with `[JsonSerializable(typeof(T))]` for every generated record, enum type, AND collection types used in client request/response bodies. When client emission is active (i.e., when the specification contains at least one operation), the context SHALL also include `[JsonSerializable(typeof(ProblemDetails))]` for AOT-compatible error deserialization. The context SHALL be named `{RootNamespaceLastSegment}JsonContext`.

#### Scenario: Spec with 3 schemas
- **WHEN** 3 schemas (Pet, Category, PetStatus enum) are generated with namespace `MyApi.Models`
- **THEN** a `ModelsJsonContext` class is emitted with three `[JsonSerializable]` attributes
- **THEN** the class is declared as `[JsonSourceGenerationOptions] public partial class ModelsJsonContext : JsonSerializerContext`

#### Scenario: Context is partial for user extension
- **WHEN** the JsonSerializerContext is emitted
- **THEN** the class is `partial` so users can add additional `[JsonSerializable]` attributes in a companion file

#### Scenario: Collection types from client responses are included
- **WHEN** an operation returns `IReadOnlyList<Pet>` and another returns `Pet`
- **THEN** the context includes `[JsonSerializable(typeof(Pet))]` and `[JsonSerializable(typeof(IReadOnlyList<Pet>))]`

#### Scenario: Collection types are deduplicated
- **WHEN** two operations both return `IReadOnlyList<Pet>`
- **THEN** only one `[JsonSerializable(typeof(IReadOnlyList<Pet>))]` attribute is emitted

#### Scenario: Collection types from request bodies are included
- **WHEN** an operation accepts `IReadOnlyList<Pet>` as a request body
- **THEN** the context includes `[JsonSerializable(typeof(IReadOnlyList<Pet>))]`

#### Scenario: ProblemDetails included when operations exist
- **WHEN** the specification contains at least one operation (client emission is active)
- **THEN** the context includes `[JsonSerializable(typeof(ProblemDetails))]`

#### Scenario: ProblemDetails omitted when no operations exist
- **WHEN** the specification contains no operations (model-only generation)
- **THEN** the context does NOT include `[JsonSerializable(typeof(ProblemDetails))]`
