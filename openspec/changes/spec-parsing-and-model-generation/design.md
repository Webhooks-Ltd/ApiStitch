## Context

ApiStitch is a greenfield .NET OpenAPI client generator. No code exists yet. This change establishes the entire generation pipeline from OpenAPI spec to compilable C# records. Every subsequent feature (type reuse, Refit interfaces, MSBuild integration) depends on the semantic model, type mapper, and emitter infrastructure built here.

The key constraint is that the generated output must look hand-written — modern C# 12 idioms, System.Text.Json source generation, no preprocessor directives, no framework-specific serialization abstractions. This rules out approaches that generate framework-coupled code (Kiota's IParsable) or legacy patterns (NSwag's Newtonsoft attributes).

Target runtime: .NET 8+. No .NET Standard, no .NET Framework.

## Goals / Non-Goals

**Goals:**
- Parse any valid OpenAPI 3.0 spec (JSON or YAML) and produce compilable C# for its component schemas
- Establish a semantic model that represents API schemas independent of both OpenAPI DOM and C# output
- Stub operation/parameter/response types in the semantic model for future use
- Generate partial records with `required`/`init`/nullable, `[JsonPropertyName]`, `[GeneratedCode("ApiStitch")]`
- Generate a partial `JsonSerializerContext` covering all emitted models
- Handle the common 80%: objects, arrays, primitives, enums, `$ref`, `allOf`, nullable, required/optional
- Produce deterministic, diff-friendly output (sorted, no timestamps)
- Validate the approach with integration tests: generate → compile (Roslyn in-memory) → deserialize round-trip
- Establish snapshot testing (Verify) for generated output

**Non-Goals:**
- Operations, endpoints, or HTTP methods (next change)
- Type reuse, type mappings, namespace exclusion (next change)
- Refit interfaces or any client generation (next change)
- MSBuild integration (third change)
- CLI tool
- OpenAPI 3.1 or 2.0 support
- oneOf / anyOf / discriminated unions
- Schema-level overrides (partial, skip, enumType)
- Open enums (unknown values will throw)

## Decisions

### D1: Semantic Model as Intermediate Representation

