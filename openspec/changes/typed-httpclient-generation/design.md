## Context

The generation pipeline currently runs: `OpenApiSpecLoader` → `SchemaTransformer` → `InheritanceDetector` → `CSharpTypeMapper` → `ScribanModelEmitter`. This produces model records, enums, and a `JsonSerializerContext`. The semantic model types `ApiOperation`, `ApiParameter`, and `ApiResponse` exist as empty stubs. `SchemaTransformer` only processes `document.Components.Schemas` — `document.Paths` is ignored entirely, and `ApiSpecification.Operations` is always `[]`.

This change adds operation parsing and typed HttpClient wrapper emission to produce a complete, usable API client from an OpenAPI spec.

## Goals / Non-Goals

**Goals:**
- Parse OpenAPI paths/operations into the semantic model with full parameter and response schema resolution
- Generate typed HttpClient wrappers (interface + implementation) grouped by tag
- Generate DI registration returning `IHttpClientBuilder` for Polly chaining
- Generate `ApiException`, client options, JSON options wrapper, enum query string extensions
- Maintain zero third-party runtime dependencies in generated output
- Full AOT/trimming compatibility (explicit `JsonSerializerOptions` on every serialization call)
- Deterministic, diff-friendly output consistent with existing model emission

**Non-Goals:**
- Type reuse (YAML mapping, namespace exclusion) — separate change
- MSBuild task integration — separate change
- Extension method output style — MVP scope
- Refit output style — v1 scope
- `ApiResponse<T>` return type variants — post-MMVP
- XML documentation comments on generated types — v1 scope (feature 3.11)
- Binary/stream responses, multipart uploads, form-encoded bodies
- Security scheme generation (auth handled via HttpClient defaults or delegating handlers)

## Decisions

### D1: Separate `OperationTransformer` class

**Decision**: Create a new `OperationTransformer` in `ApiStitch.Parsing` rather than extending `SchemaTransformer`.

**Rationale**: `SchemaTransformer` is 450+ lines with complex internal state (`_schemaMap`, `_componentSchemaNames`, circular reference tracking). Operation parsing is a distinct concern: it reads `document.Paths`, classifies parameters, and resolves response schemas against already-transformed schemas. Separation keeps both classes focused and testable.

**Approach**: `SchemaTransformer.Transform()` currently returns `(ApiSpecification, IReadOnlyList<Diagnostic>)`. Extend the return to also expose the schema map as `IReadOnlyDictionary<OpenApiSchema, ApiSchema>` so `OperationTransformer` can resolve `$ref` parameter/response schemas to existing `ApiSchema` instances. The pipeline becomes:

```
Load → TransformSchemas → DetectInheritance → MapCSharpTypes → TransformOperations → Emit
```

`OperationTransformer.Transform` takes the `OpenApiDocument`, the schema map, and the `ApiStitchConfig` (for client name derivation). It returns `(IReadOnlyList<ApiOperation>, IReadOnlyList<Diagnostic>)`. The pipeline merges operations into the `ApiSpecification` before emission.

**Alternative considered**: Extending `SchemaTransformer` to also parse paths. Rejected because it violates single responsibility and the schema map's internal state would be used across two unrelated concerns.

### D2: Separate `ScribanClientEmitter` class

**Decision**: Create a new `ScribanClientEmitter` implementing a new `IClientEmitter` interface, rather than extending `ScribanModelEmitter`.

**Rationale**: Model emission and client emission have different inputs (schemas vs operations), different templates, and different output file sets. The pipeline calls both independently:

```csharp
var (modelFiles, modelDiags) = _modelEmitter.Emit(specification, config);
var (clientFiles, clientDiags) = _clientEmitter.Emit(specification, config);
```

**Interface**:

```csharp
public interface IClientEmitter
{
    (IReadOnlyList<GeneratedFile> Files, IReadOnlyList<Diagnostic> Diagnostics) Emit(
        ApiSpecification spec, ApiStitchConfig config);
}
```

Same signature as `IModelEmitter` — deliberately. Both consume the full `ApiSpecification` and produce files + diagnostics. The pipeline merges their outputs.

**Alternative considered**: A single `IEmitter` with model and client emission in one class. Rejected because it makes it impossible to skip client emission when outputStyle is not set, and makes testing harder.

### D3: Semantic model shape for operations

