## Why

ApiStitch cannot generate anything without a foundation: an OpenAPI spec parser, an internal representation of schemas, and a code emitter that produces compilable C#. This is the engine that every subsequent feature (type reuse, Refit interfaces, MSBuild integration) builds on. Starting with model generation validates the core quality proposition — clean, modern C# records with System.Text.Json source generation — before adding the differentiation layer.

Getting the schema-to-C# translation right is the hardest piece (~50% of MMVP effort). If the generated models are wrong, everything built on top is wrong. De-risk the foundation first.

## What Changes

- Parse OpenAPI 3.0 specs (JSON/YAML) via Microsoft.OpenApi into an internal semantic model
- Transform OpenAPI component schemas into a language-agnostic schema representation (objects, arrays, primitives, enums, `$ref` references, `allOf` composition, nullable, required/optional)
- Map semantic model types to C# types and emit clean partial records: `required` properties, `init` setters, nullable reference types (`string?` not `#if` directives), `[JsonPropertyName]` attributes, file-scoped namespaces, `[GeneratedCode("ApiStitch")]`
- Generate a partial `JsonSerializerContext` subclass with `[JsonSerializable(typeof(T))]` for every emitted model, enabling AOT-compatible serialization from day one
- Read a minimal YAML configuration file (`apistitch.yaml`) with spec path, output namespace, and output directory
- Establish the solution structure, project layout, Scriban template infrastructure, and integration test patterns

## Design Decisions

### Type Mapping (OpenAPI format → C#)

| OpenAPI type | OpenAPI format | C# type |
|---|---|---|
| `string` | (none) | `string` |
| `string` | `date-time` | `DateTimeOffset` |
| `string` | `date` | `DateOnly` |
| `string` | `time` | `TimeOnly` |
| `string` | `duration` | `TimeSpan` |
| `string` | `uuid` | `Guid` |
| `string` | `uri` | `Uri` |
| `string` | `byte` | `byte[]` |
| `string` | `binary` | `byte[]` |
| `string` | `enum` values | C# `enum` (string-backed) |
| `integer` | `int32` / (none) | `int` |
| `integer` | `int64` | `long` |
| `number` | `float` | `float` |
| `number` | `double` / (none) | `double` |
| `number` | `decimal` | `decimal` |
| `boolean` | — | `bool` |
| `array` | — | `IReadOnlyList<T>` |
| `object` | — | Generated record |
| unknown format | — | `string` (with diagnostic warning) |

`DateTimeOffset` over `DateTime` — always preserve timezone offset from API responses. `IReadOnlyList<T>` over `List<T>` — response models are read-only.

### Nullable + Required Interaction

| required | nullable | C# property |
|---|---|---|
| yes | no | `required string Name { get; init; }` |
| yes | yes | `required string? Name { get; init; }` |
| no | no | `string? Name { get; init; }` |
| no | yes | `string? Name { get; init; }` |

Optional properties are always nullable in C# regardless of OpenAPI `nullable`, because a missing JSON property deserializes to `null` via System.Text.Json. This is the pragmatic choice — `string Name = default!` would silence the warning but lie about the type.

### allOf Composition Strategy

**Default: flatten.** Merge all properties from all `allOf` entries into a single record. OpenAPI `allOf` is composition, not inheritance — `allOf: [A, B]` means "has all properties of A and B", not `is-a`.

**Inheritance as detected special case.** When `allOf` has exactly one `$ref` and one inline schema with additional properties, AND the referenced schema is used as a base by multiple schemas, generate `sealed partial record Derived : Base`. Single inheritance only — C# records don't support multiple inheritance.

Configurable per-schema via schema-level overrides (feature 2.2, deferred).

### Naming Conventions

- Schema names → PascalCase type names: `pet_status` → `PetStatus`, `pet-status` → `PetStatus`
- Property names → PascalCase C# names with `[JsonPropertyName("original_name")]`: `first_name` → `FirstName`
- Enum members → PascalCase with `[EnumMember(Value = "original_value")]`: `active` → `Active`
- Name collisions (e.g., `pet_status` and `PetStatus` both → `PetStatus`): suffix with ordinal (`PetStatus2`), emit diagnostic warning

