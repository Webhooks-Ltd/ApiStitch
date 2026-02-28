## MODIFIED Requirements

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
