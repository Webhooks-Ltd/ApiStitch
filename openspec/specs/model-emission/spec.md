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

The system SHALL emit a single `partial class` inheriting from `JsonSerializerContext` with `[JsonSerializable(typeof(T))]` for every generated record and enum type. The context SHALL be named `{RootNamespaceLastSegment}JsonContext`.

#### Scenario: Spec with 3 schemas
- **WHEN** 3 schemas (Pet, Category, PetStatus enum) are generated with namespace `MyApi.Models`
- **THEN** a `ModelsJsonContext` class is emitted with three `[JsonSerializable]` attributes
- **THEN** the class is declared as `[JsonSourceGenerationOptions] public partial class ModelsJsonContext : JsonSerializerContext`

#### Scenario: Context is partial for user extension
- **WHEN** the JsonSerializerContext is emitted
- **THEN** the class is `partial` so users can add additional `[JsonSerializable]` attributes in a companion file

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

The system SHALL emit each record and enum as a separate `.cs` file named after the type (e.g., `Pet.cs`, `PetStatus.cs`). The JsonSerializerContext SHALL be in its own file.

#### Scenario: Three schemas produce four files
- **WHEN** a spec has schemas Pet (object), Category (object), PetStatus (enum)
- **THEN** the emitter produces `Pet.cs`, `Category.cs`, `PetStatus.cs`, and `{Name}JsonContext.cs`

### Requirement: Emit diagnostic comments for unsupported patterns

The system SHALL emit inline comments in the generated output when an unsupported OpenAPI pattern is encountered, in addition to console-level warning diagnostics.

#### Scenario: Unsupported pattern encountered
- **WHEN** an unsupported schema construct is encountered during emission
- **THEN** the generated file contains a comment `// ApiStitch: [description of unsupported pattern]` at the relevant location
- **THEN** a warning diagnostic is also added to the generation result
