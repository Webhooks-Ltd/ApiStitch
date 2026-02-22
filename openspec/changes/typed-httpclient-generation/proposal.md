## Why

The generation pipeline currently produces model types (records, enums, JsonSerializerContext) but no HTTP client code. Without typed HttpClient wrappers, the generated output is unusable — developers cannot call any API endpoints. This is the critical gap between "generates models" and "generates a working client." Typed HttpClient wrappers are the MMVP output style: Microsoft-recommended pattern, zero third-party runtime dependencies, fully AOT-compatible.

## What Changes

- **Flesh out semantic model**: `ApiOperation`, `ApiParameter`, `ApiResponse` are currently empty stubs. Add properties for HTTP method, path, parameters (classified as path/query/header/body), request/response schemas, operation grouping by tag.
- **New `OperationTransformer`**: A separate transformer (not extending `SchemaTransformer`) to parse `document.Paths` into `ApiOperation` instances, resolving parameter and response schemas against the already-transformed schema map. Runs after `SchemaTransformer` in the pipeline.
- **New `ScribanClientEmitter`**: A separate emitter (not extending `ScribanModelEmitter`) responsible for generating all client-side files. The pipeline calls model emission and client emission independently, composing their file lists.
- **Generate client interfaces**: One `public interface` per tag (e.g., `IPetStoreApiPetsClient`) with async methods, optional parameters, and `CancellationToken` on every method. Operations with no tags go into a single `I{ApiName}Client`. Operations with multiple tags appear on each tag interface.
- **Generate client implementations**: One `internal sealed class` per tag. Constructor takes `IHttpClientFactory` (not `HttpClient` directly) and creates the named client — transient lifetime avoids DNS rotation issues. Uses `System.Net.Http.Json` with explicit `JsonSerializerOptions` on every call for AOT safety. `ConfigureAwait(false)` on all awaits. Path parameters use `Uri.EscapeDataString()`.
- **Generate `ApiException`**: `public sealed class ApiException : HttpRequestException`. Passes `HttpStatusCode` through base constructor for `catch (HttpRequestException ex) when (ex.StatusCode == ...)` patterns. Exposes `ResponseBody` (string?, truncated at 8KB), `ResponseHeaders` (HttpResponseHeaders).
- **Generate DI registration**: `public static IHttpClientBuilder Add{ApiName}(this IServiceCollection, Action<{ApiName}ClientOptions>)`. Registers a single named `HttpClient`, registers all tag clients as transient via `IHttpClientFactory`, returns `IHttpClientBuilder` for Polly/handler chaining. Registers `{ApiName}JsonOptions` as singleton.
- **Generate options class**: `public sealed class {ApiName}ClientOptions` with `BaseAddress` (Uri?), `DefaultHeaders` (IDictionary<string, string>), `Timeout` (TimeSpan?).
- **Generate JSON options wrapper**: `internal sealed class {ApiName}JsonOptions` wrapping an immutable `JsonSerializerOptions` with the generated `JsonSerializerContext`. Registered as singleton — `JsonSerializerOptions` is thread-safe once frozen.
- **Expand config model**: Add `OutputStyle` (enum: `TypedClient`), `ClientName` (optional, derived from spec title).
- **Enum query string extensions**: Generate `internal static` extension methods for enum-to-query-string conversion using original wire values (switch expression with string constants, AOT-compatible).
- **Update JsonSerializerContext**: Add `[JsonSerializable]` entries for collection types used in responses (e.g., `IReadOnlyList<Pet>`), not just element types.
- **Integration tests**: Generate full clients from Petstore spec, compile with Roslyn, verify interface/class structure. Add `Microsoft.Extensions.Http`/DI assembly references to Roslyn compilation helper.

### Method naming and signatures

- Method names: `operationId` → PascalCase + `Async` suffix. When `operationId` is absent, derive from HTTP method + path (e.g., `GET /pets/{petId}` → `GetPetsByPetIdAsync`) and emit a diagnostic warning.
- Parameter order: path params, body, query params, header params, CancellationToken (always last, `= default`).
- Optional parameters (not overloads). Query/header params nullable with `= null` defaults per OpenAPI `required` flag.
- Return `Task<T>` for responses with body, `Task` for no-content responses (204, 202).

### Unsupported patterns (emit diagnostics, not code)

- Inline complex schemas in operation parameters/responses — MMVP supports primitives and `$ref` only
- `application/xml`, `application/x-www-form-urlencoded`, `multipart/form-data` request/response content types — MMVP supports `application/json` only
- `application/octet-stream` / binary responses — defer to post-MMVP
- Query parameter styles other than `explode: true` (the OpenAPI default for query params)
- Callbacks, links, webhooks
- Security schemes (auth handled via `HttpClient` defaults or delegating handlers)

### Array query parameters

MMVP supports `explode: true` (the default): `?status=available&status=pending` (repeated key). Emit diagnostic for other styles.

## Capabilities

### New Capabilities
- `operation-parsing`: Parse OpenAPI paths/operations into semantic model (ApiOperation, ApiParameter, ApiResponse) with tag-based grouping, parameter classification (path/query/header/body), response schema resolution, and operationId-based method naming.
- `client-emission`: Generate typed HttpClient wrappers (interface + implementation + ApiException + ClientOptions + JsonOptions wrapper + DI registration + enum extensions) from the semantic model using Scriban templates.

### Modified Capabilities
- `schema-model`: ApiSpecification.Operations populated (was always `[]`). ApiOperation, ApiParameter, ApiResponse gain properties. SchemaTransformer exposes schema map for OperationTransformer consumption.
- `configuration`: Add `outputStyle` and `clientName` fields to config model and YAML parsing.
- `model-emission`: JsonSerializerContext gains `[JsonSerializable]` for collection types used in client responses. Enum schemas gain query string extension methods.

## Impact

- **New source files**: `OperationTransformer.cs` (parsing), `ScribanClientEmitter.cs` (emission), ~8 Scriban templates (ClientInterface, ClientImplementation, DiRegistration, ApiException, ClientOptions, JsonOptionsWrapper, EnumExtensions, plus updates to JsonSerializerContext template)
- **Modified source files**: `ApiOperation.cs`, `ApiParameter.cs`, `ApiResponse.cs`, `ApiSpecification.cs` (semantic model); `ApiStitchConfig.cs`, `ConfigLoader.cs` (config); `GenerationPipeline.cs` (orchestration — calls both emitters, runs OperationTransformer in pipeline)
- **New dependencies in generated output**: `Microsoft.Extensions.Http`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options` — delivered via MSBuild `.targets` `PackageReference` injection when the MSBuild task ships (separate change). For now, consumers add these manually; integration tests reference assemblies explicitly.
- **Test specs**: Existing test specs (`petstore.yaml`, `complex-microservice.yaml`) need `paths` sections added for integration tests
- **No breaking changes** to existing model generation behaviour
