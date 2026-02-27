## Context

The ApiStitch code generator has a clean pipeline: `ConfigLoader → OpenApiSpecLoader → SchemaTransformer → InheritanceDetector → CSharpTypeMapper → OperationTransformer → ModelEmitter → ClientEmitter`. The producer-side `ApiStitch.OpenApi` package already writes `x-apistitch-type` vendor extensions onto OpenAPI schemas with CLR fully-qualified type names.

The generator currently treats every component schema as something to generate — there's no concept of "this type already exists externally." This change introduces that concept: reading the vendor extension, deciding whether to reuse or regenerate, and propagating external type names through the emission layer.

The `ApiSchema` model already has a mutable `CSharpTypeName` property set by `CSharpTypeMapper`. The `ScribanModelEmitter` iterates all schemas and emits a `.cs` file per Object/Enum. The `JsonSerializerContext.sbn-cs` template emits `[JsonSerializable(typeof(X))]` for each type name. The `Record.sbn-cs` template uses `{{ base_name }}` for inheritance.

## Goals / Non-Goals

**Goals:**
- Read `x-apistitch-type` vendor extensions from OpenAPI specs and use them for automatic type reuse
- Allow users to exclude specific namespaces or types from reuse via `apistitch.yaml` configuration
- Skip code generation for external types while still including them in `[JsonSerializable]` attributes
- Handle external types correctly in all contexts: properties, operation parameters, request/response bodies, inheritance bases, collection items, enum query serialization

**Non-Goals:**
- Attribute-based type discovery (`[OpenApiSchema]` scanning via Roslyn) — separate future change
- Namespace remapping (mapping `Producer.Models.Pet` → `Consumer.Models.Pet`) — out of scope
- `includeNamespaces` allowlist filtering — deferred until a concrete user scenario demands it
- Language-agnostic model refactoring — there's a separate pending change for that

## Decisions

### Decision 1: Separate `ExternalTypeResolver` pipeline step

**Choice:** Introduce `ExternalTypeResolver` as a new pipeline step between `InheritanceDetector` and `CSharpTypeMapper`.

**Alternatives considered:**
- Inline the logic in `SchemaTransformer` — rejected because it conflates parsing (what is this schema?) with policy (should we reuse this type?). The existing pipeline has clean separation: `SchemaTransformer` handles shape, `InheritanceDetector` handles relationships, `CSharpTypeMapper` handles naming.
- Inline the logic in `CSharpTypeMapper` — rejected because the type mapper is a pure function that maps schema kinds to C# type strings. Adding exclusion pattern matching and nested type normalisation muddies that responsibility.

**How it works:**
1. `SchemaTransformer` reads the raw `x-apistitch-type` extension string during transformation and stashes it on `ApiSchema.VendorTypeHint` (no policy applied)
2. `ExternalTypeResolver.Resolve(specification, config)` iterates all schemas, checks `VendorTypeHint` against exclusion patterns, normalises the type name (nested type `+` to `.`), and sets `ExternalClrTypeName`
3. `CSharpTypeMapper.MapAll` checks `IsExternal` — if true, sets `CSharpTypeName = ExternalClrTypeName` instead of deriving from the schema kind

Updated pipeline order:
```
ConfigLoader → OpenApiSpecLoader → SchemaTransformer → InheritanceDetector → ExternalTypeResolver → CSharpTypeMapper → OperationTransformer → ModelEmitter → ClientEmitter
```

`ExternalTypeResolver` runs after `InheritanceDetector` because inheritance detection operates on the semantic model structure (allOf patterns) which is independent of whether a type is external. The resolver then marks types as external, and `CSharpTypeMapper` respects that.

### Decision 2: `VendorTypeHint` on ApiSchema for raw extension value

**Choice:** Add `string? VendorTypeHint` to `ApiSchema`, set by `SchemaTransformer` when reading the `x-apistitch-type` extension.

The raw value is stashed without processing so that `ExternalTypeResolver` can apply config-driven policy separately. This avoids `SchemaTransformer` needing access to `ApiStitchConfig`.

**Reading the extension in SchemaTransformer:**

