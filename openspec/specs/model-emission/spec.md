## Purpose

C# code generation from the semantic model. Emits partial record types with modern idioms (required, init, nullable reference types), System.Text.Json attributes, enum serialization, inheritance, and a partial JsonSerializerContext for AOT-compatible source generation. Produces deterministic, diff-friendly output with one file per type.
## Requirements
### Requirement: Emit object schemas as partial records with modern C# idioms

The system SHALL emit each ApiSchema with Kind = Object as a `partial record` with file-scoped namespace, `[GeneratedCode("ApiStitch")]` attribute, and properties using `required`/`init`/nullable modifiers.

#### Scenario: Simple object with required and optional properties
- **WHEN** an ApiSchema has a required non-nullable `Id` (long) and an optional `Name` (string)
- **THEN** the emitted record contains `required long Id { get; init; }` and `string? Name { get; init; }`
- **THEN** both properties have `[JsonPropertyName("original_name")]` attributes
- **THEN** the record is declared as `public partial record SchemaName`
- **THEN** the file has `[GeneratedCode("ApiStitch")]` on the type
- **THEN** the file uses a file-scoped namespace
- **THEN** the file includes necessary using directives: `System`, `System.Text.Json.Serialization`, `System.CodeDom.Compiler`, and any others required by the emitted types (e.g., `System.Collections.Generic` for `IReadOnlyList`, `System.Runtime.Serialization` for `EnumMember`)

#### Scenario: Required nullable property
- **WHEN** an ApiProperty has IsRequired = true and IsNullable = true
- **THEN** the emitted property is `required string? Name { get; init; }`

#### Scenario: Deprecated schema
- **WHEN** an ApiSchema has IsDeprecated = true
- **THEN** the emitted record has `[Obsolete]` attribute

#### Scenario: Deprecated property
- **WHEN** an ApiProperty has IsDeprecated = true
- **THEN** the emitted property has `[Obsolete]` attribute

### Requirement: Emit properties with correct C# type mappings

The system SHALL map semantic model PrimitiveType values to C# type names for emitted properties.

#### Scenario: DateTimeOffset property
- **WHEN** a property has PrimitiveType = DateTimeOffset
- **THEN** the emitted C# type is `DateTimeOffset` (or `DateTimeOffset?` if nullable)

#### Scenario: Guid property
- **WHEN** a property has PrimitiveType = Guid
- **THEN** the emitted C# type is `Guid` (or `Guid?` if nullable)

#### Scenario: Array of referenced type
- **WHEN** a property has Kind = Array with ArrayItemSchema pointing to a named object schema
- **THEN** the emitted C# type is `IReadOnlyList<ReferencedType>` (or `IReadOnlyList<ReferencedType>?` if nullable)

### Requirement: Emit additionalProperties as JsonExtensionData

The system SHALL emit a `Dictionary<string, JsonElement>?` property with `[JsonExtensionData]` when an ApiSchema has HasAdditionalProperties = true and no typed schema. When a typed schema is present, the dictionary value type SHALL match the schema type.

#### Scenario: Untyped additionalProperties
- **WHEN** ApiSchema.HasAdditionalProperties = true and AdditionalPropertiesSchema = null
- **THEN** the emitted record includes `[JsonExtensionData] public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }`

#### Scenario: Typed additionalProperties (string values)
- **WHEN** ApiSchema.HasAdditionalProperties = true and AdditionalPropertiesSchema is Primitive/String
- **THEN** the emitted record includes `[JsonExtensionData] public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }`
- **THEN** a warning diagnostic is emitted noting that typed additionalProperties is approximated as `JsonElement` because `[JsonExtensionData]` only supports `Dictionary<string, JsonElement>` or `JsonObject`

### Requirement: Emit inherited records with base type

The system SHALL emit schemas with BaseSchema set as `sealed partial record Derived : Base` and SHALL NOT include inherited properties in the derived record.

#### Scenario: Derived record with base type
- **WHEN** ApiSchema `Dog` has BaseSchema pointing to `Animal`
- **THEN** the emitted code is `public sealed partial record Dog : Animal`
- **THEN** `Dog` does not redeclare any properties that exist on `Animal`

#### Scenario: Base record used in inheritance
- **WHEN** ApiSchema `Animal` is referenced as BaseSchema by `Dog` and `Cat`
- **THEN** the emitted code is `public partial record Animal` (not sealed, because it is inherited from)

### Requirement: Emit enum schemas as C# enums with string serialization

The system SHALL emit each ApiSchema with Kind = Enum as a C# `enum` with `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` and `[EnumMember(Value = "...")]` on each member for wire-name mapping. Enum files SHALL use file-scoped namespaces and include `[GeneratedCode("ApiStitch")]`.

#### Scenario: String enum with three values
- **WHEN** an ApiSchema has Kind = Enum with values `active`, `inactive`, `pending`
- **THEN** the emitted enum has members `Active`, `Inactive`, `Pending`
- **THEN** each member has `[EnumMember(Value = "active")]` (etc.) for serialization
- **THEN** the enum type has `[JsonConverter(typeof(JsonStringEnumConverter<EnumName>))]`
- **THEN** the file has `[GeneratedCode("ApiStitch")]`
- **THEN** the file uses a file-scoped namespace

### Requirement: Emit a partial JsonSerializerContext for all generated models

When client emission is active, JsonSerializerContext SHALL include ProblemDetails metadata only when ProblemDetails support is signaled by the specification.