**Decision**: Flesh out the stubs as follows:

```csharp
public class ApiOperation
{
    public required string OperationId { get; init; }
    public required string Path { get; init; }
    public required ApiHttpMethod HttpMethod { get; init; }
    public required string Tag { get; init; }
    public required string CSharpMethodName { get; init; }
    public IReadOnlyList<ApiParameter> Parameters { get; init; } = [];
    public ApiRequestBody? RequestBody { get; init; }
    public ApiResponse? SuccessResponse { get; init; }
    public bool IsDeprecated { get; init; }
    public string? Description { get; init; }
    public List<Diagnostic> Diagnostics { get; } = [];
}

public enum ApiHttpMethod { Get, Post, Put, Delete, Patch, Head, Options }

public class ApiParameter
{
    public required string Name { get; init; }
    public required string CSharpName { get; init; }
    public required ParameterLocation Location { get; init; }
    public required ApiSchema Schema { get; init; }
    public bool IsRequired { get; init; }
    public string? Description { get; init; }
}

public enum ParameterLocation { Path, Query, Header }

public class ApiRequestBody
{
    public required ApiSchema Schema { get; init; }
    public bool IsRequired { get; init; }
    public required string ContentType { get; init; }
}

public class ApiResponse
{
    public ApiSchema? Schema { get; init; }
    public required int StatusCode { get; init; }
    public required string ContentType { get; init; }
    public bool HasBody => Schema is not null;
}
```

**Key decisions within this model**:
- `ApiHttpMethod` is a custom enum, not `System.Net.Http.HttpMethod`. This keeps the semantic model free of HTTP infrastructure dependencies — the mapping to `System.Net.Http.HttpMethod` happens in the emitter templates.
- `Tag` is a single string. Multi-tag operations produce one `ApiOperation` per tag (duplicated). This simplifies grouping without a tag collection.
- `CSharpMethodName` is computed during transformation (operationId → PascalCase + Async, or derived from method + path).
- `ApiRequestBody` is separate from `ApiParameter` because it has different semantics (content type, schema, not a named parameter).
- `ApiResponse` captures only the success response. Error responses are handled uniformly by `ApiException` — no per-operation error typing in MMVP.

### D4: Operation grouping and client naming

**Decision**:
- One interface + implementation per unique tag value.
- Untagged operations go into a default client named `I{ApiName}Client` / `{ApiName}Client`.
- Multi-tag operations are duplicated onto each tag interface.
- The `{ApiName}` prefix is derived from the spec's `info.title`, PascalCased, with "Api" suffix if not already present. Configurable via `clientName` in YAML.

**Naming scheme**:
| Artifact | Name pattern | Example |
|----------|-------------|---------|
| Tag interface | `I{ApiName}{Tag}Client` | `IPetStoreApiPetsClient` |
| Tag implementation | `{ApiName}{Tag}Client` | `PetStoreApiPetsClient` |
| Default interface (no tags) | `I{ApiName}Client` | `IPetStoreApiClient` |
| Default implementation | `{ApiName}Client` | `PetStoreApiClient` |
| Options | `{ApiName}ClientOptions` | `PetStoreApiClientOptions` |
| JSON options | `{ApiName}JsonOptions` | `PetStoreApiJsonOptions` |
| DI extensions | `{ApiName}ServiceCollectionExtensions` | `PetStoreApiServiceCollectionExtensions` |
| Exception | `ApiException` | `ApiException` |

`ApiException` is not prefixed — it is a shared type across all generated APIs within a namespace. If two APIs share a namespace, only one `ApiException.cs` is emitted (deduplicated by the emitter).

### D5: DI registration — shared named HttpClient

**Decision**: All tag clients for one API share a single named `HttpClient`. The `Add{ApiName}` extension method registers:
1. The named `HttpClient` with base address / headers / timeout from options
2. Each tag client as transient, resolved via `IHttpClientFactory.CreateClient(name)`
3. `{ApiName}JsonOptions` as singleton

Returns `IHttpClientBuilder` for the named client, enabling Polly/handler chaining.

