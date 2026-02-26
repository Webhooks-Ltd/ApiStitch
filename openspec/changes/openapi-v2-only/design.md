## Context

The codebase was built during the net8.0→net9.0 transition when Microsoft.OpenApi shipped a breaking v2 release. To support both, we multi-targeted (`net8.0;net9.0` for core, `net9.0;net10.0` for OpenApi producer) with `#if NET9_0_OR_GREATER` and `#if NET10_0_OR_GREATER` preprocessor directives throughout. This resulted in duplicated code paths for:

1. **Spec loading** — `OpenApiStreamReader` (v1) vs `OpenApiDocument.Parse` (v2)
2. **Operation type mapping** — `OperationType` enum (v1) vs `operationType.Method` string (v2)
3. **Namespace imports** — `Microsoft.OpenApi.Models` (v1) vs `Microsoft.OpenApi` (v2)
4. **Vendor extension writing** — `OpenApiString` (v1) vs `JsonNodeExtension` (v2)
5. **Vendor extension reading** — `OpenApiString` cast (v1) vs v2 API
6. **Test helpers** — `OpenApiStringReader` (v1, from `Microsoft.OpenApi.Readers` package)

net10.0 is now the current LTS. net9.0 goes EOL May 2026. There's no reason to maintain v1 compatibility.

## Goals / Non-Goals

**Goals:**
- Target `net10.0` everywhere — all src, test, and sample projects
- Remove all conditional compilation (`#if`) blocks
- Remove `Microsoft.OpenApi.Readers` package (v1-only, merged into `Microsoft.OpenApi` v2)
- Keep only the v2 code paths, simplified and inlined
- All existing tests pass on the new single target

**Non-Goals:**
- Behavioral changes to the generator output
- New features or capabilities
- Changing the generated code's target framework (generated output is independent of the generator's TFM)

## Decisions

### D1: net10.0 for everything

Target `net10.0` for all projects. net10.0 is LTS, Microsoft.OpenApi 3.x is the latest release, and `Microsoft.AspNetCore.OpenApi` 10.x aligns.

Projects affected:
| Project | From | To |
|---------|------|----|
| `ApiStitch` | `net8.0;net9.0` | `net10.0` |
| `ApiStitch.Cli` | `net8.0` | `net10.0` |
| `ApiStitch.OpenApi` | `net9.0;net10.0` | `net10.0` |
| `ApiStitch.Tests` | `net8.0` | `net10.0` |
| `ApiStitch.IntegrationTests` | `net8.0` | `net10.0` |
| `ApiStitch.OpenApi.Tests` | `net9.0` | `net10.0` |
| `SampleApi` | `net10.0` | `net10.0` (no change) |

### D2: Microsoft.OpenApi package references

Replace conditional ItemGroups with single unconditional references. Use Microsoft.OpenApi 3.x (latest) instead of 2.x. YAML reading is no longer built into the core package — add `Microsoft.OpenApi.YamlReader` 3.x as a separate dependency.

**ApiStitch.csproj:**
- Remove: `Microsoft.OpenApi` 1.6.x (net8.0), `Microsoft.OpenApi.Readers` 1.6.x (net8.0)
- Remove: conditional `ItemGroup` blocks
- Add: `Microsoft.OpenApi` 3.x and `Microsoft.OpenApi.YamlReader` 3.x as single unconditional references

**ApiStitch.OpenApi.csproj:**
- Remove: conditional `ItemGroup` blocks for net9.0 and net10.0
- Keep: `Microsoft.AspNetCore.OpenApi` 10.x and `Microsoft.Extensions.ApiDescription.Server` 10.x as single unconditional references

### D3: OpenApiSpecLoader — inline LoadV2, delete LoadV1

Remove `LoadV1()` entirely. Inline `LoadV2()` as the body of `Load()`. Remove the `#if` dispatch. The v3 path uses `OpenApiDocument.Parse(content, settings: settings)` which returns a `ReadResult` with `Document` and `Diagnostic` (singular).

YAML support requires explicit registration: create `OpenApiReaderSettings`, call `settings.AddYamlReader()`, and pass settings to `Parse()`.

The v1 `OpenApiSpecVersion.OpenApi2_0` rejection check is dropped — v3 natively supports Swagger 2.0 (via upconversion) and OpenAPI 3.1.

### D4: OperationTransformer — keep string-based MapHttpMethod

Remove the `OperationType` enum-based `MapHttpMethod(OperationType)` overload. Keep the `MapHttpMethod(string method)` overload. In the iteration loop, use `operationType.Method` directly (no `#if`).

### D5: SchemaTransformer — v2 vendor extension reading