#### Scenario: Context omits ProblemDetails without signal
- **WHEN** operations exist but no ProblemDetails support signal is present
- **THEN** JsonSerializerContext does not include ProblemDetails `JsonSerializable` metadata

#### Scenario: Context includes ProblemDetails with signal
- **WHEN** ProblemDetails support is signaled by response media types or schema usage
- **THEN** JsonSerializerContext includes the selected ProblemDetails type metadata (generated or external)

### Requirement: Emit properties with enum types correctly

The system SHALL emit properties whose type is an enum schema using the enum's C# type name, with nullable suffix when the property is nullable.

#### Scenario: Required enum property
- **WHEN** a record has a required property whose schema is an enum type `PetStatus`
- **THEN** the emitted property is `required PetStatus Status { get; init; }`

#### Scenario: Optional enum property
- **WHEN** a record has an optional property whose schema is an enum type `PetStatus`
- **THEN** the emitted property is `PetStatus? Status { get; init; }`

### Requirement: Emit properties in spec declaration order

The system SHALL emit record properties in the order they are declared in the OpenAPI spec (as parsed by Microsoft.OpenApi, which preserves YAML insertion order). This ensures diff-friendly, predictable output.

#### Scenario: Properties emitted in declaration order
- **WHEN** a schema declares properties in order: `name`, `id`, `status`
- **THEN** the emitted record lists properties in that same order: `Name`, `Id`, `Status`

### Requirement: Produce deterministic, diff-friendly output

The system SHALL produce identical output for identical input across runs. Files SHALL be sorted alphabetically by name. Properties within records SHALL follow declaration order from the spec. No timestamps, random values, or version numbers SHALL appear in the output.

#### Scenario: Deterministic output across runs
- **WHEN** the same spec and config are processed twice
- **THEN** the output files are byte-for-byte identical

#### Scenario: GeneratedCode attribute has no version
- **WHEN** any type is emitted
- **THEN** the `[GeneratedCode("ApiStitch")]` attribute does NOT include a version parameter

### Requirement: Emit one file per type

The model emitter SHALL emit one `.cs` file per object/record/enum schema and one JsonSerializerContext file.

When output style is `TypedClientStructured`:
- model files SHALL be emitted under `Models/`
- JsonSerializerContext SHALL be emitted under `Infrastructure/`

When output style is `TypedClientFlat`:
- model files and JsonSerializerContext SHALL be emitted at output root.

#### Scenario: simple model emission with structured layout
- **WHEN** schemas include `Pet` object, `Category` object, and `PetStatus` enum and output style is `TypedClientStructured`
- **THEN** files include `Models/Pet.cs`, `Models/Category.cs`, `Models/PetStatus.cs`, and `Infrastructure/{ClientName}JsonContext.cs`

#### Scenario: simple model emission with flat layout
- **WHEN** schemas include `Pet` object, `Category` object, and `PetStatus` enum and output style is `TypedClientFlat`
- **THEN** files include `Pet.cs`, `Category.cs`, `PetStatus.cs`, and `{ClientName}JsonContext.cs` at output root

#### Scenario: deterministic model path ordering
- **WHEN** generation runs repeatedly with same input and output style
- **THEN** emitted model/context relative paths are stable and deterministic

### Requirement: Emit diagnostic comments for unsupported patterns

The system SHALL emit inline comments in the generated output when an unsupported OpenAPI pattern is encountered, in addition to console-level warning diagnostics.

#### Scenario: Unsupported pattern encountered
- **WHEN** an unsupported schema construct is encountered during emission
- **THEN** the generated file contains a comment `// ApiStitch: [description of unsupported pattern]` at the relevant location
- **THEN** a warning diagnostic is also added to the generation result

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

### Requirement: Emit synthetic inline response models

The system SHALL emit generated model files for synthetic schemas created from supported inline success-response object schemas.

#### Scenario: Synthetic inline response model is emitted
- **WHEN** operation parsing creates a synthetic inline response ApiSchema for a supported success response object
- **THEN** model emission writes a corresponding `.cs` model file using the same conventions as other generated object schemas

#### Scenario: Synthetic inline response model is included in JsonSerializerContext
- **WHEN** a synthetic inline response ApiSchema is present in ApiSpecification.Schemas
- **THEN** JsonSerializerContext includes metadata for the synthetic type unless excluded by compatibility rules

#### Scenario: Inline primitive response does not create synthetic model file
- **WHEN** an operation uses a supported inline primitive success-response schema
- **THEN** no synthetic model file is created for that primitive response
- **THEN** client emission uses the mapped primitive type directly

### Requirement: Structured model namespaces align with folder roles

When output style is `TypedClientStructured`, model and serialization files SHALL use folder-aligned namespaces.

Required mapping:
- Model files in `Models/` use `{RootNamespace}.Models`
- JsonSerializerContext in `Infrastructure/` uses `{RootNamespace}.Infrastructure`

#### Scenario: Structured model namespace mapping
- **WHEN** object/enum models are emitted in structured mode
- **THEN** generated model namespaces are `{RootNamespace}.Models`

#### Scenario: Structured JsonSerializerContext namespace mapping
- **WHEN** JsonSerializerContext is emitted in structured mode
- **THEN** generated context namespace is `{RootNamespace}.Infrastructure`
- **THEN** context references model types correctly via segmented namespaces

#### Scenario: Flat mode keeps root model/context namespaces
- **WHEN** output style is `TypedClientFlat`
- **THEN** model and JsonSerializerContext files use `{RootNamespace}`