```csharp
public static IHttpClientBuilder Add{ApiName}(
    this IServiceCollection services,
    Action<{ApiName}ClientOptions> configure)
{
    services.Configure<{ApiName}ClientOptions>(configure);
    services.TryAddSingleton<{ApiName}JsonOptions>();

    var builder = services.AddHttpClient("{ApiName}", (sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<{ApiName}ClientOptions>>().Value;
        if (options.BaseAddress is not null)
            client.BaseAddress = options.BaseAddress;
        if (options.Timeout is not null)
            client.Timeout = options.Timeout.Value;
        foreach (var header in options.DefaultHeaders)
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
    });

    services.TryAddTransient<I{ApiName}{Tag}Client>(sp =>
        new {ApiName}{Tag}Client(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<{ApiName}JsonOptions>()));

    // ... repeat for each tag client

    return builder;
}
```

**Why transient via `IHttpClientFactory`**: Typed clients must not hold long-lived `HttpClient` instances. `IHttpClientFactory` manages handler rotation and DNS refresh. Transient registration ensures each injection gets a fresh `HttpClient` from the factory.

**Why shared named client**: Most teams apply one resilience policy per API, not per endpoint group. A shared named client means one `IHttpClientBuilder` chain configures everything. Per-tag named clients is a post-MMVP refinement.

### D6: Client implementation — constructor and HTTP mechanics

**Decision**: Each implementation class stores `IHttpClientFactory` (not `HttpClient`) and creates a client per-call. This is the canonical `IHttpClientFactory` usage — avoids holding a stale `HttpClient` that bypasses handler rotation:

```csharp
internal sealed class PetStoreApiPetsClient : IPetStoreApiPetsClient
{
    private const string HttpClientName = "PetStoreApi";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions;

    public PetStoreApiPetsClient(IHttpClientFactory httpClientFactory, PetStoreApiJsonOptions jsonOptions)
    {
        _httpClientFactory = httpClientFactory;
        _jsonOptions = jsonOptions.Options;
    }
}
```

**HTTP method pattern** (all methods follow this structure):

```csharp
public async Task<Pet> GetPetByIdAsync(long petId, CancellationToken cancellationToken = default)
{
    using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
    using var request = new HttpRequestMessage(HttpMethod.Get, $"pets/{Uri.EscapeDataString(petId.ToString())}");
    using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    return (await response.Content.ReadFromJsonAsync<Pet>(_jsonOptions, cancellationToken).ConfigureAwait(false))!;
}
```

**Relative URIs, not absolute paths**: Generated paths are relative (no leading `/`). `HttpClient` resolves relative URIs against `BaseAddress`. An absolute path like `/pets/123` would discard the base address path component (e.g., `/v1/` in `https://api.example.com/v1/`). The `ClientOptions` enforces trailing `/` on `BaseAddress`:

```csharp
public sealed class PetStoreApiClientOptions
{
    private Uri? _baseAddress;

    public Uri? BaseAddress
    {
        get => _baseAddress;
        set => _baseAddress = value is not null && !value.AbsoluteUri.EndsWith('/')
            ? new Uri(value.AbsoluteUri + "/")
            : value;
    }

    public TimeSpan? Timeout { get; set; }
    public Dictionary<string, string> DefaultHeaders { get; } = [];
}
```

**Why `HttpRequestMessage` + `SendAsync`**: More flexible than `GetFromJsonAsync` etc. Works uniformly across all HTTP methods. Allows setting headers per-request in the future. The `using` pattern ensures the request/response are disposed.

**Query string building**: Generate a private static helper method in each client class:

```csharp
private static string BuildQueryString(List<KeyValuePair<string, string?>> parameters)
{
    if (parameters.Count == 0)
        return string.Empty;

    var sb = new StringBuilder("?");
    for (var i = 0; i < parameters.Count; i++)
    {
        if (parameters[i].Value is null) continue;
        if (sb.Length > 1) sb.Append('&');
        sb.Append(Uri.EscapeDataString(parameters[i].Key));
        sb.Append('=');
        sb.Append(Uri.EscapeDataString(parameters[i].Value));
    }
    return sb.ToString();
}
```

For array parameters (`explode: true`), add one entry per value with the same key.

### D7: Error handling — `ApiException`

**Decision**:

```csharp
public sealed class ApiException : HttpRequestException
{
    public ApiException(HttpStatusCode statusCode, string? responseBody, HttpResponseHeaders? responseHeaders)
        : base($"HTTP {(int)statusCode} ({statusCode})", inner: null, statusCode)
    {
        ResponseBody = responseBody;
        ResponseHeaders = responseHeaders;
    }

    public string? ResponseBody { get; }
    public HttpResponseHeaders? ResponseHeaders { get; }
}
```

