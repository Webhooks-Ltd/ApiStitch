## MODIFIED Requirements

### Requirement: Emit a partial JsonSerializerContext for all generated models

The system SHALL emit a single `partial class` inheriting from `JsonSerializerContext` with `[JsonSerializable(typeof(T))]` for every generated record, enum type, AND collection types used in client request/response bodies. The context SHALL be named `{RootNamespaceLastSegment}JsonContext`.

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

## ADDED Requirements

### Requirement: Emit enum query string extension methods

The system SHALL emit an `internal static class {EnumName}Extensions` with a `ToQueryString()` method for each enum schema that appears as a query parameter in any operation.

#### Scenario: Enum used as query parameter
- **WHEN** enum `PetStatus` with values `available`, `pending`, `sold` is used as a query parameter
- **THEN** a `PetStatusExtensions.cs` file is emitted with class `internal static class PetStatusExtensions`
- **THEN** the class contains `internal static string ToQueryString(this PetStatus value)` returning wire values via switch expression

#### Scenario: Enum not used as query parameter
- **WHEN** enum `Category` is only used as an object property, never as a query parameter
- **THEN** no `CategoryExtensions.cs` file is emitted

#### Scenario: Extension method uses switch expression
- **WHEN** `PetStatus.ToQueryString()` is called with `PetStatus.Available`
- **THEN** the result is `"available"` (the original wire value, not the PascalCase C# name)

#### Scenario: AOT safety
- **WHEN** the extension method is emitted
- **THEN** it uses a switch expression with string constants (no reflection, no Enum.ToString())
- **THEN** a `_ => throw new ArgumentOutOfRangeException(nameof(value))` default arm is included