The `SchemaTransformer` receives an `OpenApiSchema` which has `Extensions: IDictionary<string, IOpenApiExtension>`. When `x-apistitch-type` is present:

```csharp
if (openApiSchema.Extensions.TryGetValue("x-apistitch-type", out var ext)
    && ext is OpenApiString str
    && !string.IsNullOrWhiteSpace(str.Value))
{
    schema.VendorTypeHint = str.Value;
}
```

Note: This uses Microsoft.OpenApi v1 types (`OpenApiString`). The core `ApiStitch` library targets `net8.0` and uses `Microsoft.OpenApi` 1.x. The v2 extension types (`JsonNodeExtension`) are only relevant for the producer-side `ApiStitch.OpenApi` package which multi-targets net9.0/net10.0. The consumer reads pre-generated spec files where extensions are always deserialised as `OpenApiString` by the Microsoft.OpenApi reader.

### Decision 3: `ExternalClrTypeName` and computed `IsExternal`

**Choice:** Add `string? ExternalClrTypeName { get; set; }` and `bool IsExternal => ExternalClrTypeName is not null` to `ApiSchema`.

This avoids two sources of truth. A schema is external if and only if `ExternalClrTypeName` has been set by `ExternalTypeResolver`.

### Decision 4: Nested type normalisation

**Choice:** `ExternalTypeResolver` replaces `+` with `.` in the vendor type hint before storing it as `ExternalClrTypeName`.

`Type.FullName` uses `+` for nested types (e.g., `Outer+Inner`). C# code requires `.` (e.g., `Outer.Inner`). The normalisation happens once in `ExternalTypeResolver` so all downstream consumers get a valid C# identifier.

### Decision 5: Fully-qualified names for external types everywhere

**Choice:** External types use their fully-qualified CLR type name (e.g., `SampleApi.Models.Pet`) in all generated code. No `using` directives are added for external namespaces.

**Alternatives considered:**
- Add `using` directives for external namespaces — rejected because it risks namespace collisions with the generated namespace and requires tracking unique namespaces across all external types. Fully-qualified names are verbose but deterministic.

This means:
- `CSharpTypeMapper` sets `CSharpTypeName = ExternalClrTypeName` (the FQN)
- Properties referencing external types emit the FQN as the type: `public required SampleApi.Models.Pet Pet { get; init; }`
- Base class declarations use the FQN: `public sealed partial record Dog : SampleApi.Models.Animal`
- `[JsonSerializable(typeof(SampleApi.Models.Pet))]` uses the FQN
- Operation return types use the FQN: `Task<SampleApi.Models.Pet>`

### Decision 6: Configuration under `typeReuse:` section

**Choice:** Nest exclusion config under a `typeReuse:` section in `apistitch.yaml`.

```yaml
spec: openapi.json
namespace: MyApp.Client
typeReuse:
  excludeNamespaces:
    - "Microsoft.AspNetCore.*"
    - "System.*"
  excludeTypes:
    - "Microsoft.AspNetCore.Mvc.ProblemDetails"
```

**Config model:**

```csharp
public class TypeReuseConfig
{
    public List<string> ExcludeNamespaces { get; init; } = [];
    public List<string> ExcludeTypes { get; init; } = [];
}
```

Add `TypeReuse` property to `ApiStitchConfig`:
```csharp
public TypeReuseConfig TypeReuse { get; init; } = new();
```

Add `TypeReuseDto` to `ConfigDto` in `ConfigLoader`:
```csharp
public TypeReuseDto? TypeReuse { get; set; }
```

**Pattern matching:** `excludeNamespaces` uses simple glob patterns where `*` matches any sequence of characters. The matching is performed against the full type name (e.g., `Microsoft.AspNetCore.*` matches `Microsoft.AspNetCore.Mvc.ProblemDetails`). `excludeTypes` uses exact string comparison.

**Glob pattern semantics:** Patterns are anchored — they must match the full type name from start to end. The `.` character is literal (not a regex wildcard). Only `*` is a wildcard, matching any sequence of characters. Implementation: convert pattern to regex via `"^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$"`. This means `System.*` matches `System.String` and `System.Collections.Generic.List` but NOT `SystemMonitor.Types.Foo` (because the `.` after `System` is literal and must match a literal dot in the input).