**Response body truncation**: Read up to 8KB of the response body for the exception. Large error pages (HTML 500s) should not cause OOM.

**Private `EnsureSuccessAsync` method** in each client class (not a shared base class — no inheritance needed):

```csharp
private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
{
    if (response.IsSuccessStatusCode)
        return;

    string? body = null;
    if (response.Content.Headers.ContentLength is not 0)
    {
        using var reader = new StreamReader(
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        var buffer = new char[8192];
        var read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        body = new string(buffer, 0, read);
    }

    throw new ApiException(response.StatusCode, body, response.Headers);
}
```

### D8: JSON serialization isolation

**Decision**: Per-API `{ApiName}JsonOptions` wrapper:

```csharp
internal sealed class PetStoreApiJsonOptions
{
    public JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = PetStoreApiJsonContext.Default,
    };
}
```

Registered as singleton. `JsonSerializerOptions` becomes immutable after first use, so singleton is safe.

**Why not register `JsonSerializerOptions` directly**: Multiple generated APIs in the same DI container would collide. The wrapper type is unique per API.

**Why `JsonSerializerDefaults.Web`**: Uses camelCase naming by default, which matches most REST APIs. The `JsonSerializerContext` can override specific property names via `[JsonPropertyName]`.

### D9: Config model expansion

**Decision**: Add two fields to `ApiStitchConfig`:

```csharp
public class ApiStitchConfig
{
    public required string Spec { get; init; }
    public string Namespace { get; init; } = "ApiStitch.Generated";
    public string OutputDir { get; init; } = "./Generated";
    public OutputStyle OutputStyle { get; init; } = OutputStyle.TypedClient;
    public string? ClientName { get; init; }
}

public enum OutputStyle { TypedClient }
```

`ClientName` is optional — when null, derived from the spec's `info.title` (PascalCased, "Api" suffix). The enum has one value for now; `ExtensionMethods` and `Refit` will be added in future changes.

`ConfigDto` and `ConfigLoader` updated to parse `outputStyle` (string → enum) and `clientName` from YAML.

### D10: Pipeline orchestration

**Decision**: Update `GenerationPipeline.Generate()`:

```csharp
public GenerationResult Generate(ApiStitchConfig config)
{
    var allDiagnostics = new List<Diagnostic>();

    var (document, loadDiags) = OpenApiSpecLoader.Load(config.Spec);
    allDiagnostics.AddRange(loadDiags);
    if (document == null) return new GenerationResult([], allDiagnostics);

    var schemaTransformer = new SchemaTransformer();
    var (specification, schemaMap, schemaDiags) = schemaTransformer.Transform(document);
    allDiagnostics.AddRange(schemaDiags);

    InheritanceDetector.Detect(specification);
    CSharpTypeMapper.MapAll(specification);

    var (operations, opDiags) = OperationTransformer.Transform(document, schemaMap, config);
    allDiagnostics.AddRange(opDiags);
    specification = specification with { Operations = operations };

    var allFiles = new List<GeneratedFile>();

    var (modelFiles, modelDiags) = _modelEmitter.Emit(specification, config);
    allFiles.AddRange(modelFiles);
    allDiagnostics.AddRange(modelDiags);

    var (clientFiles, clientDiags) = _clientEmitter.Emit(specification, config);
    allFiles.AddRange(clientFiles);
    allDiagnostics.AddRange(clientDiags);

    return new GenerationResult(allFiles, allDiagnostics);
}
```

**Note**: `ApiSpecification` becomes a `record class` to support the `with` expression. This is the minimal change — it has only three properties and no mutable state beyond init.

**Client emission guard**: The pipeline skips client emission when `specification.Operations` is empty (not based on config). This means existing tests with `paths: {}` and no `OutputStyle` config produce model-only output unchanged. The `IClientEmitter` constructor is injected into `GenerationPipeline` alongside `IModelEmitter`:

```csharp
public GenerationPipeline(IModelEmitter? modelEmitter = null, IClientEmitter? clientEmitter = null)
{
    _modelEmitter = modelEmitter ?? new ScribanModelEmitter();
    _clientEmitter = clientEmitter ?? new ScribanClientEmitter();
}
```