**Decision**: Build a semantic model (set of C# types) that represents the API surface independent of both the OpenAPI DOM and the C# output.

**Alternatives considered**:
- **Emit directly from OpenAPI DOM** — Rejected. Microsoft.OpenApi's object model is verbose and reflects spec structure, not intent. Inline schemas, unresolved `$ref`, and the nullable/required ambiguity all need normalization before emission. Coupling the emitter to the OpenAPI DOM would make every emitter responsible for this normalization.
- **Use Kiota's CodeDOM approach** — Rejected. Kiota's CodeDOM is language-agnostic at the cost of being verbose and hard to extend. Our semantic model is C#-aware at the type mapping level but schema-agnostic at the structural level. We don't need multi-language support.

**Structure**:
```
ApiSpecification (root)
├── Schemas: IReadOnlyList<ApiSchema>
├── Operations: IReadOnlyList<ApiOperation>  (empty stub for this change)
└── Metadata (spec title, version, etc.)

ApiSchema
├── Name: string                          (unique PascalCased C# name — collisions resolved here, not in emitter)
├── OriginalName: string                  (original OpenAPI schema name, for diagnostics)
├── Description: string?
├── Kind: enum (Object, Enum, Primitive, Array)
├── Properties: IReadOnlyList<ApiProperty>  (for Object kind)
├── EnumValues: IReadOnlyList<ApiEnumMember>  (for Enum kind)
├── ArrayItemSchema: ApiSchema?           (for Array kind)
├── BaseSchema: ApiSchema?                (for detected inheritance — internal set, written only by InheritanceDetector)
├── PrimitiveType: enum (String, Int32, Int64, Float, Double, Decimal, Bool, DateTimeOffset, DateOnly, TimeOnly, TimeSpan, Guid, Uri, ByteArray)
├── IsNullable: bool
├── IsDeprecated: bool
├── HasAdditionalProperties: bool         (true when additionalProperties is set)
├── AdditionalPropertiesSchema: ApiSchema? (typed additionalProperties, null = Dictionary<string, JsonElement>)
├── CSharpTypeName: string?               (set by CSharpTypeMapper — all pipeline passes enrich the model)
├── Source: string?                       (JSON pointer into the spec, for diagnostics)
└── Diagnostics: List<Diagnostic>         (diagnostics specific to this schema)

ApiProperty
├── Name: string                          (original wire name)
├── CSharpName: string                    (PascalCased C# property name)
├── Schema: ApiSchema                     (the property's type — shared instance for $ref)
├── IsRequired: bool
├── IsNullable: bool                      (computed: !IsRequired || Schema.IsNullable)
├── IsDeprecated: bool
├── Description: string?
└── DefaultValue: string?

ApiEnumMember
├── Name: string                          (original wire value)
├── CSharpName: string                    (PascalCased member name)
└── Description: string?

GeneratedFile
├── RelativePath: string                  (relative to output directory, e.g., "Models/Pet.cs")
├── Content: string                       (full file content)

CSharpType (used internally by the mapper, then written to ApiSchema.CSharpTypeName)
├── FullName: string                      (e.g., "Pet", "IReadOnlyList<Pet>", "string")
├── IsNullable: bool
├── IsCollection: bool
├── ElementTypeName: string?              (for collections)
├── RequiresUsing: string?                (extra using directive, if any)
```

**Key design rules**:
- All `$ref` pointers are resolved during parsing. Two properties referencing the same schema point to the same `ApiSchema` instance (reference equality). The semantic model contains no unresolved references.
- Name collision resolution (e.g., `pet_status` and `PetStatus` both → `PetStatus`) happens in `SchemaTransformer`, not in the emitter. The `Name` field contains the final unique C# name.
- All pipeline passes enrich the model (inheritance detection sets `BaseSchema`, type mapping sets `CSharpTypeName`). No side-channel dictionaries. The emitter receives only `ApiSpecification` and `ApiStitchConfig`.
- `ApiProperty.IsNullable` is a computed property, not stored: `!IsRequired || Schema.IsNullable`.
- `BaseSchema` has an internal setter — only `InheritanceDetector` writes it.
- `binary` format maps to `byte[]` (not `Stream`). Stream-based file upload support is deferred to v1 feature 3.8.

### D2: allOf Handling — Flatten by Default, Inheritance Detected

**Decision**: Flatten all `allOf` entries into a single record. Detect the inheritance pattern (one `$ref` + one inline with extra properties, base used by 2+ schemas) and generate `sealed partial record Derived : Base` in that case.

**Implementation**:
1. During schema transformation, collect all `allOf` entries
2. Resolve each entry (inline schemas get their properties extracted, `$ref` entries resolve to their target)
3. Default: merge all properties into the current schema
4. Inheritance detection pass (after all schemas are transformed): scan for schemas where `allOf` has exactly one `$ref` target used by multiple schemas. Mark those with `BaseSchema` reference. Remove inherited properties from the derived schema's property list.

**Alternatives considered**:
- **Always inheritance** — Rejected. `allOf: [A, B]` with two `$ref` targets cannot be modeled as C# single inheritance. Flattening is the only correct general-case behavior.
- **Never inheritance** — Considered. Simpler, but produces redundant property declarations for genuine inheritance hierarchies that are common in practice. The detection heuristic is cheap and handles the 90% case.

### D3: Scriban for Code Templating

**Decision**: Use Scriban templates for C# code generation.

**Alternatives considered**:
- **Raw string interpolation** — Simpler, compile-time safe, but the template shape becomes invisible in C# code. For a solo developer who needs to iterate on output formatting quickly, visual templates are more productive. If Scriban's whitespace management becomes a tax, the emitter interface allows swapping to interpolation later.
- **Roslyn SyntaxFactory** — Correct-by-construction output, but 30+ lines of factory calls for a simple record. Overkill for file-based generation where we control the entire output.

**Mitigation for Scriban's weaknesses**:
- Snapshot tests (Verify) for every template catch silent rendering errors
- Strongly-typed template models — no raw dictionaries, each template receives a typed C# object
- Emitter interface (`IModelEmitter`) decouples the pipeline from the template engine

### D4: In-Memory Compilation for Integration Tests

**Decision**: Use Roslyn `CSharpCompilation` for integration tests instead of shelling out to `dotnet build`.

**Rationale**: Faster (no process startup), parallel-safe (no filesystem coordination), direct access to compilation diagnostics. Add `MetadataReference` for BCL, System.Text.Json, and System.Runtime assemblies.

### D5: Diagnostic Model

**Decision**: A simple `Diagnostic` record:
```
record Diagnostic(DiagnosticSeverity Severity, string Code, string Message, string? SpecPath);
enum DiagnosticSeverity { Warning, Error }
```

The generation pipeline collects diagnostics and returns them alongside the generated output. Warnings don't halt generation. Errors halt generation. The consumer (tests, future CLI, future MSBuild task) decides how to surface them.

Diagnostic codes are stable strings (e.g., `AS001`, `AS002`) to enable filtering and documentation.

### D6: Project and Solution Structure

```
ApiStitch.sln
src/
  ApiStitch/
    ApiStitch.csproj
    Parsing/
      OpenApiSpecLoader.cs         — Load + validate via Microsoft.OpenApi
      SchemaTransformer.cs         — OpenAPI schemas → semantic model
    Model/
      ApiSpecification.cs          — Root semantic model
      ApiSchema.cs                 — Schema types (object, enum, primitive, array)
      ApiProperty.cs               — Property with type, required, nullable
      ApiEnumMember.cs             — Enum value
      ApiOperation.cs              — Stub for operations (empty for this change)
      ApiParameter.cs              — Stub
      ApiResponse.cs               — Stub
    TypeMapping/
      CSharpTypeMapper.cs          — Semantic model → C# type names
    Emission/
      IModelEmitter.cs             — Interface for emitter abstraction
      ScribanModelEmitter.cs       — Scriban-based implementation
      Templates/                   — .sbn-cs template files (embedded resources)
        Record.sbn-cs
        Enum.sbn-cs
        JsonSerializerContext.sbn-cs
    Configuration/
      ApiStitchConfig.cs           — Config POCO
      ConfigLoader.cs              — YAML parsing via YamlDotNet
    Diagnostics/
      Diagnostic.cs
      DiagnosticSeverity.cs
    Generation/
      GenerationPipeline.cs        — Orchestrator: load → transform → map → emit
      GenerationResult.cs          — Output files + diagnostics
tests/
  ApiStitch.Tests/
    ApiStitch.Tests.csproj
    Parsing/                       — SchemaTransformer unit tests
    TypeMapping/                   — CSharpTypeMapper unit tests
    Emission/                      — Snapshot tests for templates
  ApiStitch.IntegrationTests/
    ApiStitch.IntegrationTests.csproj
    Specs/                         — Bundled OpenAPI specs (embedded resources)
      petstore.yaml
      complex-microservice.yaml
      allof-composition.yaml
      edge-cases.yaml
    GenerationTests.cs             — Generate → compile → deserialize round-trips
    DeterminismTests.cs            — Generate twice, assert identical output
```

### D7: Generation Pipeline Flow

```
ConfigLoader.Load("apistitch.yaml")
    → ApiStitchConfig

OpenApiSpecLoader.Load(config.Spec)
    → OpenApiDocument + parse diagnostics

SchemaTransformer.Transform(document)
    → ApiSpecification (semantic model with unique PascalCased names, resolved $refs,
       inline schemas hoisted, circular refs broken) + transform diagnostics

InheritanceDetector.Detect(specification)
    → Enriches ApiSchema.BaseSchema where inheritance pattern found
       Removes inherited properties from derived schemas

CSharpTypeMapper.MapAll(specification)
    → Enriches ApiSchema.CSharpTypeName for every schema
       (no side-channel dictionary — all data lives on the model)

ScribanModelEmitter.Emit(specification, config)
    → List<GeneratedFile> (path + content)

GenerationResult = new(files, diagnostics)
```

Each step is independently testable. The pipeline orchestrator (`GenerationPipeline`) composes them. The `GenerationPipeline` takes `ApiStitchConfig`, not a file path — config loading is the caller's responsibility.

### D8: Circular Reference Breaking Algorithm

Detect cycles during `SchemaTransformer.Transform` using depth-first traversal with a visited set.

When a cycle is detected (schema A references schema B which references schema A):
1. The back-edge property (the one encountered second during DFS) is made optional/nullable
2. Specifically: the `ApiProperty` on the later-visited schema has `IsRequired` set to `false`
3. A diagnostic warning is emitted: `AS003: Circular reference detected between '{A}' and '{B}'. Property '{B}.{prop}' relaxed to optional to break cycle.`
4. The cycle is broken — traversal continues normally

The "encountered second" rule ensures deterministic behavior: schemas are processed in alphabetical order by name, and DFS always visits properties in declaration order. This means the same spec always breaks the same property.

## Risks / Trade-offs

**[allOf detection heuristic may misfire]** → The "one $ref + one inline, base used by 2+ schemas" heuristic could miss valid inheritance or falsely detect it. Mitigation: schema-level overrides (feature 2.2) let users correct the heuristic. For MMVP, flattening is the safe default — inheritance detection is an optimization, not a correctness requirement.

**[Scriban whitespace pain for indentation-sensitive output]** → C# formatting is cosmetic but matters for the "looks hand-written" goal. Mitigation: snapshot tests catch formatting regressions. If Scriban becomes a tax, swap to raw interpolation behind the `IModelEmitter` interface. Consider running CSharpier as a post-processing step in integration tests to normalize formatting.

**[Circular references may be more common than expected]** → Breaking cycles by making back-references nullable changes the type contract. Mitigation: emit a diagnostic warning so the developer knows which property was relaxed. This is the same approach NSwag uses.

**[Large specs may produce slow Roslyn in-memory compilation in tests]** → Mitigation: integration tests use focused subset specs, not full Stripe/Graph specs. Full-spec testing deferred to performance benchmarks.

**[Microsoft.OpenApi parser may not handle all real-world specs correctly]** → Microsoft.OpenApi has known issues with certain edge cases. Mitigation: test against multiple real-world specs early. Report upstream bugs. The parser is a dependency we cannot work around.

**[System.Text.Json source generation requires all types known at compile time]** → When type reuse is added (next change), the JsonSerializerContext must include `[JsonSerializable]` for external types too. Mitigation: design the context generator to accept a list of all types (generated + external) from the start, even though external types don't exist in this change.
