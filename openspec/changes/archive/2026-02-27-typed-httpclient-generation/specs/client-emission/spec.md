## ADDED Requirements

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

The system SHALL append `.ConfigureAwait(false)` to every `await` expression in all generated client code — including `SendAsync`, `ReadFromJsonAsync`, `EnsureSuccessAsync`, `ReadAsStreamAsync`, and `ReadAsync`.

#### Scenario: ConfigureAwait on all awaits
- **WHEN** any async method is emitted in a client implementation
- **THEN** every `await` expression in the method body has `.ConfigureAwait(false)`

### Requirement: Emit client implementation per tag

The system SHALL emit one `internal sealed class` per tag implementing the corresponding interface. The implementation SHALL store `IHttpClientFactory` and create a client per method call.

#### Scenario: Implementation class structure
- **WHEN** an interface `IPetStoreApiPetsClient` exists
- **THEN** a `PetStoreApiPetsClient` class is emitted that is `internal sealed` and implements `IPetStoreApiPetsClient`
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

#### Scenario: POST request with JSON body
- **WHEN** an operation is `POST pets` with request body of type `Pet`
- **THEN** the generated code creates `HttpRequestMessage` with `JsonContent.Create(body, mediaType: null, _jsonOptions)` as content

#### Scenario: Response deserialization
- **WHEN** a successful response has body type `Pet`
- **THEN** the generated code calls `response.Content.ReadFromJsonAsync<Pet>(_jsonOptions, cancellationToken)`
- **THEN** `ConfigureAwait(false)` is on all awaits

#### Scenario: No-content response (204)
- **WHEN** an operation returns 204
- **THEN** the method body calls `EnsureSuccessAsync` but does not read or deserialize the response body

#### Scenario: Query string building
- **WHEN** an operation has query parameters
- **THEN** the generated code builds a query string using `BuildQueryString` with `List<KeyValuePair<string, string?>>` and `StringBuilder`
- **THEN** null-valued optional parameters are excluded

#### Scenario: Array query parameter with explode
- **WHEN** a query parameter has Kind = Array (explode: true)
- **THEN** each array element is added as a separate key=value pair with the same key

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

The system SHALL emit a `public sealed class ApiException : HttpRequestException` with `ResponseBody` (string?, truncated at 8KB) and `ResponseHeaders` properties. StatusCode SHALL be passed through the base constructor.

#### Scenario: ApiException structure
- **WHEN** ApiException is emitted
- **THEN** it extends `HttpRequestException`
- **THEN** the constructor passes `HttpStatusCode` to the base constructor's `statusCode` parameter
- **THEN** the message format is `"HTTP {(int)statusCode} ({statusCode})"`
- **THEN** `ResponseBody` is `string?` and `ResponseHeaders` is `HttpResponseHeaders?`

#### Scenario: ApiException is not prefixed with API name
- **WHEN** the emitter generates exception code
- **THEN** the class is named `ApiException` (shared across APIs in the same namespace)

#### Scenario: Namespace deduplication
- **WHEN** two APIs share the same namespace
- **THEN** only one `ApiException.cs` file is emitted

### Requirement: Emit EnsureSuccessAsync helper in each client

The system SHALL emit a `private static async Task EnsureSuccessAsync` method in each client implementation that reads the response body (up to 8KB) and throws `ApiException` on non-success status codes.

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

### Requirement: Emit DI registration extension method

The system SHALL emit a `public static class {ApiName}ServiceCollectionExtensions` with an `Add{ApiName}` method that registers the named HttpClient, all tag clients, and JSON options.

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
- **THEN** both `IPetStoreApiPetsClient` → `PetStoreApiPetsClient` and `IPetStoreApiStoreClient` → `PetStoreApiStoreClient` are registered as transient
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

The system SHALL emit each client-side type as a separate `.cs` file with `[GeneratedCode("ApiStitch")]`, file-scoped namespace, `#nullable enable`, and the necessary `using` directives (e.g., `System.Net.Http`, `System.Net.Http.Json`, `System.Net.Http.Headers`, `System.Text`, `System.Text.Json`, `System.CodeDom.Compiler`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Http` as applicable to each file).

#### Scenario: File inventory for single-tag API
- **WHEN** an API named "PetStoreApi" has one tag "Pets"
- **THEN** the emitter produces: `IPetStoreApiPetsClient.cs`, `PetStoreApiPetsClient.cs`, `PetStoreApiServiceCollectionExtensions.cs`, `ApiException.cs`, `PetStoreApiClientOptions.cs`, `PetStoreApiJsonOptions.cs`

#### Scenario: File inventory for multi-tag API
- **WHEN** an API has tags "Pets" and "Store"
- **THEN** the emitter produces interface + implementation files for each tag, plus the shared files (DI, options, exception, JSON options)

#### Scenario: Files are sorted alphabetically
- **WHEN** the emitter returns generated files
- **THEN** the files are ordered alphabetically by file name for deterministic output

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