### D11: Method naming derivation

**Decision**: Two paths:

1. **`operationId` present**: Convert to PascalCase + `Async` suffix. `findPets` → `FindPetsAsync`, `getPetById` → `GetPetByIdAsync`.
2. **`operationId` absent**: Derive from HTTP method + path segments. `GET /pets/{petId}` → `GetPetsByPetIdAsync`. Emit diagnostic `AS400` (Warning) recommending the spec author add `operationId`.

PascalCase conversion: split on `-`, `_`, `.`, camelCase boundaries. Capitalize each segment. Remove non-alphanumeric characters.

**Collision handling**: If two operations in the same tag produce identical method names after PascalCase conversion (e.g., `get_pets` and `getPets` both → `GetPetsAsync`), deduplicate by appending the HTTP method or a numeric suffix. Emit diagnostic `AS403` (Warning) for the collision.

### D12: Inline schema handling

**Decision**: For MMVP, operation parameters and response schemas must be either:
- A `$ref` to a component schema (resolved via schema map)
- An inline primitive (string, integer, boolean, etc.)
- An inline array of `$ref` or primitive items

Inline complex object schemas or inline `allOf` compositions in parameters or responses emit diagnostic `AS401` (Warning) and are skipped (method not generated for that operation). Cookie parameters emit diagnostic `AS402` (Warning) and are ignored. Method name collisions within a tag emit diagnostic `AS403` (Warning) and the second method is deduplicated with a numeric suffix.

### D13: Scriban template strategy

**Decision**: New embedded resource templates in `ApiStitch.Emission.Templates`:

| Template | Output | Visibility |
|----------|--------|-----------|
| `ClientInterface.sbn-cs` | `I{ApiName}{Tag}Client.cs` | public |
| `ClientImplementation.sbn-cs` | `{ApiName}{Tag}Client.cs` | internal sealed |
| `DiRegistration.sbn-cs` | `{ApiName}ServiceCollectionExtensions.cs` | public static |
| `ApiException.sbn-cs` | `ApiException.cs` | public sealed |
| `ClientOptions.sbn-cs` | `{ApiName}ClientOptions.cs` | public sealed |
| `JsonOptionsWrapper.sbn-cs` | `{ApiName}JsonOptions.cs` | internal sealed |
| `EnumExtensions.sbn-cs` | `{EnumName}Extensions.cs` | internal static |

The existing `JsonSerializerContext.sbn-cs` template is modified to include `[JsonSerializable]` for collection types used in responses (e.g., `IReadOnlyList<Pet>`). Collection type gathering happens in the pipeline after operation transformation — the pipeline collects all distinct response/request types across operations, deduplicates against schema-emitted types, and passes the full list to the model emitter. Templates remain logic-free.

## Risks / Trade-offs

**[Schema map exposure]** → `SchemaTransformer.Transform()` return type changes to include the schema map. This is a minor API break on an internal type. Mitigated by updating all callers (pipeline + tests) in the same change.

**[`ApiSpecification` mutability]** → The pipeline currently constructs `ApiSpecification` in `SchemaTransformer` with `Operations = []`, then needs to set operations later. Options: make it a record with `with` expression, or make `Operations` settable. → Use `init` setter on `Operations` and construct a new instance with operations populated after `OperationTransformer` runs.

**[Test spec changes]** → Existing test YAML specs have `paths: {}`. Adding paths to these specs may cause existing tests to behave differently if the pipeline now also generates client files. → Mitigated by: client emission is skipped when `specification.Operations` is empty. Existing tests with `paths: {}` produce model-only output as before.

**[Integration test compilation]** → Generated client code references `Microsoft.Extensions.Http`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options`. The Roslyn compilation helper must add these assembly references. → Add the NuGet packages to the integration test project and include their assemblies in the reference list.

**[Response body read in error path]** → Reading the response body in `EnsureSuccessAsync` consumes the stream. If someone catches `ApiException` and tries to re-read the response, it is empty. → Acceptable trade-off: the body is available on `ApiException.ResponseBody`. The response message is disposed anyway (`using`).

**[Enum extensions for query strings]** → Each enum type used as a query parameter gets an extension class. If an enum is only used as a property (never a query param), no extension is generated. → The emitter must track which enums appear in query parameters during template rendering.
