# client-emission Specification

## Purpose
TBD - created by archiving change typed-httpclient-generation. Update Purpose after archive.
## Requirements
### Requirement: Emit client interface per tag

The system SHALL emit one `public partial interface` per unique tag value, containing async method signatures for all operations in that tag. The interface name SHALL follow the pattern `I{ApiName}{Tag}Client`.

#### Scenario: Single-tag interface
- **WHEN** a spec has operations tagged "Pets" with methods `ListPetsAsync` and `GetPetByIdAsync`
- **THEN** an `IPetStoreApiPetsClient` interface is emitted with both method signatures
- **THEN** the interface is `public partial`
- **THEN** the file has `[GeneratedCode("ApiStitch")]` on the type

#### Scenario: Untagged operations use default client name
- **WHEN** operations have no tags
- **THEN** they are emitted on `I{ApiName}Client` (e.g., `IPetStoreApiClient`)

#### Scenario: Multi-tag operation appears on each tag interface
- **WHEN** an operation has `tags: [Pets, Admin]`
- **THEN** the method signature appears on both `IPetStoreApiPetsClient` and `IPetStoreApiAdminClient`

#### Scenario: Method signature with return type
- **WHEN** an operation has a success response with body schema `Pet`
- **THEN** the method returns `Task<Pet>`

#### Scenario: Method signature for no-content response
- **WHEN** an operation's success response has no body (204)
- **THEN** the method returns `Task`

#### Scenario: Method signature with no success response
- **WHEN** an operation has no 2xx response defined
- **THEN** the method returns `Task`

#### Scenario: CancellationToken on every method
- **WHEN** any method signature is emitted
- **THEN** the last parameter is `CancellationToken cancellationToken = default`

#### Scenario: Parameter ordering
- **WHEN** a method has path params `petId`, body `Pet`, query param `status`, header param `X-Trace`
- **THEN** the parameter order is: `petId`, `Pet body`, `status`, `xTrace`, `cancellationToken`

#### Scenario: Optional string parameters use nullable defaults
- **WHEN** a query parameter has IsRequired = false and schema type string
- **THEN** the parameter is `string? status = null`

#### Scenario: Optional value type parameters use nullable defaults
- **WHEN** a query parameter has IsRequired = false and schema type int32
- **THEN** the parameter is `int? limit = null`

#### Scenario: Array return type
- **WHEN** the success response schema has Kind = Array with item type Pet
- **THEN** the method returns `Task<IReadOnlyList<Pet>>`

#### Scenario: Deprecated operation gets Obsolete attribute
- **WHEN** an ApiOperation has IsDeprecated = true
- **THEN** the interface method has `[Obsolete]` attribute

#### Scenario: Operation descriptions are not emitted
- **WHEN** an ApiOperation has a Description value
- **THEN** the description is NOT emitted as XML doc comments or inline comments (XML docs are v1 scope)

### Requirement: All generated awaits use ConfigureAwait(false)

The system SHALL append `.ConfigureAwait(false)` to every `await` expression in all generated client code — including `SendAsync`, `ReadFromJsonAsync`, `EnsureSuccessAsync`, `ReadAsStreamAsync`, `ReadAsStringAsync`, `ReadAsync`, and `FileResponse.CreateAsync`.

#### Scenario: ConfigureAwait on all awaits
- **WHEN** any async method is emitted in a client implementation
- **THEN** every `await` expression in the method body has `.ConfigureAwait(false)`

### Requirement: Emit client implementation per tag

The system SHALL emit one `internal sealed partial class` per tag implementing the corresponding interface. The implementation SHALL store `IHttpClientFactory` and create a client per method call. The implementation SHALL handle request body emission for all supported `ContentKind` values (Json, FormUrlEncoded, MultipartFormData, OctetStream, PlainText), response handling for all supported content types (JSON deserialization, plain text reading, stream responses via FileResponse), Accept header generation on every request with a response body, and Content-Type validation before JSON deserialization.

