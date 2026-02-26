## Context

ApiStitch is a consumer-side OpenAPI client generator. It reads spec files that have already been stripped of CLR type information during OpenAPI document generation. To enable type reuse (skipping generation of types the consumer already has), ApiStitch needs the original .NET type names.

`Microsoft.Extensions.ApiDescription.Server` already boots the API at build time to generate the spec via `dotnet-getdocument.dll`. The `IOpenApiSchemaTransformer` pipeline in `Microsoft.AspNetCore.OpenApi` runs during this process and has full access to `System.Type` via `context.JsonTypeInfo.Type`. We piggyback on this existing infrastructure.

The entry assembly during build-time spec generation is `GetDocument.Insider` — this is a stable, documented detection mechanism.

## Goals / Non-Goals

**Goals:**
- Provide a NuGet package (`ApiStitch.OpenApi`) that API projects reference to enrich their OpenAPI specs with CLR type names
- Write `x-apistitch-type` extension with `Type.FullName` on user-defined schemas
- Provide `ApiStitchDetection.IsOpenApiGenerationOnly` static helper so API teams can guard heavy startup dependencies during build-time spec generation
- Make type enrichment configurable: always-on during build-time generation, opt-in at runtime
- Keep the package tiny — one transformer, one helper, one registration method

**Non-Goals:**
- Consumer-side reading of `x-apistitch-type` (separate change in core ApiStitch)
- Replacing or competing with `Microsoft.Extensions.ApiDescription.Server` infrastructure
- Supporting Swashbuckle (it's sunset)
- Property-level type enrichment (only schema-level)

## Decisions

### 1. Extension name: `x-apistitch-type`

Use a tool-prefixed vendor extension rather than a generic `x-csharp-type`. No established convention exists for CLR type names in OpenAPI extensions (checked OpenAPI Generator wiki, NSwag, Kiota — none define one). Tool-prefixing avoids future collisions.

**Alternative**: `x-dotnet-type` or `x-csharp-type` — rejected because no standard exists and we'd risk collision with other tools.

### 2. Value format: `Type.FullName` (namespace-qualified, no assembly)

Write `SampleApi.Models.Pet`, not `SampleApi.Models.Pet, SampleApi`. The namespace-qualified name is sufficient for type matching. Assembly names change between projects (producer vs consumer), so including them would prevent matching.

Guard against `Type.FullName` returning `null` (can happen for open generic parameters, dynamic assembly types, compiler-generated types) — skip those schemas.

For nested types, `Type.FullName` uses the `+` separator (e.g., `Outer+Inner`). The consumer-side reader must handle this format when matching types.

For closed generic types, raw `Type.FullName` includes assembly-qualified type arguments (e.g., `` PagedResult`1[[Pet, Assembly, ...]] ``), which is unusable for cross-project matching. A `GetCleanFullName` helper strips assembly information and produces a clean C#-style format:

```csharp
static string? GetCleanFullName(Type type)
{
    if (!type.IsGenericType) return type.FullName;

    var baseName = type.GetGenericTypeDefinition().FullName;
    if (baseName is null) return null;

    var backtickIndex = baseName.IndexOf('`');
    if (backtickIndex >= 0) baseName = baseName[..backtickIndex];

    var args = type.GetGenericArguments();
    var argNames = new string[args.Length];
    for (var i = 0; i < args.Length; i++)
    {
        var argName = GetCleanFullName(args[i]);
        if (argName is null) return null;
        argNames[i] = argName;
    }

    return $"{baseName}<{string.Join(", ", argNames)}>";
}
```

This produces clean values like `MyNamespace.PagedResult<MyNamespace.Pet>` or `MyNamespace.Result<MyNamespace.Pet, MyNamespace.Error>`. The transformer calls `GetCleanFullName` instead of `Type.FullName` directly.

**Alternative**: `Type.AssemblyQualifiedName` — rejected because the assembly name differs between producer and consumer projects. `Type.Name` alone — rejected because it's ambiguous when multiple namespaces have the same type name.

### 3. Configurable emission: build-time always, runtime opt-in

The transformer accepts an options object:

```csharp
public sealed class ApiStitchTypeInfoOptions
{
    public bool AlwaysEmit { get; set; } = false;
}
```

Behaviour:
- **Build-time generation** (`GetDocument.Insider`): always emits `x-apistitch-type`, regardless of `AlwaysEmit`. This is the primary use case — generating a spec for client generation.
- **Runtime** (app serving `/openapi/v1.json`): only emits if `AlwaysEmit = true`. Defaults to `false` because runtime specs may be public-facing, and leaking internal CLR type names is undesirable.

**Rationale**: Build-time is the happy path for ApiStitch consumers. `AlwaysEmit` is an opt-in escape hatch for teams that consume the spec directly from the running API rather than from a build artifact.

### 4. Characteristic-based type filtering with well-known skip-sets

Skip types that map directly to JSON Schema primitives/formats, and skip collection types (whose element types get their own separate transformer invocation). Annotate everything else — including user-defined closed generics and framework types like `ProblemDetails` — because these are real domain types that consumers may want to reuse.

```csharp
private static readonly HashSet<Type> WellKnownTypes =
[
    typeof(string), typeof(decimal), typeof(object),
    typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly),
    typeof(TimeSpan), typeof(Guid), typeof(Uri), typeof(Half),
];

private static readonly HashSet<Type> CollectionDefinitions =
[
    typeof(List<>), typeof(IList<>), typeof(ICollection<>),
    typeof(IEnumerable<>), typeof(IReadOnlyList<>), typeof(IReadOnlyCollection<>),
    typeof(HashSet<>), typeof(ISet<>), typeof(IReadOnlySet<>),
    typeof(Dictionary<,>), typeof(IDictionary<,>), typeof(IReadOnlyDictionary<,>),
];

static bool IsUserDefinedType(Type type)
{
    var t = Nullable.GetUnderlyingType(type) ?? type;
    if (t.IsArray) return false;
    if (t.IsPrimitive) return false;
    if (WellKnownTypes.Contains(t)) return false;
    if (t.IsGenericType && CollectionDefinitions.Contains(t.GetGenericTypeDefinition())) return false;
    if (t.FullName is null) return false;
    return true;
}
```

This correctly handles:
- All primitive types (`int`, `bool`, `byte`, etc.) — `IsPrimitive` check
- Well-known BCL value types (`DateTime`, `Guid`, `TimeSpan`, `Half`, etc.) — explicit skip-set
- Nullable wrappers (`int?`, `DateTime?`, etc.) — unwrapped before checking
- Collection types (`List<Pet>`, `IReadOnlyList<Pet>`, `Dictionary<string, Pet>`) — skipped via `CollectionDefinitions` matching the generic type definition. Element types get their own transformer invocation.
- Arrays (`Pet[]`) — skipped because `IsArray` is true
- User-defined closed generics (`PagedResult<Pet>`, `Result<Pet, Error>`) — correctly included (not in collection definitions). Extension value uses `GetCleanFullName` for a clean format.
- User-defined enums (`PetStatus`) — correctly included (not primitive, not in skip-set)
- User-defined structs (`Money`) — correctly included (not primitive, not in skip-set)
- Framework domain types (`ProblemDetails`, `JsonPatchDocument`) — correctly included (not primitive, not in skip-set)

**Important**: The schema transformer is invoked for every schema instance in the document, not just component schemas. For the same CLR type appearing in multiple operations, the transformer runs multiple times. After all transformers run, the framework extracts schemas to `components.schemas` and replaces duplicates with `$ref` — so the extension ends up in the right place. The transformer is idempotent per schema instance. The ASP.NET Core OpenAPI pipeline invokes schema transformers sequentially per schema, not in parallel, so mutating `schema.Extensions` is safe.

### 5. Target framework: `net9.0` and `net10.0` with conditional compilation

`IOpenApiSchemaTransformer` was introduced in .NET 9. Multi-target `net9.0;net10.0` to cover both LTS versions.

**Critical**: ASP.NET Core 10 migrated from `Microsoft.OpenApi` 1.x to `Microsoft.OpenApi` 2.x. The namespace and types differ between versions. The extension-writing code requires `#if` conditional compilation:

```csharp
var typeName = GetCleanFullName(context.JsonTypeInfo.Type);
#if NET10_0_OR_GREATER
schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
schema.Extensions["x-apistitch-type"] = new JsonNodeExtension(typeName);
#else
schema.Extensions["x-apistitch-type"] = new OpenApiString(typeName);
#endif
```

The csproj must version-condition the package references:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net9.0'">
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.*" />
</ItemGroup>
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.*" />
</ItemGroup>
```

### 6. Registration pattern: extension method on `OpenApiOptions`

```csharp
builder.Services.AddOpenApi(options =>
{
    options.AddApiStitchTypeInfo();
});

// With configuration:
builder.Services.AddOpenApi(options =>
{
    options.AddApiStitchTypeInfo(o => o.AlwaysEmit = true);
});
```

Follows the established ASP.NET Core pattern. Overload accepting `Action<ApiStitchTypeInfoOptions>` for configuration.

### 7. `IsOpenApiGenerationOnly` as a static helper, not an extension method

```csharp
public static class ApiStitchDetection
{
    public static bool IsOpenApiGenerationOnly { get; } =
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
```

Static property (not extension method) because the check is process-global — it inspects `Assembly.GetEntryAssembly()`, not anything on the builder. Making it an extension method would falsely imply the builder matters.

Cached as a static auto-property with initialiser, computed once. No per-call overhead.

Usage:

```csharp
if (!ApiStitchDetection.IsOpenApiGenerationOnly)
{
    builder.Services.AddDbContext<AppDbContext>(...);
    builder.Services.AddAuthentication().AddJwtBearer(...);
}
```

### 8. NuGet packaging: FrameworkReference + PackageReference

The csproj uses `FrameworkReference` for the ASP.NET Core shared framework and `PackageReference` for the OpenApi package:

```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

The version-conditioned `PackageReference` for `Microsoft.AspNetCore.OpenApi` flows transitively via NuGet. This is intentional — NuGet handles version unification well for Microsoft packages, and "just works" beats "read the README". If the consuming API project already has a direct reference (as expected), NuGet resolves to the higher version.

## Risks / Trade-offs

- **[Risk] `GetDocument.Insider` name could change in future .NET versions** → Mitigation: It's been stable since the package was created. If it changes, we release a patch. The check is isolated to one static property.
- **[Risk] Microsoft.OpenApi v1→v2 break between net9.0 and net10.0** → Mitigation: Conditional compilation with `#if NET10_0_OR_GREATER`. Both code paths are simple (one-line extension write). When .NET 11 ships, `NET10_0_OR_GREATER` will cover it if the OpenApi 2.x API surface remains stable.
- **[Risk] Transformer runs for every schema instance, not just components** → Mitigation: Idempotent. The framework's post-transformer `$ref` extraction ensures the extension ends up on the component schema.
- **[Trade-off] Separate package vs. bundled in core** → Separate package is correct because it has an ASP.NET Core dependency that the consumer-side core library should not take.
