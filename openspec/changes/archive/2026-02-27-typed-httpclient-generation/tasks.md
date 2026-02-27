## 1. Semantic Model Types

- [x] 1.1 Add `ApiHttpMethod` enum to `ApiStitch.Model` (Get, Post, Put, Delete, Patch, Head, Options)
- [x] 1.2 Add `ParameterLocation` enum to `ApiStitch.Model` (Path, Query, Header)
- [x] 1.3 Flesh out `ApiParameter` class with properties: Name, CSharpName, Location, Schema, IsRequired, Description
- [x] 1.4 Add `ApiRequestBody` class to `ApiStitch.Model` with properties: Schema, IsRequired, ContentType
- [x] 1.5 Flesh out `ApiResponse` class with properties: Schema (nullable), StatusCode, ContentType, HasBody (computed)
- [x] 1.6 Flesh out `ApiOperation` class with properties: OperationId, Path, HttpMethod, Tag, CSharpMethodName, Parameters, RequestBody, SuccessResponse, IsDeprecated, Description, Diagnostics (mutable List)
- [x] 1.7 Convert `ApiSpecification` from `class` to `record class`; add `CollectionTypes` property (`IReadOnlyList<ApiSchema>`, default `[]`) for collection types used in client responses/requests; add `ClientName` property (`string?`) for derived client name

## 2. Diagnostic Codes

- [x] 2.1 Define diagnostic codes AS400–AS405 with severity and message templates: AS400 (Warning, missing operationId), AS401 (Warning, unsupported inline schema), AS402 (Warning, cookie parameter skipped), AS403 (Warning, method name collision), AS404 (Warning, unsupported content type), AS405 (Warning, unsupported query parameter style)

## 3. Configuration

- [x] 3.1 Add `OutputStyle` enum to `ApiStitch.Configuration` with single value `TypedClient`
- [x] 3.2 Add `OutputStyle` and `ClientName` properties to `ApiStitchConfig`
- [x] 3.3 Update `ConfigDto` and `ConfigLoader.LoadFromYaml` to parse `outputStyle` (case-insensitive) and `clientName` from YAML, with error diagnostic for unknown output style values
- [x] 3.4 Add unit tests for new config properties: defaults, explicit values, case-insensitive parsing, unknown value error

## 4. SchemaTransformer — Expose Schema Map

- [x] 4.1 Change `SchemaTransformer.Transform()` return type to `(ApiSpecification, IReadOnlyDictionary<OpenApiSchema, ApiSchema>, IReadOnlyList<Diagnostic>)`
- [x] 4.2 Update `GenerationPipeline` to destructure the new return type and rename existing `_emitter` field to `_modelEmitter` for consistency with `_clientEmitter`
- [x] 4.3 Update all existing tests that construct `ApiSpecification` or call `SchemaTransformer.Transform()` to match the new record class and return type

## 5. OperationTransformer — Core

- [x] 5.1 Create `OperationTransformer` class in `ApiStitch.Parsing` with `Transform(OpenApiDocument, IReadOnlyDictionary<OpenApiSchema, ApiSchema>, ApiStitchConfig)` returning `(IReadOnlyList<ApiOperation>, string ClientName, IReadOnlyList<Diagnostic>)`
- [x] 5.2 Implement client name derivation from `config.ClientName` or spec `info.title` (PascalCase, "Api" suffix, fallback to "ApiClient" on empty/null title); return as part of Transform result
- [x] 5.3 Implement path iteration: loop `document.Paths`, merge path-level and operation-level parameters (operation-level overrides by name+in), strip leading `/` from paths
- [x] 5.4 Implement tag handling: single tag, no tags (use derived client name as default), multi-tag (duplicate ApiOperation per tag)
- [x] 5.5 Implement parameter classification: path (always required), query, header; skip cookie params with `AS402` diagnostic
- [x] 5.6 Implement parameter schema resolution: `$ref` → schema map lookup, inline primitive, inline array of `$ref`/primitive items; skip inline complex/allOf with `AS401` diagnostic
- [x] 5.7 Implement parameter CSharpName: camelCase conversion for method parameters
- [x] 5.8 Implement request body parsing: `application/json` only, `$ref` and inline array of `$ref`; skip non-JSON with `AS404`, skip inline complex with `AS401`; exclude operation from result list and emit diagnostic for unsupported body
- [x] 5.9 Implement success response parsing: find lowest 2xx with body, handle 204/no-content, handle array responses, skip inline complex response with `AS401` (exclude operation)
- [x] 5.10 Implement method naming: operationId → PascalCase + `Async` (no double Async), derived name from HTTP method + path when absent (emit `AS400`), collision dedup within tag with `AS403`
- [x] 5.11 Implement array query parameter style handling: accept `explode: true` (default), skip non-explode with `AS405` diagnostic

## 6. OperationTransformer — Tests

- [x] 6.1 Add unit tests for basic operation parsing: GET with operationId and tag, POST with body, DELETE 204
- [x] 6.2 Add unit tests for parameter classification: path/query/header params, cookie skip with AS402
- [x] 6.3 Add unit tests for schema resolution: $ref, inline primitive, inline array, inline complex skip with AS401
- [x] 6.4 Add unit tests for request body: JSON $ref, inline array, non-JSON skip with AS404, inline complex skip with AS401 (verify operation excluded from result)
- [x] 6.5 Add unit tests for response parsing: 200 with body, 204 no-content, array response, multiple 2xx, no 2xx
- [x] 6.6 Add unit tests for method naming: camelCase/snake_case/kebab-case operationId, derived name, existing Async suffix, collision dedup
- [x] 6.7 Add unit tests for tag handling: no tags (default), multi-tag (duplication), deprecated operations
- [x] 6.8 Add unit tests for path-level parameter merging and operation-level override
- [x] 6.9 Add unit tests for client name derivation: from config, from spec title, special characters, empty title fallback

