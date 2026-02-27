## Why

The codebase currently dual-targets Microsoft.OpenApi v1 (1.6.x for net8.0) and a newer OpenAPI reader path for net9.0+, using `#if NET9_0_OR_GREATER` / `#if NET10_0_OR_GREATER` conditional compilation throughout. This creates maintenance burden, duplicated code paths, and fragile branching logic. net10.0 is LTS (released Nov 2025) and net9.0 goes EOL May 2026. Drop v1 entirely, standardise on Microsoft.OpenApi v3 APIs/packages, and target net10.0 across the board.

## What Changes

- **BREAKING**: Target `net10.0` only for all projects (core library, CLI, OpenApi package, tests, samples)
- Remove all `#if NET9_0_OR_GREATER` / `#if !NET9_0_OR_GREATER` / `#if NET10_0_OR_GREATER` conditional compilation
- Remove `Microsoft.OpenApi` 1.6.x and `Microsoft.OpenApi.Readers` package references — use `Microsoft.OpenApi` 3.x (+ `Microsoft.OpenApi.YamlReader`) only
- Remove v1 code paths: `LoadV1()`, `OpenApiStreamReader`, `OperationType` enum-based `MapHttpMethod`, `Microsoft.OpenApi.Models` namespace usages, `OpenApiString` casts
- Keep only v3 code paths: `OpenApiDocument.Parse()`, `ReadResult`, string-based `MapHttpMethod`, `Microsoft.OpenApi` namespace, `JsonNodeExtension`
- Fix `SchemaTransformer` vendor extension reading to use v3 API instead of `OpenApiString` cast
- Remove conditional package reference ItemGroups from `.csproj` files — single unconditional references
- Update test project `Microsoft.OpenApi.Readers` / `OpenApiStringReader` usages to v3 equivalents

## Capabilities

### New Capabilities
_(none — this is a simplification change, not a feature addition)_

### Modified Capabilities
_(no spec-level behavior changes — this is purely an implementation/dependency change)_

## Impact

- `src/ApiStitch/ApiStitch.csproj` — single-target `net10.0`, single `Microsoft.OpenApi` 3.x reference (+ `Microsoft.OpenApi.YamlReader`), drop `Microsoft.OpenApi.Readers`
- `src/ApiStitch/Parsing/OpenApiSpecLoader.cs` — remove `LoadV1()`, inline the v3 parse path as `Load`
- `src/ApiStitch/Parsing/OperationTransformer.cs` — remove `OperationType` enum `MapHttpMethod`, keep string version; remove `Microsoft.OpenApi.Models` import
- `src/ApiStitch/Parsing/SchemaTransformer.cs` — remove `Microsoft.OpenApi.Models` import; fix `OpenApiString` cast to v3 extension reading
- `src/ApiStitch.OpenApi/ApiStitch.OpenApi.csproj` — single-target `net10.0`, single `Microsoft.AspNetCore.OpenApi` 10.x reference
- `src/ApiStitch.OpenApi/ApiStitchTypeInfoSchemaTransformer.cs` — remove conditional compilation, keep `JsonNodeExtension` path only
- `src/ApiStitch.Cli/ApiStitch.Cli.csproj` — target `net10.0`
- `samples/SampleApi/SampleApi.csproj` — already `net10.0`, no change
- `tests/ApiStitch.Tests/ApiStitch.Tests.csproj` — target `net10.0`, update `OpenApiStringReader` to v3 API
- `tests/ApiStitch.IntegrationTests/ApiStitch.IntegrationTests.csproj` — target `net10.0`
- `tests/ApiStitch.OpenApi.Tests/ApiStitch.OpenApi.Tests.csproj` — already `net9.0`, update to `net10.0`