When an `x-apistitch-type` value is present but excluded by config, the schema is treated as non-external (regenerated), and a diagnostic `AS500` (info severity) is emitted: `"Type '{typeName}' excluded from reuse by configuration. Code will be generated."`.

**Note on generic types in exclusion patterns:** If the vendor hint contains angle brackets (e.g., `SampleApi.Models.PagedResult<SampleApi.Models.Pet>`), the exclusion pattern is matched against the full string including type arguments. A pattern like `SampleApi.*` will match because the string starts with `SampleApi.` — the container type's namespace determines the match. This is intentional: the container type's namespace is what matters for reuse decisions.

### Decision 7: Model emission — skip external, keep in JsonSerializerContext

**Choice:** `ScribanModelEmitter` skips external schemas when emitting `.cs` files but includes them in `[JsonSerializable]` attributes.

**Skip logic in `ScribanModelEmitter.Emit`:**
```csharp
foreach (var schema in spec.Schemas.OrderBy(s => s.Name, StringComparer.Ordinal))
{
    if (schema.IsExternal)
    {
        // Don't emit a .cs file, but do track for JsonSerializerContext
        typeNames.Add(schema.CSharpTypeName!);  // FQN for external types
        continue;
    }
    // ... existing Object/Enum emission
}
```

The `JsonSerializerContext.sbn-cs` template already emits `[JsonSerializable(typeof({{ type_name }}))]` for each type name. Currently these are short names (e.g., `Pet`) that resolve within the generated namespace. For external types, the FQN (e.g., `SampleApi.Models.Pet`) resolves because the consumer project must already reference the external assembly.

This ensures AOT/trimming compatibility: the generated `JsonSerializerContext` includes metadata for all types used in serialization, including external ones.

### Decision 8: External enum handling — no EnumExtensions

**Choice:** External enums skip `.cs` file emission AND skip `EnumExtensions` generation. Query parameter serialization for external enums falls back to `.ToString()`.

**Why not emit EnumExtensions for external enums?** The `ToQueryString()` extension method maps C# enum member names to wire values using a `switch` expression (e.g., `PetStatus.Available => "available"`). The member names are derived by PascalCasing the OpenAPI `enum` wire values. But the external enum's actual member names are set by the external assembly's author — they might use different casing, different names entirely, or `[EnumMember]`/`[JsonStringEnumMemberName]` attributes that map member names to wire values differently. The generator has no way to know the actual member names without reflecting over or analysing the external assembly.

**Fallback strategy:** When an external enum is used as a query parameter, the `ScribanClientEmitter` uses `.ToString()` instead of `.ToQueryString()`. This works when the enum member names match the wire values (the common case for PascalCase enums with string serialization). If they don't match, the user must handle query serialization manually — this is documented as a known limitation.

**Implementation:** In `ScribanClientEmitter.BuildParamModel` and `BuildQueryParamModel`, check `param.Schema.IsExternal` (or `param.Schema.ArrayItemSchema?.IsExternal` for array-of-enum) before adding to the `queryEnums` set. External enums are excluded from `queryEnums`, so no `EnumExtensions` file is emitted for them. The `toStringExpr` for external enum parameters uses `.ToString()` instead of `.ToQueryString()`.

### Decision 9: Inheritance with external types

**Base is external, derived types are not:**
- `InheritanceDetector` runs before `ExternalTypeResolver`, so it still detects the inheritance pattern and sets `BaseSchema`
- `ExternalTypeResolver` marks the base as external and sets its `ExternalClrTypeName`
- `CSharpTypeMapper` sets the base's `CSharpTypeName` to the FQN
- The `Record.sbn-cs` template uses `{{ base_name }}` — the `ScribanModelEmitter` must set `base_name` to `schema.BaseSchema.CSharpTypeName` (which is the FQN for external bases) instead of `schema.BaseSchema.Name` (the short name)

Currently:
```csharp
model.Add("base_name", schema.BaseSchema?.Name);
```