## 7. Pipeline Orchestration

- [x] 7.1 Add `IClientEmitter` interface in `ApiStitch.Emission` with same signature as `IModelEmitter`
- [x] 7.2 Update `GenerationPipeline` constructor to accept optional `IClientEmitter` parameter, defaulting to `ScribanClientEmitter`
- [x] 7.3 Add `OperationTransformer.Transform()` call in pipeline after `CSharpTypeMapper.MapAll()`; populate `specification.ClientName` and merge operations via `with` expression
- [x] 7.4 Add collection type gathering in pipeline: collect distinct response/request array types from operations, populate `specification.CollectionTypes` via `with` expression before passing to model emitter
- [x] 7.5 Add client emission call in pipeline, guarded on `specification.Operations` not being empty; merge file lists and diagnostics
- [x] 7.6 Update existing pipeline tests to verify model-only output is unchanged when operations are empty

## 8. Scriban Templates — Client

- [x] 8.1 Create `ClientInterface.sbn-cs` template: public partial interface, method signatures with correct parameter ordering, return types (Task/Task<T>), CancellationToken, nullable optional params (including nullable value types like `int?`), [Obsolete] for deprecated, [GeneratedCode]
- [x] 8.2 Create `ClientImplementation.sbn-cs` template — structure and simple GET: internal sealed class, IHttpClientFactory + JsonOptions constructor, const HttpClientName, per-call CreateClient with `using`, HttpRequestMessage with relative URI, path param escaping via `Uri.EscapeDataString`, response deserialization with `ReadFromJsonAsync`, ConfigureAwait(false) on all awaits, EnsureSuccessAsync call
- [x] 8.3 Create `ClientImplementation.sbn-cs` template — query string, headers, and body: BuildQueryString helper with StringBuilder, null exclusion, array param explode support, enum `ToQueryString()` calls; header params via `TryAddWithoutValidation`; POST/PUT body via `JsonContent.Create(body, mediaType: null, _jsonOptions)`
- [x] 8.4 Create `ClientImplementation.sbn-cs` template — EnsureSuccessAsync helper: private static method, read up to 8KB on error, throw ApiException with status code and body, handle ContentLength 0 vs null
- [x] 8.5 Create `ApiException.sbn-cs` template: public sealed class extending HttpRequestException, StatusCode passthrough, ResponseBody, ResponseHeaders
- [x] 8.6 Create `ClientOptions.sbn-cs` template: public sealed class, BaseAddress with trailing slash enforcement, Timeout, DefaultHeaders
- [x] 8.7 Create `JsonOptionsWrapper.sbn-cs` template: internal sealed class, get-only Options property with JsonSerializerDefaults.Web and generated context
- [x] 8.8 Create `DiRegistration.sbn-cs` template: public static extension class, Add{ApiName} method, named HttpClient registration, tag client registrations as transient, JsonOptions singleton, Configure options, return IHttpClientBuilder
- [x] 8.9 Create `EnumExtensions.sbn-cs` template: internal static class, ToQueryString extension method with switch expression and wire values, `ArgumentOutOfRangeException(nameof(value))` default arm

## 9. Scriban Templates — Model Updates

- [x] 9.1 Update `JsonSerializerContext.sbn-cs` template to accept and emit `[JsonSerializable]` attributes for collection types from `specification.CollectionTypes` (e.g., `IReadOnlyList<Pet>`)

## 10. ScribanClientEmitter

- [x] 10.1 Create `ScribanClientEmitter` class implementing `IClientEmitter` in `ApiStitch.Emission`
- [x] 10.2 Implement template loading from embedded resources for all client templates
- [x] 10.3 Implement operation grouping by tag, passing grouped operations + client name to interface + implementation templates
- [x] 10.4 Implement enum query parameter tracking: identify which enums need ToQueryString extensions
- [x] 10.5 Implement ApiException deduplication: emit once per namespace
- [x] 10.6 Implement file list composition: all generated files sorted alphabetically, each with [GeneratedCode], file-scoped namespace, #nullable enable, correct using directives

## 11. ScribanClientEmitter — Tests

- [x] 11.1 Add unit tests for emitter: verify file names follow naming conventions (I{ApiName}{Tag}Client.cs, etc.)
- [x] 11.2 Add unit tests for emitter: verify template rendering for a simple single-tag API (interface + implementation + shared files)
- [x] 11.3 Add unit tests for emitter: verify multi-tag API produces correct file set
- [x] 11.4 Add unit tests for emitter: verify empty operations produces no client files
- [x] 11.5 Add unit tests for emitter: verify enum extension classes only generated for query-parameter enums

## 12. Integration Tests

- [x] 12.1 Add `paths` section to `petstore.yaml` test spec with GET/POST/PUT/DELETE operations, path/query params (including enum query param for status), request/response bodies
- [x] 12.2 Add `Microsoft.Extensions.Http`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options` NuGet packages to integration test project
- [x] 12.3 Verify `RoslynCompilationHelper` picks up new assemblies from trusted platform assemblies; add explicit references only if needed
- [x] 12.4 Add integration test: generate full client from petstore spec, compile with Roslyn, verify interface names and method signatures
- [x] 12.5 Add integration test: verify client implementation classes are internal sealed and implement correct interfaces
- [x] 12.6 Add integration test: verify DI registration extension method exists with correct signature
- [x] 12.7 Add integration test: verify ApiException class structure
- [x] 12.8 Add integration test: verify enum extension class generated for enum used as query parameter
- [x] 12.9 Add integration test: verify model-only generation still works (spec with paths: {} produces no client files)
