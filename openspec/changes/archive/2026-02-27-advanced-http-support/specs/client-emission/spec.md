## MODIFIED Requirements

### Requirement: Emit client implementation per tag

The system SHALL emit one `internal sealed partial class` per tag implementing the corresponding interface. The implementation SHALL store `IHttpClientFactory` and create a client per method call. The implementation SHALL handle request body emission for all supported `ContentKind` values (Json, FormUrlEncoded, MultipartFormData, OctetStream, PlainText), response handling for all supported content types (JSON deserialization, plain text reading, stream responses via FileResponse), Accept header generation on every request with a response body, and Content-Type validation before JSON deserialization.

#### Scenario: Implementation class structure
- **WHEN** an interface `IPetStoreApiPetsClient` exists
- **THEN** a `PetStoreApiPetsClient` class is emitted that is `internal sealed partial` and implements `IPetStoreApiPetsClient`
- **THEN** the constructor accepts `IHttpClientFactory` and `{ApiName}JsonOptions`
- **THEN** `IHttpClientFactory` is stored in a field, not `HttpClient`
- **THEN** the file has `[GeneratedCode("ApiStitch", null)]` on the type

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
- **WHEN** an operation is `POST pets` with request body ContentKind = Json and body type `Pet`
- **THEN** the generated code creates `HttpRequestMessage` with `JsonContent.Create(body, mediaType: null, _jsonOptions)` as content

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
- **THEN** `metadata` is added as `JsonContent.Create(metadata, mediaType: null, _jsonOptions), "metadata"` because the encoding specifies application/json
- **THEN** `using var content = new MultipartFormDataContent()` disposes the multipart content after send

#### Scenario: POST request with octet-stream body
- **WHEN** an operation has request body ContentKind = OctetStream
- **THEN** the generated code sets `request.Content = new StreamContent(body)`
- **THEN** the content type header is set to `application/octet-stream`

#### Scenario: POST request with plain text body
- **WHEN** an operation has request body ContentKind = PlainText
- **THEN** the generated code sets `request.Content = new StringContent(body, Encoding.UTF8, "text/plain")`

#### Scenario: JSON response deserialization
- **WHEN** a successful response has body type `Pet` and ContentKind = Json
- **THEN** the generated code calls `response.Content.ReadFromJsonAsync<Pet>(_jsonOptions, cancellationToken)`
- **THEN** `ConfigureAwait(false)` is on all awaits

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

### Requirement: All generated awaits use ConfigureAwait(false)

The system SHALL append `.ConfigureAwait(false)` to every `await` expression in all generated client code — including `SendAsync`, `ReadFromJsonAsync`, `EnsureSuccessAsync`, `ReadAsStreamAsync`, `ReadAsStringAsync`, `ReadAsync`, and `FileResponse.CreateAsync`.

#### Scenario: ConfigureAwait on all awaits
- **WHEN** any async method is emitted in a client implementation
- **THEN** every `await` expression in the method body has `.ConfigureAwait(false)`

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

### Requirement: Emit ApiException class

The system SHALL emit a `public sealed class ApiException : HttpRequestException` with `ResponseBody` (string?, truncated at 8KB), `ResponseHeaders`, and `Problem` (ProblemDetails?) properties. StatusCode SHALL be passed through the base constructor.

#### Scenario: ApiException structure
- **WHEN** ApiException is emitted
- **THEN** it extends `HttpRequestException`
- **THEN** the constructor passes `HttpStatusCode` to the base constructor's `statusCode` parameter
- **THEN** the constructor accepts `HttpStatusCode statusCode`, `string? responseBody`, `HttpResponseHeaders? responseHeaders`, and `ProblemDetails? problem = null`
- **THEN** the message format is `"HTTP {(int)statusCode} ({statusCode})"`
- **THEN** `ResponseBody` is `string?`, `ResponseHeaders` is `HttpResponseHeaders?`, and `Problem` is `ProblemDetails?`

#### Scenario: ApiException is not prefixed with API name
- **WHEN** the emitter generates exception code
- **THEN** the class is named `ApiException` (shared across APIs in the same namespace)

#### Scenario: Namespace deduplication
- **WHEN** two APIs share the same namespace
- **THEN** only one `ApiException.cs` file is emitted

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

## ADDED Requirements

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

The system SHALL conditionally emit a `public sealed record ProblemDetails` when client emission is active (i.e., at least one operation exists). The record SHALL use non-positional syntax with `init` properties and `[JsonPropertyName]` attributes for robust STJ source generation.

#### Scenario: ProblemDetails structure
- **WHEN** `ProblemDetails` is emitted
- **THEN** it is a `public sealed record ProblemDetails` with five `init` properties: `Type` (string?), `Title` (string?), `Status` (int?), `Detail` (string?), `Instance` (string?)
- **THEN** each property has `[JsonPropertyName("type")]` (etc.) for explicit wire-name mapping
- **THEN** it has `[GeneratedCode("ApiStitch", null)]` attribute

#### Scenario: ProblemDetails is generated (not from ASP.NET Core)
- **WHEN** the emitter generates ProblemDetails
- **THEN** the type is in the generated output namespace, NOT `Microsoft.AspNetCore.Mvc.ProblemDetails`
- **THEN** no ASP.NET Core dependency is introduced

#### Scenario: ProblemDetails emitted conditionally
- **WHEN** no operations exist (model-only generation)
- **THEN** no `ProblemDetails.cs` file is emitted

