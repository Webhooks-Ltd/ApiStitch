## ADDED Requirements

### Requirement: Read x-apistitch-type vendor extensions from component schemas
The `SchemaTransformer` SHALL read the `x-apistitch-type` vendor extension from each OpenAPI component schema during transformation. When present and non-empty, the raw string value SHALL be stored on `ApiSchema.VendorTypeHint`. The extension SHALL only be read from component schemas, not inline property schemas.

#### Scenario: Component schema with x-apistitch-type extension
- **WHEN** a component schema has `"x-apistitch-type": "SampleApi.Models.Pet"`
- **THEN** the resulting `ApiSchema.VendorTypeHint` is `"SampleApi.Models.Pet"`

#### Scenario: Component schema without x-apistitch-type extension
- **WHEN** a component schema has no `x-apistitch-type` extension
- **THEN** the resulting `ApiSchema.VendorTypeHint` is `null`

#### Scenario: Extension value is empty or whitespace
- **WHEN** a component schema has `"x-apistitch-type": "  "`
- **THEN** the resulting `ApiSchema.VendorTypeHint` is `null`

#### Scenario: Extension value is not a string
- **WHEN** a component schema has `"x-apistitch-type": 42` or an object value
- **THEN** the resulting `ApiSchema.VendorTypeHint` is `null`

#### Scenario: Inline property schema with extension (ignored)
- **WHEN** an inline property schema (not a `$ref` to a component) has `x-apistitch-type`
- **THEN** the extension is not read and `VendorTypeHint` is `null`

### Requirement: ExternalTypeResolver resolves vendor type hints to external CLR type names
The `ExternalTypeResolver` pipeline step SHALL iterate all schemas in the specification, evaluate each `VendorTypeHint` against the exclusion configuration, and set `ExternalClrTypeName` on schemas that qualify for reuse. The step SHALL run after `InheritanceDetector` and before `CSharpTypeMapper`.

#### Scenario: Schema with vendor type hint and no exclusions
- **WHEN** a schema has `VendorTypeHint = "SampleApi.Models.Pet"` and no exclusion patterns match
- **THEN** `ExternalClrTypeName` is set to `"SampleApi.Models.Pet"` and `IsExternal` is `true`

#### Scenario: Schema with vendor type hint excluded by namespace pattern
- **WHEN** a schema has `VendorTypeHint = "Microsoft.AspNetCore.Mvc.ProblemDetails"` and `excludeNamespaces` contains `"Microsoft.AspNetCore.*"`
- **THEN** `ExternalClrTypeName` remains `null` and `IsExternal` is `false`
- **THEN** a diagnostic `AS500` (info) is emitted: "Type 'Microsoft.AspNetCore.Mvc.ProblemDetails' excluded from reuse by configuration. Code will be generated."

#### Scenario: Schema with vendor type hint excluded by exact type name
- **WHEN** a schema has `VendorTypeHint = "SampleApi.Models.Pet"` and `excludeTypes` contains `"SampleApi.Models.Pet"`
- **THEN** `ExternalClrTypeName` remains `null` and `IsExternal` is `false`
- **THEN** a diagnostic `AS500` (info) is emitted

#### Scenario: Schema with no vendor type hint
- **WHEN** a schema has `VendorTypeHint = null`
- **THEN** `ExternalClrTypeName` remains `null` and `IsExternal` is `false`
- **THEN** no diagnostic is emitted

### Requirement: Normalise nested type separator in external CLR type names
The `ExternalTypeResolver` SHALL replace all `+` characters with `.` in the vendor type hint before storing it as `ExternalClrTypeName`. This normalisation SHALL apply to the entire string, including within generic type arguments.

#### Scenario: Nested type name
- **WHEN** a schema has `VendorTypeHint = "SampleApi.Models.Outer+Inner"`
- **THEN** `ExternalClrTypeName` is set to `"SampleApi.Models.Outer.Inner"`

#### Scenario: Nested type within generic type argument
- **WHEN** a schema has `VendorTypeHint = "SampleApi.Models.Container<SampleApi.Models.Outer+Inner>"`
- **THEN** `ExternalClrTypeName` is set to `"SampleApi.Models.Container<SampleApi.Models.Outer.Inner>"`

#### Scenario: Non-nested type (no +)
- **WHEN** a schema has `VendorTypeHint = "SampleApi.Models.Pet"`
- **THEN** `ExternalClrTypeName` is set to `"SampleApi.Models.Pet"` (unchanged)

### Requirement: IsExternal is a computed property
`ApiSchema.IsExternal` SHALL be a computed property that returns `true` when `ExternalClrTypeName` is not null, and `false` otherwise. It SHALL NOT be a separately stored field.

#### Scenario: Schema with ExternalClrTypeName set
- **WHEN** `ExternalClrTypeName` is `"SampleApi.Models.Pet"`
- **THEN** `IsExternal` returns `true`

#### Scenario: Schema without ExternalClrTypeName
- **WHEN** `ExternalClrTypeName` is `null`
- **THEN** `IsExternal` returns `false`

### Requirement: CSharpTypeMapper uses ExternalClrTypeName for external schemas
`CSharpTypeMapper.MapAll` and `MapSchema` SHALL check `IsExternal` before applying default type mapping. When `IsExternal` is `true`, the schema's `CSharpTypeName` SHALL be set to `ExternalClrTypeName` (the fully-qualified CLR type name).

#### Scenario: External object schema
- **WHEN** an external schema has Kind = Object and `ExternalClrTypeName = "SampleApi.Models.Pet"`
- **THEN** `CSharpTypeName` is set to `"SampleApi.Models.Pet"` (not the short name `"Pet"`)