Change to:
```csharp
model.Add("base_name", schema.BaseSchema?.CSharpTypeName);
```

No fallback to `?.Name` — `CSharpTypeName` is always set by `CSharpTypeMapper.MapAll` before the emitter runs. A null `CSharpTypeName` at emit time would be a pipeline bug that should surface clearly rather than silently falling back to the wrong value.

**Derived is external, base is not:**
- The derived schema is skipped during emission (no `.cs` file)
- The base schema is still generated normally
- `InheritanceDetector` still strips base properties from the derived schema, but since the derived is never emitted, this is harmless wasted work

**Both are external:**
- Both are skipped during emission
- Both appear in `[JsonSerializable]` with their FQNs

### Decision 10: `CSharpTypeMapper` changes

**Choice:** `CSharpTypeMapper.MapAll` and `MapSchema` check `IsExternal` before applying default mapping.

```csharp
public static void MapAll(ApiSpecification specification)
{
    foreach (var schema in specification.Schemas)
    {
        if (schema.IsExternal)
            schema.CSharpTypeName = schema.ExternalClrTypeName;
        else
            schema.CSharpTypeName = MapSchema(schema);
    }
}
```

`MapSchema` (used for inline/property schemas) also needs to check:
```csharp
internal static string MapSchema(ApiSchema schema)
{
    if (schema.IsExternal)
        return schema.ExternalClrTypeName!;

    return schema.Kind switch { ... };
}
```

This ensures that when an array item is external (e.g., `IReadOnlyList<SampleApi.Models.Pet>`), the collection type name is built correctly.

### Decision 11: Diagnostic codes

| Code | Severity | Meaning |
|------|----------|---------|
| AS500 | Info | Type excluded from reuse by configuration |

Note: AS400 series is already used by `OperationTransformer` (AS400–AS405).

### Decision 12: Vendor type hints only from component schemas

`VendorTypeHint` is only read from component schemas (top-level entries in `#/components/schemas/`). Inline property schemas go through `GetOrTransformPropertySchema` which only calls `TransformSchema` for `$ref` targets. In practice this is correct — the producer-side `ApiStitchTypeInfoSchemaTransformer` only fires for component schemas registered with the JSON serializer, so inline schemas won't carry the extension.

### Decision 13: Nested type `+` replacement applies to entire string

The `+` to `.` normalisation in `ExternalTypeResolver` uses a simple `string.Replace("+", ".")` on the entire vendor type hint. This correctly handles nested types within generic type arguments (e.g., `Outer+Inner<Foo+Bar>` → `Outer.Inner<Foo.Bar>`).

## Risks / Trade-offs

**[Risk] External type doesn't match the OpenAPI schema shape** → The generator trusts that the external type matches the spec. If the external type has different properties, serialization will fail at runtime. This is the user's responsibility — the generator can't validate external assemblies. Mitigation: clear documentation that type reuse requires the external type to match the schema.

**[Risk] External assembly not referenced by consumer project** → If `[JsonSerializable(typeof(ExternalType))]` is emitted but the consumer project doesn't reference the external assembly, the build will fail with a clear compiler error (CS0246). This is self-diagnosing.

**[Risk] Dual JsonSerializerContext for same type** → The consumer's generated context emits `[JsonSerializable(typeof(ExternalType))]`. The external assembly may also have its own context for that type. This is safe: STJ source generation produces separate context classes with independent metadata. `[JsonPropertyName]` and other attributes on the external type take precedence over the context's naming policy, so serialization is correct regardless of which context compiled the metadata. The actual serialization behavior depends on which context is used at runtime.

**[Trade-off] Fully-qualified names are verbose** → Generated code like `public required SampleApi.Models.V2.Pet Pet { get; init; }` is wordy. The alternative (adding `using` directives) risks namespace collisions and adds complexity. Accepted: generated code is rarely read by humans, and FQNs are unambiguous.

**[Trade-off] External enum query serialization uses `.ToString()`** → External enums used as query parameters fall back to `.ToString()` instead of the spec-aware `ToQueryString()`. This works when enum member names match wire values (the common case) but may produce incorrect query strings if the external enum uses non-standard naming. Documented as a known limitation.
