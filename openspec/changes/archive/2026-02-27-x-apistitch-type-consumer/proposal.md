## Why

The `ApiStitch.OpenApi` producer package now writes `x-apistitch-type` vendor extensions onto OpenAPI schemas with CLR type names. The code generator needs to read these extensions and use them for automatic type reuse — skipping generation for types the consumer already has, and referencing the existing types instead. Without this, ApiStitch generates duplicate types even when the producer has annotated them.

This change also introduces type exclusion patterns so users can control which annotated types are reused vs regenerated (e.g., exclude `Microsoft.AspNetCore.*` types they don't want to depend on).

## What Changes

- Read `x-apistitch-type` vendor extensions from OpenAPI schemas during spec parsing; stash the raw value as `VendorTypeHint` on `ApiSchema`
- Introduce a new `ExternalTypeResolver` pipeline step (after `InheritanceDetector`, before `CSharpTypeMapper`) that applies exclusion config and sets `ExternalClrTypeName` on qualifying schemas
- `IsExternal` is a computed property (`=> ExternalClrTypeName is not null`), not a stored field
- Normalise `Type.FullName` nested type separator (`+`) to `.` for C# code emission
- Skip model emission for external schemas — no `.cs` file emitted
- **Do** emit `[JsonSerializable(typeof(ExternalType))]` for external types in the `JsonSerializerContext` (the consumer project must already reference the external assembly)
- Use fully-qualified CLR type names for all external type references in generated code (no `using` directives needed)
- External enums: skip model emission but still emit `EnumExtensions` for query parameter serialization
- Inheritance: when a base type is external, emit the fully-qualified external name as the base class in generated records
- Add configuration under a `typeReuse:` section in `apistitch.yaml`:
  - `excludeNamespaces`: glob patterns for namespaces to exclude from reuse (e.g., `Microsoft.AspNetCore.*`)
  - `excludeTypes`: specific fully-qualified type names to exclude from reuse
- Emit a diagnostic when an `x-apistitch-type` value is excluded by configuration (regenerated instead of reused)
- Emit a diagnostic when an `x-apistitch-type` value is found but malformed

## Capabilities

### New Capabilities
- `type-reuse-consumer`: Reading `x-apistitch-type` extensions, resolving external types, skipping emission, and using external CLR type names in generated code
- `type-exclusion-patterns`: Configuration for excluding types from reuse via namespace and type name glob patterns

### Modified Capabilities
- `schema-model`: Add `VendorTypeHint` and `ExternalClrTypeName` fields, computed `IsExternal` property to `ApiSchema`
- `configuration`: Add `typeReuse` section with `excludeNamespaces` and `excludeTypes` to `ApiStitchConfig`
- `model-emission`: Skip external schemas during emission; still emit `[JsonSerializable]` for external types and `EnumExtensions` for external enums

## Impact

- `src/ApiStitch/Model/ApiSchema.cs` — new fields (`VendorTypeHint`, `ExternalClrTypeName`, computed `IsExternal`)
- `src/ApiStitch/Configuration/ApiStitchConfig.cs` — new `TypeReuse` section with exclusion properties
- `src/ApiStitch/Configuration/ConfigLoader.cs` — deserialize `typeReuse` section
- `src/ApiStitch/Parsing/SchemaTransformer.cs` — read `x-apistitch-type` extension, set `VendorTypeHint`
- `src/ApiStitch/Parsing/ExternalTypeResolver.cs` — new: apply exclusion logic, set `ExternalClrTypeName`, normalise nested type separators
- `src/ApiStitch/TypeMapping/CSharpTypeMapper.cs` — respect `ExternalClrTypeName` (skip mapping for external schemas)
- `src/ApiStitch/Emission/ScribanModelEmitter.cs` — skip external schemas; include external types in `[JsonSerializable]`; external base class FQN
- `src/ApiStitch/Emission/ScribanClientEmitter.cs` — use fully-qualified external type names; emit `EnumExtensions` for external enums
- `src/ApiStitch/Generation/GenerationPipeline.cs` — wire `ExternalTypeResolver` into pipeline after `InheritanceDetector`
- `tests/` — new unit and integration tests for all of the above
