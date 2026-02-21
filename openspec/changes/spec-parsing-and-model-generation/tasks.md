## 1. Solution and Project Setup

- [x] 1.1 Create `ApiStitch.sln` with `src/ApiStitch/ApiStitch.csproj` (.NET 8, C# 12, nullable enable, warnings as errors)
- [x] 1.2 Create `tests/ApiStitch.Tests/ApiStitch.Tests.csproj` with xUnit, Verify references
- [x] 1.3 Create `tests/ApiStitch.IntegrationTests/ApiStitch.IntegrationTests.csproj` with xUnit, Microsoft.CodeAnalysis.CSharp references
- [x] 1.4 Add NuGet references to `ApiStitch.csproj`: Microsoft.OpenApi, Microsoft.OpenApi.Readers, Scriban, YamlDotNet
- [x] 1.5 Create `Directory.Build.props` with shared settings (LangVersion, Nullable, TreatWarningsAsErrors, ImplicitUsings)

## 2. Semantic Model

- [x] 2.1 Create `Model/ApiSpecification.cs` — root type with `Schemas`, `Operations` (empty stub list), `Metadata`
- [x] 2.2 Create `Model/ApiSchema.cs` — Kind enum (Object, Enum, Primitive, Array), all properties per design (Name, OriginalName, Description, Properties, EnumValues, ArrayItemSchema, BaseSchema with internal set, PrimitiveType, IsNullable, IsDeprecated, HasAdditionalProperties, AdditionalPropertiesSchema, CSharpTypeName, Source, Diagnostics)
- [x] 2.3 Create `Model/ApiProperty.cs` — Name, CSharpName, Schema, IsRequired, computed IsNullable, IsDeprecated, Description, DefaultValue
- [x] 2.4 Create `Model/ApiEnumMember.cs` — Name, CSharpName, Description
- [x] 2.5 Create `Model/PrimitiveType.cs` enum — String, Int32, Int64, Float, Double, Decimal, Bool, DateTimeOffset, DateOnly, TimeOnly, TimeSpan, Guid, Uri, ByteArray
- [x] 2.6 Create stub types: `Model/ApiOperation.cs`, `Model/ApiParameter.cs`, `Model/ApiResponse.cs`
- [x] 2.7 Create `Diagnostics/Diagnostic.cs` record and `Diagnostics/DiagnosticSeverity.cs` enum
- [x] 2.8 Create `Generation/GeneratedFile.cs` record (RelativePath, Content)
- [x] 2.9 Create `Generation/GenerationResult.cs` record (Files, Diagnostics)

## 3. Configuration

- [x] 3.1 Create `Configuration/ApiStitchConfig.cs` — Spec, Namespace (default: ApiStitch.Generated), OutputDir (default: ./Generated)
- [x] 3.2 Create `Configuration/ConfigLoader.cs` — parse YAML via YamlDotNet, validate required `spec` property, ignore unknown properties. Return error diagnostics: AS300 (file not found), AS301 (invalid YAML), AS302 (missing/empty spec)
- [x] 3.3 Write unit tests for ConfigLoader: valid config, defaults, missing spec, empty spec, invalid YAML, unknown properties

## 4. OpenAPI Spec Parsing

- [x] 4.1 Create `Parsing/OpenApiSpecLoader.cs` — load from file path via Microsoft.OpenApi.Readers, convert parser warnings to diagnostics, reject OpenAPI 2.0 (AS101) and 3.1 (AS102), return error diagnostic for missing file (AS100), emit warning AS103 when spec has no component schemas
- [x] 4.2 Bundle test OpenAPI specs as embedded resources in `ApiStitch.Tests`: minimal valid spec, OpenAPI 2.0 spec, spec with warnings, spec with no component schemas
- [x] 4.3 Write unit tests for OpenApiSpecLoader: valid YAML, valid JSON, missing file, invalid content, version rejection, warnings pass-through, no component schemas warning

## 5. Schema Transformation

- [x] 5.1 Create `Parsing/SchemaTransformer.cs` — transform OpenApiDocument component schemas to ApiSpecification. Resolve all $ref pointers. Populate Operations as empty list.
- [x] 5.2 Implement PascalCase naming: convert schema names (snake_case, kebab-case, camelCase) to PascalCase. Resolve name collisions with ordinal suffix (AS203). Store OriginalName.
- [x] 5.3 Implement property CSharpName PascalCasing (snake_case, kebab-case, camelCase → PascalCase)
- [x] 5.4 Implement primitive type + format mapping per the type mapping table (all 15 combinations). Emit AS204 for unknown formats.
- [x] 5.5 Implement object schema transformation: properties with required/nullable modifiers, $ref resolution with shared instances
- [x] 5.6 Implement enum schema transformation: string enums with PascalCased CSharpName. Integer enums emit AS200 and fall back to primitive type.
- [x] 5.7 Implement array schema transformation (including top-level array component schemas)
- [x] 5.8 Implement allOf flattening: merge properties from all entries. Handle property name conflicts (AS201, keep last).
- [x] 5.9 Implement inline schema hoisting: generate `{ParentType}{PropertyName}` names, resolve collisions with ordinal suffix (AS202)
- [x] 5.10 Implement circular reference detection: DFS in alphabetical order, break back-edge by setting IsRequired = false, emit AS003
- [x] 5.11 Implement additionalProperties handling: set HasAdditionalProperties and AdditionalPropertiesSchema. Emit warning diagnostic when typed additionalProperties is encountered (approximated as Dictionary<string, JsonElement> because [JsonExtensionData] only supports JsonElement)
- [x] 5.12 Implement deprecation flag pass-through for schemas and properties
- [x] 5.13 Implement description field pass-through for schemas and properties
- [x] 5.14 Handle edge cases: empty object (no properties), primitive type aliases, top-level arrays, allOf with only inline schemas, allOf with empty inline
- [x] 5.15 Create `Parsing/InheritanceDetector.cs` — post-transform pass: detect one-$ref-plus-inline pattern with base used by 2+ schemas, set BaseSchema (internal), remove inherited properties from derived
- [x] 5.16 Write unit tests for SchemaTransformer: one test per scenario in schema-model/spec.md. Use YAML snippet helpers to construct OpenApiDocument instances.
- [x] 5.17 Write unit tests for InheritanceDetector: two-schema inheritance, single-use (no inheritance), allOf with empty inline (no inheritance)

## 6. C# Type Mapping

- [x] 6.1 Create `TypeMapping/CSharpTypeMapper.cs` — enrich ApiSchema.CSharpTypeName for every schema. Map PrimitiveType to C# type name, handle nullable suffix, handle IReadOnlyList<T> for arrays, use schema Name for objects/enums.
- [x] 6.2 Create `TypeMapping/CSharpType.cs` — FullName, IsNullable, IsCollection, ElementTypeName, RequiresUsing (internal helper consumed during mapping)
- [x] 6.3 Write unit tests for CSharpTypeMapper: all primitive mappings, nullable variants, array of ref, object ref, enum ref

## 7. Code Emission (Scriban Templates)

- [x] 7.1 Create Scriban template `Emission/Templates/Record.sbn-cs` — partial record with file-scoped namespace, using directives, [GeneratedCode], [JsonPropertyName], required/init/nullable, [Obsolete] for deprecated, [JsonExtensionData] for additionalProperties. Sealed for derived types, not sealed for base types.
- [x] 7.2 Create Scriban template `Emission/Templates/Enum.sbn-cs` — enum with file-scoped namespace, [GeneratedCode], [JsonConverter(typeof(JsonStringEnumConverter<T>))], [EnumMember(Value = "...")] per member
- [x] 7.3 Create Scriban template `Emission/Templates/JsonSerializerContext.sbn-cs` — partial class inheriting JsonSerializerContext, [JsonSerializable(typeof(T))] per model, [JsonSourceGenerationOptions]
- [x] 7.4 Create `Emission/IModelEmitter.cs` interface — `IReadOnlyList<GeneratedFile> Emit(ApiSpecification spec, ApiStitchConfig config)`
- [x] 7.5 Create `Emission/ScribanModelEmitter.cs` — load embedded templates, create strongly-typed template models, render each schema to a GeneratedFile. One file per type plus one for JsonSerializerContext. Emit diagnostic comments for unsupported patterns.
- [x] 7.6 Write snapshot tests (Verify) for each template: simple record, record with inheritance, enum, JsonSerializerContext. Assert exact output.
- [x] 7.7 Verify emitted properties follow spec declaration order
- [x] 7.8 Verify emitted files are sorted alphabetically by name

## 8. Generation Pipeline

- [x] 8.1 Create `Generation/GenerationPipeline.cs` — orchestrate: load spec → transform → detect inheritance → map types → emit. Collect diagnostics from all stages. Return GenerationResult.
- [x] 8.2 Pipeline takes ApiStitchConfig (not file path). Config loading is caller's responsibility.
- [x] 8.3 Write unit test for pipeline: happy path with a minimal spec, verify GenerationResult contains expected files and no error diagnostics

## 9. Integration Tests

- [x] 9.1 Bundle test specs as embedded resources in IntegrationTests: petstore.yaml, complex-microservice.yaml (15-20 schemas), allof-composition.yaml, edge-cases.yaml
- [x] 9.2 Create Roslyn in-memory compilation helper: compile generated .cs files with MetadataReference for BCL, System.Text.Json, System.Runtime. Assert zero diagnostics.
- [x] 9.3 Write Petstore integration test: generate → compile → assert no errors/warnings
- [x] 9.4 Write complex microservice integration test: generate → compile → assert no errors/warnings
- [x] 9.5 Write allOf composition integration test: generate → compile → verify inheritance relationships
- [x] 9.6 Write edge case integration test: empty objects, primitive aliases, circular refs → compile → assert no errors
- [x] 9.7 Write deserialization round-trip test: generate → compile → load assembly → deserialize sample JSON → assert property values
- [x] 9.8 Write determinism test: generate twice from same spec, assert byte-for-byte identical output