The current code reads `x-apistitch-type` via:
```csharp
ext is Microsoft.OpenApi.Any.OpenApiString str
```

In Microsoft.OpenApi v2, extensions are stored as `JsonNodeExtension`. The reading pattern becomes:
```csharp
if (openApiSchema.Extensions.TryGetValue("x-apistitch-type", out var ext)
    && ext is JsonNodeExtension jsonExt
    && jsonExt.Node is JsonValue jsonValue
    && jsonValue.TryGetValue(out string? strValue)
    && !string.IsNullOrWhiteSpace(strValue))
{
    schema.VendorTypeHint = strValue;
}
```

Similarly for enum member values — `OpenApiString` casts become string extraction from the v2 enum representation.

For enum values in v2, `OpenApiSchema.Enum` contains `JsonNode` items. The extraction becomes:
```csharp
var value = (e as JsonValue)?.ToString() ?? e.ToString() ?? "";
```

### D6: ApiStitchTypeInfoSchemaTransformer — keep JsonNodeExtension path

Remove the `#if NET10_0_OR_GREATER` block. Keep only the `JsonNodeExtension` code for writing `x-apistitch-type`:
```csharp
schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
schema.Extensions["x-apistitch-type"] = new JsonNodeExtension(typeName);
```

Remove `using Microsoft.OpenApi.Any` — no longer needed.

### D7: Test helper migration — OpenApiStringReader → OpenApiDocument.Parse

Tests currently use `new OpenApiStringReader().Read(yaml, out diagnostic)` from `Microsoft.OpenApi.Readers`. In v3, this becomes:
```csharp
var settings = new OpenApiReaderSettings();
settings.AddYamlReader();
return OpenApiDocument.Parse(yaml, settings: settings).Document!;
```

The `Microsoft.OpenApi.Readers` package reference is removed from test projects. The `Microsoft.OpenApi.Models` using directive becomes `Microsoft.OpenApi`. YAML reader settings are shared via a static field to avoid repeated allocation.

### D8: Namespace changes

| v1 | v3 |
|----|-----|
| `using Microsoft.OpenApi.Models;` | `using Microsoft.OpenApi;` |
| `using Microsoft.OpenApi.Readers;` | `using Microsoft.OpenApi.Reader;` (for `OpenApiReaderSettings`) |
| `using Microsoft.OpenApi.Any;` | `using System.Text.Json.Nodes;` (for `JsonValue`) |
| `Microsoft.OpenApi.Models.ParameterLocation` | `Microsoft.OpenApi.ParameterLocation` |
| `OpenApiSchema` (concrete) | `IOpenApiSchema` (interface) + `OpenApiSchemaReference` for `$ref` |
| `ReadResult.Diagnostics` | `ReadResult.Diagnostic` (singular) |

Key v3 changes beyond v2:
- `$ref` schemas are `OpenApiSchemaReference` instances (not shared object identity with component schemas). Use `schemaRef.Target` to get the underlying `OpenApiSchema`.
- `OpenApiSchema.Type` is `JsonSchemaType?` (flags enum), not `string`. Strip `JsonSchemaType.Null` flag for base type comparison.
- `OpenApiSchema.Nullable` removed — nullable is encoded as `Type.HasFlag(JsonSchemaType.Null)`.
- Collections return `IOpenApiSchema` interfaces: `Components.Schemas`, `Properties`, `AllOf`, `Items`.
- Parameters/request bodies use interfaces: `IOpenApiParameter`, `IOpenApiRequestBody`.

## Risks / Trade-offs

**[Risk] net10.0 SDK not installed on contributor machines** → net10.0 has been GA since Nov 2025. CI and local dev require the SDK. The `global.json` (if present) should pin to a 10.x SDK. Self-diagnosing: build fails immediately with a clear error.

**[Risk] Microsoft.OpenApi v3 `$ref` identity change** → v3 wraps `$ref` schemas in `OpenApiSchemaReference` instead of sharing object identity. Required `ResolveRef()` helpers in `SchemaTransformer` and `OperationTransformer` to unwrap references before dictionary lookups. Mitigation: all existing tests pass including integration tests with real specs.

**[Trade-off] Dropped net8.0/net9.0 consumer support for ApiStitch CLI** → Users on older runtimes cannot run the CLI tool. Acceptable: net10.0 is LTS, and the CLI is a developer tool (not a production dependency).

**[Trade-off] Swagger 2.0 and OpenAPI 3.1 now accepted** → v1 rejected these with explicit errors. v3 parses them natively (Swagger 2.0 via upconversion). Tests updated to reflect the new behavior.