#### Scenario: External enum schema
- **WHEN** an external schema has Kind = Enum and `ExternalClrTypeName = "SampleApi.Models.PetStatus"`
- **THEN** `CSharpTypeName` is set to `"SampleApi.Models.PetStatus"`

#### Scenario: Array item is external
- **WHEN** a non-external array schema has an `ArrayItemSchema` that is external with `ExternalClrTypeName = "SampleApi.Models.Pet"`
- **THEN** the array schema's `CSharpTypeName` is `"IReadOnlyList<SampleApi.Models.Pet>"`

#### Scenario: Non-external schema unchanged
- **WHEN** a schema has `IsExternal = false`
- **THEN** `CSharpTypeName` is derived from the schema kind as before (e.g., `"Pet"` for objects, `"int"` for primitives)

### Requirement: External types use fully-qualified names in all generated code
All generated code SHALL reference external types by their fully-qualified CLR type name. No `using` directives SHALL be added for external type namespaces.

#### Scenario: Property references external type
- **WHEN** a non-external record has a required property whose schema is external with `CSharpTypeName = "SampleApi.Models.Category"`
- **THEN** the emitted property is `public required SampleApi.Models.Category Category { get; init; }`

#### Scenario: Operation return type is external
- **WHEN** an operation's success response schema is external with `CSharpTypeName = "SampleApi.Models.Pet"`
- **THEN** the generated client method returns `Task<SampleApi.Models.Pet>`

#### Scenario: Operation request body is external
- **WHEN** an operation's request body schema is external with `CSharpTypeName = "SampleApi.Models.CreatePetRequest"`
- **THEN** the generated client method parameter type is `SampleApi.Models.CreatePetRequest`

#### Scenario: Generic external type in typeof
- **WHEN** an external schema has `CSharpTypeName = "SampleApi.Models.PagedResult<SampleApi.Models.Pet>"`
- **THEN** the `[JsonSerializable]` attribute emits `typeof(SampleApi.Models.PagedResult<SampleApi.Models.Pet>)`

### Requirement: Inheritance with external base type uses FQN
When a non-external derived schema has a `BaseSchema` that is external, the generated record SHALL use the external type's fully-qualified name as the base class.

#### Scenario: External base, non-external derived
- **WHEN** schema `Dog` has `BaseSchema` pointing to an external `Animal` with `CSharpTypeName = "SharedModels.Animal"`
- **THEN** the emitted code is `public sealed partial record Dog : SharedModels.Animal`

#### Scenario: Both base and derived are external
- **WHEN** both `Animal` and `Dog` are external
- **THEN** neither `.cs` file is emitted; both appear in `[JsonSerializable]` with their FQNs

### Requirement: External enums fall back to .ToString() for query serialisation
When an external enum is used as a query parameter, the generated client code SHALL use `.ToString()` instead of `.ToQueryString()` for query string serialisation. No `EnumExtensions` class SHALL be emitted for external enums.

#### Scenario: External enum as query parameter
- **WHEN** an operation has a query parameter of an external enum type `SampleApi.Models.PetStatus`
- **THEN** the generated query string serialisation uses `.ToString()` (not `.ToQueryString()`)
- **THEN** no `PetStatusExtensions` class is emitted

#### Scenario: External enum array as query parameter
- **WHEN** an operation has a query parameter of type `IReadOnlyList<SampleApi.Models.PetStatus>` where the item schema is an external enum
- **THEN** the generated query string serialisation uses `item.ToString()` (not `item.ToQueryString()`)
- **THEN** no `PetStatusExtensions` class is emitted

#### Scenario: Non-external enum as query parameter (unchanged)
- **WHEN** an operation has a query parameter of a non-external enum type `PetStatus`
- **THEN** the generated query string serialisation uses `.ToQueryString()` as before
- **THEN** a `PetStatusExtensions` class is emitted

### Requirement: GenerationPipeline wires ExternalTypeResolver in correct order
The `GenerationPipeline.Generate` method SHALL invoke `ExternalTypeResolver.Resolve(specification, config)` after `InheritanceDetector.Detect(specification)` and before `CSharpTypeMapper.MapAll(specification)`.

#### Scenario: Pipeline ordering
- **WHEN** the generation pipeline runs
- **THEN** `ExternalTypeResolver.Resolve` is called after inheritance detection and before C# type mapping
- **THEN** schemas marked external by the resolver have `IsExternal = true` when `CSharpTypeMapper.MapAll` runs

### Requirement: ExternalTypeResolver exclusion matching uses raw VendorTypeHint
Exclusion pattern matching SHALL be performed against the raw `VendorTypeHint` value, before nested type normalisation (`+` to `.` replacement). This ensures patterns match the original CLR type name format.

#### Scenario: Exclusion matching before normalization
- **WHEN** a schema has `VendorTypeHint = "SampleApi.Models.Outer+Inner"` and `excludeNamespaces` contains `"SampleApi.*"`
- **THEN** the pattern matches against `"SampleApi.Models.Outer+Inner"` (the raw value)
- **THEN** the type is excluded from reuse

### Requirement: External derived type with non-external base
When a derived schema is external but its base is not, the base schema SHALL be emitted normally. The derived schema SHALL be skipped during emission.

#### Scenario: External derived, non-external base
- **WHEN** `Dog` is external and `Animal` is non-external, and `Dog` has `BaseSchema` pointing to `Animal`
- **THEN** `Animal.cs` is emitted normally
- **THEN** no `Dog.cs` is emitted
- **THEN** both appear in `[JsonSerializable]` with their respective type names

### Requirement: Diagnostic code constants
`DiagnosticCodes` SHALL define the constant `TypeExcludedFromReuse = "AS500"`.

#### Scenario: Diagnostic code defined
- **WHEN** the `DiagnosticCodes` class is compiled
- **THEN** it contains `public const string TypeExcludedFromReuse = "AS500"`