### Enum Handling

String-backed enums with `[JsonConverter(typeof(JsonStringEnumConverter<T>))]`. Enum member names PascalCased with `[EnumMember(Value = "...")]` for serialization mapping. Unknown enum values throw at deserialization (MMVP limitation — open enums deferred to MVP feature 2.4).

### Circular References

Detect cycles during semantic model construction. Break cycles by switching the back-reference property from `required` to optional (nullable). This ensures records are constructable. Emit a diagnostic warning when a cycle is detected and broken.

### Inline Schemas

Hoist to top-level types with generated names: `{ParentType}{PropertyName}` (e.g., an inline schema on `Pet.address` becomes `PetAddress`). If the generated name collides, suffix with ordinal.

### additionalProperties

Deferred from this change. When encountered: emit a `Dictionary<string, JsonElement>? AdditionalProperties { get; init; }` property with `[JsonExtensionData]`. This handles the common case without custom serialization.

### Partial Keyword

All generated records are `partial record` per project convention. The `JsonSerializerContext` is also `partial` so users can add `[JsonSerializable]` attributes for their own types.

### Configuration File Name

`apistitch.yaml` (shorter, easier to type). Update `openspec/project.md` to match.

### Diagnostics

Structured warning/error model. Unsupported patterns produce:
1. A console warning with spec location and explanation
2. A `// ApiStitch: unsupported [description]` comment in the generated output at the relevant location

Generation continues past warnings (does not fail). Errors (e.g., unparseable spec) halt generation.

## Capabilities

### New Capabilities

- `spec-parsing`: Loading and validating OpenAPI 3.0 documents via Microsoft.OpenApi. Error/warning reporting for malformed specs. Extracting component schemas into the semantic model. All `$ref` pointers resolved during parsing — the semantic model contains no references, only direct object graphs with shared instances for type identity.
- `schema-model`: Semantic model for the API surface. Schema types: objects with properties, arrays, primitives, enums, composition (allOf flattening and detected inheritance), nullable/required modifiers. Stub types for operations, parameters, and responses included as empty placeholders to avoid structural rework when operations are added in the next change. Circular references detected and broken.
- `model-emission`: C# code generation from the semantic model. Partial record types with modern idioms. System.Text.Json attributes. Partial JsonSerializerContext source generation. Scriban template infrastructure with snapshot tests for every template. Deterministic, sorted, diff-friendly output.
- `configuration`: YAML configuration parsing via YamlDotNet. Minimal schema for this change: `spec`, `namespace`, `outputDir`. Validation and sensible defaults. Extensible for future properties (type mappings, filtering, output style).

### Modified Capabilities

(none — greenfield)

## Impact

- **New projects**: `ApiStitch` (core library), `ApiStitch.Tests` (unit tests), `ApiStitch.IntegrationTests` (end-to-end generation + compilation tests)
- **New dependencies**: Microsoft.OpenApi, Microsoft.OpenApi.Readers, Scriban, YamlDotNet
- **Test infrastructure**: Integration tests that generate C# from bundled OpenAPI specs, compile the output in-memory via Roslyn `CSharpCompilation`, and verify correctness (zero diagnostics, deserialization round-trips). Snapshot tests (Verify) for generated output to catch unintentional formatting changes. Determinism tests (generate twice, assert byte-for-byte identical output).
- **Test specs**: Petstore (baseline: simple objects, arrays, refs). Custom spec A (nested objects, nullable/required matrix, enums). Custom spec B (deep allOf composition, circular refs, arrays of refs). Edge case spec (empty schemas, no-property objects, primitive type aliases).
- **No MSBuild integration yet** — generation is invoked programmatically via tests or a console harness
- **No operations/endpoints** — only component schemas are processed; Refit interface generation is the next change. Operation/parameter/response types stubbed in the semantic model.
- **No type reuse** — all schemas produce generated types; explicit mapping and namespace exclusion come in the next change