JSON serialization/deserialization path selection SHALL use schema compatibility policy derived from semantic schema metadata (not emitted C# type-name pattern checks):
- compatible schema graphs use generated `_jsonOptions`
- unsupported schema graphs use runtime JSON APIs without `_jsonOptions`

#### Scenario: Implementation class structure
- **WHEN** an interface `IPetStoreApiPetsClient` exists
- **THEN** a `PetStoreApiPetsClient` class is emitted that is `internal sealed partial` and implements `IPetStoreApiPetsClient`
- **THEN** the constructor accepts `IHttpClientFactory` and `{ApiName}JsonOptions`
- **THEN** `IHttpClientFactory` is stored in a field, not `HttpClient`
- **THEN** the file has `[GeneratedCode("ApiStitch")]` on the type

#### Scenario: Per-call HttpClient creation
- **WHEN** any HTTP method is invoked
- **THEN** `_httpClientFactory.CreateClient(HttpClientName)` is called to get a fresh client
- **THEN** the client is disposed after the method completes (via `using`)

#### Scenario: Named HttpClient constant
- **WHEN** the implementation class is emitted
- **THEN** it contains `private const string HttpClientName = "{ApiName}";` matching the DI registration name

#### Scenario: GET request with path parameter
- **WHEN** an operation is `GET pets/{petId}` with path param `petId` (long)
- **THEN** the generated code uses `$"pets/{Uri.EscapeDataString(petId.ToString())}"` for the URI
- **THEN** the URI is relative (no leading `/`)

#### Scenario: POST request with JSON body (compatible schema)
- **WHEN** an operation is `POST pets` with request body ContentKind = Json and a schema graph compatible with generated metadata
- **THEN** the generated code creates `HttpRequestMessage` with `JsonContent.Create(body, mediaType: null, _jsonOptions)` as content

#### Scenario: POST request with JSON body (unsupported schema)
- **WHEN** an operation request body schema graph includes unsupported source-generation external kinds
- **THEN** the generated code creates content with `JsonContent.Create(body)`

#### Scenario: POST request with form-encoded body
- **WHEN** an operation has request body ContentKind = FormUrlEncoded with schema properties `grant_type` (string, required) and `scope` (string, optional)
- **THEN** the generated code builds `List<KeyValuePair<string, string>>` from the schema properties
- **THEN** required properties are always added; optional properties are added with null-check
- **THEN** the request content is `new FormUrlEncodedContent(formFields)`

#### Scenario: POST request with multipart body
- **WHEN** an operation has request body ContentKind = MultipartFormData with properties `file` (Stream), `description` (string), and `metadata` (object with encoding contentType = application/json)
- **THEN** the generated code creates `MultipartFormDataContent`
- **THEN** `file` is added as `new StreamContent(file), "file", fileName`
- **THEN** `description` is added as `new StringContent(description), "description"` with null-check for optional properties
- **THEN** `metadata` uses `JsonContent.Create(metadata, mediaType: null, _jsonOptions)` when metadata schema graph is compatible
- **THEN** `metadata` uses `JsonContent.Create(metadata)` when metadata schema graph is unsupported for generated metadata
- **THEN** `using var content = new MultipartFormDataContent()` disposes the multipart content after send

#### Scenario: POST request with octet-stream body
- **WHEN** an operation has request body ContentKind = OctetStream
- **THEN** the generated code sets `request.Content = new StreamContent(body)`
- **THEN** the content type header is set to `application/octet-stream`

#### Scenario: POST request with plain text body
- **WHEN** an operation has request body ContentKind = PlainText
- **THEN** the generated code sets `request.Content = new StringContent(body, Encoding.UTF8, "text/plain")`

#### Scenario: JSON response deserialization (compatible schema)
- **WHEN** a successful response has ContentKind = Json and a schema graph compatible with generated metadata
- **THEN** the generated code calls `response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken)`
- **THEN** `ConfigureAwait(false)` is on all awaits

#### Scenario: JSON response deserialization (unsupported schema)
- **WHEN** a successful response has ContentKind = Json and a schema graph with unsupported source-generation external kinds
- **THEN** the generated code calls `response.Content.ReadFromJsonAsync<T>(cancellationToken)`

#### Scenario: Content-Type validation before JSON deserialization
- **WHEN** a response is expected to be JSON (ContentKind = Json) and the response status is 2xx
- **THEN** before calling `ReadFromJsonAsync`, the generated code checks `response.Content.Headers.ContentType?.MediaType`
- **THEN** if the media type is not null and not `application/json` and does not end with `+json`, an `ApiException` is thrown with a message including the unexpected media type and truncated response body

#### Scenario: Plain text response reading
- **WHEN** a successful response has ContentKind = PlainText
- **THEN** the generated code calls `response.Content.ReadAsStringAsync(cancellationToken)` and returns the string

#### Scenario: Stream response (file download)
- **WHEN** a successful response has ContentKind = OctetStream (e.g., application/pdf, application/octet-stream)
- **THEN** the method returns `Task<FileResponse>`
- **THEN** the generated code uses `HttpCompletionOption.ResponseHeadersRead` on `SendAsync` to avoid buffering
- **THEN** `HttpRequestMessage` still uses `using` (already sent, safe to dispose)
- **THEN** `HttpResponseMessage` has NO `using` — ownership transfers to `FileResponse`
- **THEN** the method returns `await FileResponse.CreateAsync(response, cancellationToken).ConfigureAwait(false)`

#### Scenario: Accept header on every request with response body
- **WHEN** an operation has a response body
- **THEN** the generated code sets `request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType))` where `mediaType` comes from `ApiResponse.MediaType`

#### Scenario: Accept header omitted for no-content responses
- **WHEN** an operation returns 204 (no content) with no response body
- **THEN** no `Accept` header is set on the request

#### Scenario: No-content response (204)
- **WHEN** an operation returns 204
- **THEN** the method body calls `EnsureSuccessAsync` but does not read or deserialize the response body

#### Scenario: Query string building with inline serialization
- **WHEN** an operation has query parameters
- **THEN** the generated code builds a query string using `BuildQueryString` with `List<KeyValuePair<string, string?>>` and `StringBuilder`
- **THEN** null-valued optional parameters are excluded
- **THEN** serialization logic is inlined per parameter based on its Style + Explode combination

#### Scenario: Array query parameter with form+explode (unchanged)
- **WHEN** a query parameter has Kind = Array and Style = Form, Explode = true
- **THEN** each array element is added as a separate key=value pair with the same key

#### Scenario: Array query parameter with form+comma (explode false)
- **WHEN** a query parameter has Kind = Array and Style = Form, Explode = false
- **THEN** the generated code adds a single key-value pair with comma-joined values via `string.Join(",", items)`

#### Scenario: Object query parameter with deepObject
- **WHEN** a query parameter has Style = DeepObject and schema is an object type
- **THEN** the generated code iterates the object's properties and adds bracket-notation key-value pairs (`name[prop]=value`)

#### Scenario: All query parameters are null
- **WHEN** all query parameter values are null
- **THEN** no query string is appended to the URI (`BuildQueryString` returns `string.Empty`)

#### Scenario: Enum query parameter uses wire value
- **WHEN** a query parameter has an enum schema
- **THEN** the generated code calls the enum's `ToQueryString()` extension method to get the wire value

#### Scenario: Header parameter
- **WHEN** an operation has a header parameter `X-Request-Id`
- **THEN** the generated code calls `request.Headers.TryAddWithoutValidation("X-Request-Id", xRequestId.ToString())` when the value is not null

### Requirement: Emit ApiException class

`ApiException` generation SHALL remain coherent with conditional ProblemDetails support.

#### Scenario: ApiException without ProblemDetails signal
- **WHEN** ProblemDetails support is not signaled by the specification
- **THEN** generated `ApiException` does not require a generated `ProblemDetails` type reference

#### Scenario: ApiException with ProblemDetails signal
- **WHEN** ProblemDetails support is signaled
- **THEN** generated `ApiException` preserves ProblemDetails attachment behavior for qualifying error responses

### Requirement: Emit EnsureSuccessAsync helper in each client

The system SHALL emit a `private async Task EnsureSuccessAsync` instance method in each client implementation that reads the response body (up to 8KB) and throws `ApiException` on non-success status codes. When the response Content-Type is `application/problem+json` or `application/json`, the method SHALL attempt to deserialize the body as `ProblemDetails` and attach it to the thrown `ApiException`.

#### Scenario: Success status code
- **WHEN** the response has a 2xx status code
- **THEN** `EnsureSuccessAsync` returns without throwing

#### Scenario: Error status code with body
- **WHEN** the response has status 404 and a body "Not Found"
- **THEN** `EnsureSuccessAsync` throws `ApiException` with StatusCode = 404 and ResponseBody = "Not Found"

#### Scenario: Error status code with large body
- **WHEN** the response body exceeds 8192 characters
- **THEN** only the first 8192 characters are included in `ApiException.ResponseBody`

#### Scenario: Error status code with Content-Length zero
- **WHEN** the response has `Content-Length: 0` header
- **THEN** `ApiException.ResponseBody` is null

#### Scenario: Error status code with no Content-Length header
- **WHEN** the response has no `Content-Length` header (`ContentLength` is null, e.g., chunked transfer)
- **THEN** `EnsureSuccessAsync` attempts to read the body (up to 8KB) because content may still be present

#### Scenario: Error with application/problem+json content type
- **WHEN** the response has a non-2xx status code and Content-Type is `application/problem+json`
- **THEN** `EnsureSuccessAsync` attempts to deserialize the body as `ProblemDetails` using `_jsonOptions`
- **THEN** the deserialized `ProblemDetails` is attached to the thrown `ApiException.Problem`
- **THEN** `ResponseBody` still contains the raw body string

#### Scenario: Error with application/json content type and ProblemDetails body
- **WHEN** the response has a non-2xx status code and Content-Type is `application/json` and the body contains ProblemDetails-shaped JSON
- **THEN** `EnsureSuccessAsync` attempts to deserialize the body as `ProblemDetails`
- **THEN** the deserialized `ProblemDetails` is attached to the thrown `ApiException.Problem`

#### Scenario: ProblemDetails deserialization failure
- **WHEN** the response has Content-Type `application/problem+json` but the body is not valid ProblemDetails JSON
- **THEN** the `JsonException` is silently caught
- **THEN** `ApiException.Problem` is null
- **THEN** `ApiException.ResponseBody` still contains the raw body string

#### Scenario: Instance method (not static)
- **WHEN** `EnsureSuccessAsync` is emitted
- **THEN** it is `private async Task` (not `private static async Task`) because it accesses `_jsonOptions` for ProblemDetails deserialization

### Requirement: Emit DI registration extension method

The system SHALL emit a `public static class {ApiName}ServiceCollectionExtensions` with an `Add{ApiName}` method that registers the named HttpClient, all tag clients, and JSON options. No serializer registrations are needed — serialization is inlined in the generated code.

#### Scenario: Registration method signature
- **WHEN** the DI registration is emitted
- **THEN** the method is `public static IHttpClientBuilder Add{ApiName}(this IServiceCollection services, Action<{ApiName}ClientOptions> configure)`
- **THEN** the method returns `IHttpClientBuilder` for Polly/handler chaining

#### Scenario: Named HttpClient registration
- **WHEN** `Add{ApiName}` is called
- **THEN** a named HttpClient is registered with name `"{ApiName}"`
- **THEN** the HttpClient is configured with BaseAddress, Timeout, and DefaultHeaders from the options

#### Scenario: Tag client registrations
- **WHEN** the API has tags "Pets" and "Store"
- **THEN** both `IPetStoreApiPetsClient` -> `PetStoreApiPetsClient` and `IPetStoreApiStoreClient` -> `PetStoreApiStoreClient` are registered as transient
- **THEN** each implementation receives `IHttpClientFactory` and `{ApiName}JsonOptions` from DI

#### Scenario: JSON options registration
- **WHEN** `Add{ApiName}` is called
- **THEN** `{ApiName}JsonOptions` is registered as singleton using `TryAddSingleton`

#### Scenario: Options configuration
- **WHEN** `Add{ApiName}` is called
- **THEN** `services.Configure<{ApiName}ClientOptions>(configure)` is called to bind the options

### Requirement: Emit ClientOptions class

The system SHALL emit a `public sealed class {ApiName}ClientOptions` with `BaseAddress` (Uri?), `Timeout` (TimeSpan?), and `DefaultHeaders` (Dictionary<string, string>).

#### Scenario: BaseAddress enforces trailing slash
- **WHEN** BaseAddress is set to `https://api.example.com/v1`
- **THEN** the stored value is `https://api.example.com/v1/` (trailing slash appended)

#### Scenario: BaseAddress already has trailing slash
- **WHEN** BaseAddress is set to `https://api.example.com/v1/`
- **THEN** the stored value is unchanged

#### Scenario: BaseAddress set to null
- **WHEN** BaseAddress is set to null
- **THEN** the stored value is null (no trailing slash logic)

#### Scenario: DefaultHeaders initialized
- **WHEN** ClientOptions is constructed
- **THEN** DefaultHeaders is an empty `Dictionary<string, string>` (not null)

### Requirement: Emit JsonOptions wrapper class

The system SHALL emit an `internal sealed class {ApiName}JsonOptions` wrapping an immutable `JsonSerializerOptions` initialized with `JsonSerializerDefaults.Web` and the generated `JsonSerializerContext`.

#### Scenario: JsonOptions wrapper structure
- **WHEN** the wrapper is emitted
- **THEN** it has a get-only `Options` property of type `JsonSerializerOptions` (no setter, initialized inline)
- **THEN** Options is initialized with `JsonSerializerDefaults.Web` and `TypeInfoResolver` set to the generated context's `Default`

#### Scenario: Singleton safety
- **WHEN** `{ApiName}JsonOptions` is registered as singleton
- **THEN** `JsonSerializerOptions` becomes immutable after first use (thread-safe)

### Requirement: Track which enums are used as query parameters

The system SHALL track which enum schemas appear as query parameters across all operations, so that enum extension methods are only emitted for enums that need them. See `model-emission` spec for the enum extension method emission details.

#### Scenario: Enum used as query parameter is tracked
- **WHEN** enum `PetStatus` appears as a query parameter in any operation
- **THEN** the emitter includes `PetStatus` in the set of enums needing query string extensions

#### Scenario: Enum not used as query parameter is not tracked
- **WHEN** enum `Category` is only used as an object property, never as a query parameter
- **THEN** `Category` is not included in the set of enums needing query string extensions

### Requirement: Update JsonSerializerContext with collection types

The system SHALL add `[JsonSerializable]` attributes to the generated `JsonSerializerContext` for collection types used in request/response bodies (e.g., `IReadOnlyList<Pet>`), not just element types.

#### Scenario: Response returns array of Pet
- **WHEN** an operation returns `IReadOnlyList<Pet>` (array response)
- **THEN** the JsonSerializerContext includes `[JsonSerializable(typeof(IReadOnlyList<Pet>))]` in addition to `[JsonSerializable(typeof(Pet))]`

#### Scenario: Deduplication of collection types
- **WHEN** two operations both return `IReadOnlyList<Pet>`
- **THEN** only one `[JsonSerializable(typeof(IReadOnlyList<Pet>))]` attribute is emitted

#### Scenario: Collection type gathering happens in pipeline
- **WHEN** the pipeline runs operation transformation
- **THEN** the pipeline collects distinct response/request collection types and passes them to the model emitter
- **THEN** Templates remain logic-free (no collection type gathering in templates)

### Requirement: ScribanClientEmitter is separate from ScribanModelEmitter

The system SHALL implement `ScribanClientEmitter` as a separate class implementing `IClientEmitter`, not extending `ScribanModelEmitter`. The pipeline calls both emitters independently.

#### Scenario: Independent emission
- **WHEN** the pipeline calls model and client emission
- **THEN** `_modelEmitter.Emit(spec, config)` and `_clientEmitter.Emit(spec, config)` are called separately
- **THEN** their file lists and diagnostics are merged by the pipeline

#### Scenario: Client emission skipped when no operations
- **WHEN** `specification.Operations` is empty
- **THEN** the pipeline skips client emission entirely
- **THEN** existing model-only tests continue to pass unchanged

#### Scenario: Emitter uses embedded Scriban templates
- **WHEN** ScribanClientEmitter emits files
- **THEN** templates are loaded as embedded resources from `ApiStitch.Emission.Templates`

### Requirement: Emit one file per generated type

The emitter SHALL produce one C# file per generated type.

Shared infrastructure files SHALL include:
- `ApiException.cs`
- `{ClientName}ClientOptions.cs`
- `{ClientName}JsonOptions.cs`
- `{ClientName}ServiceCollectionExtensions.cs`

For each tag group, emitter SHALL produce:
- interface file (`I{ClientName}{Tag}Client.cs` or `I{ClientName}Client.cs` for default tag)
- implementation file (`{ClientName}{Tag}Client.cs` or `{ClientName}Client.cs` for default tag)

When output style is `TypedClientStructured`, relative paths SHALL be foldered as follows:
- `Contracts/` for interfaces
- `Clients/` for client implementations and `FileResponse.cs`
- `Infrastructure/` for `ApiException.cs` and `ProblemDetails.cs`
- `Configuration/` for options/json/service-collection/extensions support files

When output style is `TypedClientFlat`, the same files SHALL be emitted at output root.

#### Scenario: single-tag API with structured layout
- **WHEN** spec has one tag group and output style is `TypedClientStructured`
- **THEN** generated files include `Contracts/ITestApiPetsClient.cs`, `Clients/TestApiPetsClient.cs`, and shared files under `Configuration/` and `Infrastructure/`

#### Scenario: multi-tag API with structured layout
- **WHEN** spec has `Pets` and `Store` tag groups and output style is `TypedClientStructured`
- **THEN** generated files include `Contracts/ITestApiPetsClient.cs`, `Clients/TestApiPetsClient.cs`, `Contracts/ITestApiStoreClient.cs`, `Clients/TestApiStoreClient.cs`
- **THEN** shared files are emitted once under their mapped folders

#### Scenario: single-tag API with flat layout
- **WHEN** spec has one tag group and output style is `TypedClientFlat`
- **THEN** generated files include `ITestApiPetsClient.cs`, `TestApiPetsClient.cs`, and shared files at output root

#### Scenario: file inventory deterministic ordering
- **WHEN** generation runs repeatedly with same input
- **THEN** file relative paths and order are stable and deterministic within selected layout style

### Requirement: Produce deterministic, diff-friendly client output

The system SHALL produce identical client output for identical input across runs. No timestamps, random values, or version numbers SHALL appear in the output.

#### Scenario: Deterministic output across runs
- **WHEN** the same spec and config are processed twice
- **THEN** all client files are byte-for-byte identical

#### Scenario: Methods ordered alphabetically by operation name
- **WHEN** an interface has multiple methods
- **THEN** methods are ordered alphabetically by operationId (or derived name) for stable output

#### Scenario: GeneratedCode attribute has no version
- **WHEN** any client type is emitted
- **THEN** `[GeneratedCode("ApiStitch")]` has no version parameter

### Requirement: Emit FileResponse class

The system SHALL conditionally emit a `public sealed class FileResponse : IAsyncDisposable, IDisposable` when at least one operation returns a stream response (ContentKind = OctetStream). The class SHALL use a private constructor with an async factory method `CreateAsync` to avoid synchronous I/O. It SHALL wrap an `HttpResponseMessage` and expose `Content` (Stream), `FileName` (string?), `ContentType` (string?), and `ContentLength` (long?) properties.

#### Scenario: FileResponse structure
- **WHEN** `FileResponse` is emitted
- **THEN** it is `public sealed class FileResponse : IAsyncDisposable, IDisposable`
- **THEN** it has `[GeneratedCode("ApiStitch", null)]` attribute
- **THEN** its constructor is `private` and accepts `HttpResponseMessage response` and `Stream content`
- **THEN** it has a `static async Task<FileResponse> CreateAsync(HttpResponseMessage response, CancellationToken cancellationToken)` factory method marked `internal`
- **THEN** the factory method calls `await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false)` to get the stream
- **THEN** `FileName` is parsed from `response.Content.Headers.ContentDisposition?.FileName?.Trim('"')`
- **THEN** `ContentType` is read from `response.Content.Headers.ContentType?.MediaType`
- **THEN** `ContentLength` is read from `response.Content.Headers.ContentLength`

#### Scenario: FileResponse async disposal
- **WHEN** `FileResponse.DisposeAsync()` is called
- **THEN** the Content stream is disposed via `await Content.DisposeAsync().ConfigureAwait(false)`
- **THEN** the underlying `HttpResponseMessage` is disposed

#### Scenario: FileResponse synchronous disposal
- **WHEN** `FileResponse.Dispose()` is called
- **THEN** the Content stream is disposed via `Content.Dispose()`
- **THEN** the underlying `HttpResponseMessage` is disposed

#### Scenario: FileResponse emitted conditionally
- **WHEN** no operation returns a stream response
- **THEN** no `FileResponse.cs` file is emitted

#### Scenario: FileResponse emitted once per namespace
- **WHEN** multiple operations return stream responses
- **THEN** only one `FileResponse.cs` file is emitted

### Requirement: Emit ProblemDetails record type

The system SHALL conditionally emit a `public sealed record ProblemDetails` only when the OpenAPI contract signals ProblemDetails support. Contract signals include non-2xx `application/problem+json` responses or explicit ProblemDetails schema usage.

#### Scenario: ProblemDetails omitted when no spec signal
- **WHEN** a specification has operations but no non-2xx `application/problem+json` responses and no explicit ProblemDetails schema usage
- **THEN** no `ProblemDetails.cs` file is emitted

#### Scenario: ProblemDetails emitted when problem+json exists
- **WHEN** any non-2xx response advertises `application/problem+json`
- **THEN** `ProblemDetails.cs` is emitted unless an external ProblemDetails type is selected

### Requirement: Generate client methods for supported inline response schemas

The system SHALL generate client methods for operations whose success responses use supported inline schemas (object/primitive/supported array forms).

#### Scenario: Supported inline response object produces typed method
- **WHEN** an operation has a supported inline object success response schema
- **THEN** the generated client method is emitted with `Task<GeneratedInlineType>` return type
- **THEN** the operation is not dropped from generated interfaces/implementations

#### Scenario: Supported inline primitive response produces typed method
- **WHEN** an operation has a supported inline primitive success response schema (for example `type: string`)
- **THEN** the generated client method is emitted with the mapped primitive return type (for example `Task<string>`)
- **THEN** the operation is not dropped from generated interfaces/implementations

#### Scenario: JSON string response body is quoted
- **WHEN** an operation success response type is `string` and response body is a valid quoted JSON string
- **THEN** generated client returns the unquoted/unescaped string value

#### Scenario: JSON string response body is unquoted raw text
- **WHEN** an operation success response type is `string` and server returns unquoted raw text body despite JSON content-type
- **THEN** generated client returns raw text instead of throwing deserialization failure

#### Scenario: Unsupported inline response composition remains skipped
- **WHEN** an operation success response uses unsupported inline composition
- **THEN** the method is skipped
- **THEN** warning diagnostic `AS401` remains emitted for that operation

