## 1. Semantic Model Changes

- [x] 1.1 Add `string? VendorTypeHint` property to `ApiSchema`
- [x] 1.2 Add `string? ExternalClrTypeName` settable property to `ApiSchema`
- [x] 1.3 Add computed `bool IsExternal => ExternalClrTypeName is not null` property to `ApiSchema`

## 2. Configuration

- [x] 2.1 Create `TypeReuseConfig` class with `ExcludeNamespaces` and `ExcludeTypes` list properties defaulting to empty
- [x] 2.2 Add `TypeReuse` property (type `TypeReuseConfig`, default `new()`) to `ApiStitchConfig`
- [x] 2.3 Add `TypeReuseDto` to `ConfigLoader.ConfigDto` and wire deserialization of `typeReuse` YAML section
- [x] 2.4 Filter empty/whitespace entries from `ExcludeNamespaces` and `ExcludeTypes` during config loading
- [x] 2.5 Write unit tests for config loading: with typeReuse, without typeReuse, empty section, partial sections (only excludeNamespaces, only excludeTypes), empty/whitespace entries filtered

## 3. SchemaTransformer — Read Vendor Extension

- [x] 3.1 In `TransformSchema` (single insertion point, after the sub-method returns the `ApiSchema`), read `x-apistitch-type` extension from `openApiSchema.Extensions` and set `VendorTypeHint` on the resulting schema. Use fully-qualified `Microsoft.OpenApi.Any.OpenApiString` (matching existing convention at line 152)
- [x] 3.2 Guard: only set when `ext is Microsoft.OpenApi.Any.OpenApiString str && !string.IsNullOrWhiteSpace(str.Value)`
- [x] 3.3 Write unit tests: schema with extension, without extension, empty/whitespace extension, non-string extension, inline property schema does NOT get VendorTypeHint

## 4. ExternalTypeResolver

- [x] 4.1 Create `ExternalTypeResolver` static class in `src/ApiStitch/Parsing/`
- [x] 4.2 Implement `Resolve(ApiSpecification specification, ApiStitchConfig config)` returning `IReadOnlyList<Diagnostic>` — iterate schemas, check `VendorTypeHint` against exclusion config, set `ExternalClrTypeName`
- [x] 4.3 Implement glob pattern matching for `excludeNamespaces` and exact string matching for `excludeTypes`: anchored globs with literal dots, `*` = any sequence, case-sensitive ordinal comparison. Use `Regex.Escape(pattern).Replace("\\*", ".*")` with `^`/`$` anchors
- [x] 4.4 Match exclusion patterns against raw `VendorTypeHint` (before `+` normalisation)
- [x] 4.5 Normalise `+` to `.` via `string.Replace("+", ".")` on the entire hint string after exclusion check passes
- [x] 4.6 Emit `AS500` info diagnostic when a type is excluded by configuration
- [x] 4.7 Add `TypeExcludedFromReuse = "AS500"` constant to `DiagnosticCodes`
- [x] 4.8 Write unit tests: no exclusions (hint honoured), excluded by namespace glob, excluded by exact type, no vendor hint (no-op), nested type `+` normalised, generic type `+` in args normalised, glob anchoring (`System.*` doesn't match `SystemMonitor`), case sensitivity, generic type with angle brackets matched by glob, multiple patterns where any match excludes

## 5. Pipeline Wiring

- [x] 5.1 Add `ExternalTypeResolver.Resolve(specification, config)` call in `GenerationPipeline.Generate` between `InheritanceDetector.Detect` and `CSharpTypeMapper.MapAll`
- [x] 5.2 Collect diagnostics from resolver and add to `allDiagnostics`
- [x] 5.3 Verify existing `GenerationPipelineTests` pass unchanged (default `TypeReuse` with empty lists is a no-op)

## 6. CSharpTypeMapper Changes

- [x] 6.1 Update `MapAll` to check `IsExternal` — if true, set `CSharpTypeName = ExternalClrTypeName` instead of calling `MapSchema`
- [x] 6.2 Update `MapSchema` to check `IsExternal` — if true, return `ExternalClrTypeName!`
- [x] 6.3 Write unit tests: external object maps to FQN, external enum maps to FQN, array with external item maps to `IReadOnlyList<FQN>`, non-external unchanged. Verify existing `CSharpTypeMapperTests` pass unchanged

## 7. Model Emission Changes

- [x] 7.1 Update `ScribanModelEmitter.Emit` to skip `.cs` file emission for schemas where `IsExternal` is true
- [x] 7.2 For external schemas, add `schema.CSharpTypeName` (FQN) to `typeNames` for `[JsonSerializable]` inclusion. For non-external schemas, continue using `schema.Name` (or switch to `schema.CSharpTypeName` which equals the short name post-MapAll)
- [x] 7.3 Change `base_name` in `EmitRecord` from `schema.BaseSchema?.Name` to `schema.BaseSchema?.CSharpTypeName`
- [x] 7.4 Create new `ScribanModelEmitterTests.cs` test class. Write tests: external schema skipped, external type in JsonSerializerContext, external base uses FQN, mixed external/non-external emission, external derived with non-external base (base emits normally, derived skipped, both in JsonSerializerContext)

## 8. Client Emission Changes

- [x] 8.1 Update `BuildParamModel` to check `param.Schema.IsExternal` (and `param.Schema.ArrayItemSchema?.IsExternal` for arrays) — skip adding to `queryEnums` for external enums
- [x] 8.2 Update `BuildQueryParamModel` to also guard `queryEnums.Add` calls at lines 208/213 — skip adding external enums to `queryEnums`
- [x] 8.3 Update `BuildQueryParamModel` to use `.ToString()` instead of `.ToQueryString()` for external enum parameters and `item.ToString()` for external enum array items
- [x] 8.4 Write unit tests: external enum query param uses `.ToString()`, external enum array query param uses `item.ToString()`, non-external enum unchanged, external type in return type uses FQN, external type in request body uses FQN. Verify existing `ScribanClientEmitterTests` pass unchanged

## 9. Integration Tests

- [x] 9.1 Write end-to-end test: OpenAPI spec with `x-apistitch-type` extensions → generation produces no `.cs` for external types, correct `[JsonSerializable]` entries, correct FQN in property types, collection types with external items produce correct `[JsonSerializable(typeof(IReadOnlyList<FQN>))]`
- [x] 9.2 Write end-to-end test: spec with `x-apistitch-type` + exclusion config → excluded types get regenerated, non-excluded types are external
- [x] 9.3 Write end-to-end test: external base type with non-external derived → derived record uses FQN base class
- [x] 9.4 Write end-to-end test: external derived type with non-external base → base emits normally, derived skipped
