## MODIFIED Requirements

### Requirement: Emit a partial JsonSerializerContext for all generated models

The system SHALL emit a single `partial class` inheriting from `JsonSerializerContext` with `[JsonSerializable(typeof(T))]` for every generated record and enum type, AND for every external type. External types SHALL use their fully-qualified CLR type name in the `typeof()` expression. The context SHALL be named `{RootNamespaceLastSegment}JsonContext`.

#### Scenario: Spec with 3 schemas
- **WHEN** 3 schemas (Pet, Category, PetStatus enum) are generated with namespace `MyApi.Models`
- **THEN** a `ModelsJsonContext` class is emitted with three `[JsonSerializable]` attributes
- **THEN** the class is declared as `[JsonSourceGenerationOptions] public partial class ModelsJsonContext : JsonSerializerContext`

#### Scenario: Context is partial for user extension
- **WHEN** the JsonSerializerContext is emitted
- **THEN** the class is `partial` so users can add additional `[JsonSerializable]` attributes in a companion file

#### Scenario: External type included in JsonSerializerContext
- **WHEN** a schema `Pet` is external with `CSharpTypeName = "SampleApi.Models.Pet"` and a schema `Category` is non-external
- **THEN** the context includes `[JsonSerializable(typeof(SampleApi.Models.Pet))]` and `[JsonSerializable(typeof(Category))]`

#### Scenario: Collection of external type in JsonSerializerContext
- **WHEN** an operation returns `IReadOnlyList<SampleApi.Models.Pet>` where `Pet` is external
- **THEN** the context includes `[JsonSerializable(typeof(IReadOnlyList<SampleApi.Models.Pet>))]`

## ADDED Requirements

### Requirement: Skip model emission for external schemas
The `ScribanModelEmitter` SHALL NOT emit `.cs` files (records or enums) for schemas where `IsExternal` is `true`. External schemas SHALL still be tracked for inclusion in the `JsonSerializerContext`.

#### Scenario: External object schema skipped
- **WHEN** an ApiSchema with Kind = Object has `IsExternal = true`
- **THEN** no `.cs` file is emitted for that schema
- **THEN** the schema's `CSharpTypeName` (FQN) is included in the `[JsonSerializable]` attributes

#### Scenario: External enum schema skipped
- **WHEN** an ApiSchema with Kind = Enum has `IsExternal = true`
- **THEN** no `.cs` file is emitted for that schema
- **THEN** the schema's `CSharpTypeName` (FQN) is included in the `[JsonSerializable]` attributes

#### Scenario: Non-external schemas emitted as before
- **WHEN** an ApiSchema has `IsExternal = false`
- **THEN** the schema is emitted as a `.cs` file as before (record for objects, enum for enums)

#### Scenario: Mixed external and non-external schemas
- **WHEN** a spec has 3 schemas: `Pet` (external), `Category` (non-external), `PetStatus` (external enum)
- **THEN** only `Category.cs` is emitted
- **THEN** the `JsonSerializerContext` includes `[JsonSerializable]` for all three types

### Requirement: External base type uses CSharpTypeName in record declaration
When emitting a non-external derived record, the `ScribanModelEmitter` SHALL use `BaseSchema.CSharpTypeName` (not `BaseSchema.Name`) for the base class name in the record template model. This ensures external base types are referenced by their fully-qualified name, and non-external bases continue to use their short name (since `CSharpTypeName` equals the short name for non-external schemas).

#### Scenario: Derived record with external base
- **WHEN** non-external `Dog` has `BaseSchema` pointing to external `Animal` with `CSharpTypeName = "SharedModels.Animal"`
- **THEN** the emitted code is `public sealed partial record Dog : SharedModels.Animal`

#### Scenario: Derived record with non-external base (unchanged)
- **WHEN** non-external `Dog` has `BaseSchema` pointing to non-external `Animal` with `CSharpTypeName = "Animal"`
- **THEN** the emitted code is `public sealed partial record Dog : Animal`
