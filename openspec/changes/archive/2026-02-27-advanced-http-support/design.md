## Context

The generation pipeline currently handles only `application/json` request and response bodies. `ApiRequestBody.ContentType` and `ApiResponse.ContentType` are plain strings, always set to `"application/json"`. The `OperationTransformer` rejects any non-JSON content type with diagnostic AS404, rejects `explode: false` array parameters with AS405, and never reads the `style` field from OpenAPI parameters. The `ScribanClientEmitter` has a single code path for request bodies (`JsonContent.Create`) and responses (`ReadFromJsonAsync`), a hardcoded `BuildQueryString` helper that assumes form+explode serialization, and an `EnsureSuccessAsync` that reads error bodies as raw strings without structured deserialization.

This blocks generation for APIs that use form-encoded bodies (OAuth token endpoints, payment gateways), file uploads (multipart/form-data), file downloads (octet-stream/PDF responses), non-default parameter styles (Stripe's `deep_object`, comma-separated arrays), or `application/problem+json` error responses. These are common patterns across production APIs.

This change adds comprehensive HTTP content type support, parameter serialization styles, streaming responses, and structured error deserialization to the semantic model and emitter.

## Goals / Non-Goals

**Goals:**
- Support `application/x-www-form-urlencoded`, `multipart/form-data`, `application/octet-stream`, and `text/plain` request bodies
- Support streaming response bodies (file downloads) with proper disposal semantics
- Support `form` + `explode: false` (comma-separated), `deepObject` (bracket notation), and `simple` parameter styles
- Add `Accept` header generation and `Content-Type` response validation
- Add `ProblemDetails` deserialization to `ApiException` for `application/problem+json` errors
- Content negotiation: when an operation offers multiple content types, pick the best one automatically
- Maintain zero third-party runtime dependencies in generated output
- Full AOT/trimming compatibility

**Non-Goals:**
- `application/xml` request/response bodies
- Cookie parameter support
- HEAD/OPTIONS special response handling
- User-swappable Scriban templates (separate feature)
- Streaming request bodies with progress reporting
- WebSocket/SSE support
- `spaceDelimited` and `pipeDelimited` parameter styles (uncommon, can be added later)

## Decisions

### D1: ContentKind enum replaces string ContentType

**Decision**: Replace `string ContentType` on `ApiRequestBody` and `ApiResponse` with a `ContentKind` enum. Retain the original content-type string as a secondary property `MediaType` for Accept header generation.

```csharp
public enum ContentKind
{
    Json,
    FormUrlEncoded,
    MultipartFormData,
    OctetStream,
    PlainText,
}

public class ApiRequestBody
{
    public required ApiSchema Schema { get; init; }
    public bool IsRequired { get; init; }
    public required ContentKind ContentKind { get; init; }
    public required string MediaType { get; init; }
}

public class ApiResponse
{
    public ApiSchema? Schema { get; init; }
    public required int StatusCode { get; init; }
    public ContentKind? ContentKind { get; init; }
    public string? MediaType { get; init; }
    public bool HasBody => Schema is not null;
}
```

**Rationale**: The emitter currently makes decisions based on content type using string comparisons. A `ContentKind` enum enables type-safe `switch` expressions in both the emitter and templates, eliminating scattered string comparisons like `contentType == "application/json"`. The secondary `MediaType` string preserves the original value for cases where the distinction matters — `application/pdf` and `application/octet-stream` are both `ContentKind.OctetStream` but produce different `Accept` headers.

**Alternative considered**: Keep string-based content types and add constants. Rejected because every consumer of the model would need to know the full set of recognized strings, and the emitter template would need string comparison logic rather than enum matching.

### D2: PrimitiveType.Stream for binary bodies

**Decision**: Add `Stream` to the `PrimitiveType` enum. Map OpenAPI `format: binary` to `PrimitiveType.Stream` when it appears inside multipart or octet-stream content types. Keep `PrimitiveType.ByteArray` for `format: binary` inside JSON schemas.

```csharp
public enum PrimitiveType
{
    String,
    Int32,
    Int64,
    Float,
    Double,
    Decimal,
    Bool,
    DateTimeOffset,
    DateOnly,
    TimeOnly,
    TimeSpan,
    Guid,
    Uri,
    ByteArray,
    Stream,
}
```

`CSharpTypeMapper.MapPrimitive` maps `Stream` to `"Stream"`. `CSharpTypeMapper.IsValueType` returns `false` for `Stream`.

**Rationale**: The same OpenAPI `format: binary` has fundamentally different C# representations depending on context. Inside a JSON body, binary data is base64-encoded and maps to `byte[]`. Inside a multipart part or an octet-stream body, it is a raw binary stream and maps to `System.IO.Stream`. The context-dependent mapping happens in `OperationTransformer` during request body and response parsing — the transformer knows the content type and selects the appropriate `PrimitiveType`.

**Alternative considered**: A separate `BinaryKind` enum on `ApiSchema`. Rejected because `PrimitiveType` already governs the C# type mapping, and adding a parallel discriminator would require every consumer to check two fields.

### D3: Inline parameter serialization with partial method extensibility

**Decision**: Inline all parameter serialization logic directly in the Scriban template, branching on `ParameterStyle` + `Explode` at generation time. Provide a `partial` method hook on the generated client class for users who need to override serialization for exotic APIs.

**Serialization per OpenAPI style + explode**:

| Style | Explode | Generated code | Example |
|-------|---------|---------------|---------|
| form | true (default) | Repeated key=value (existing behavior) | `color=blue&color=black` |
| form | false | `string.Join(",", items)` | `color=blue,black` |
| deepObject | true | `foreach` over properties with bracket notation | `filter[status]=active&filter[type]=user` |
| simple | true/false | Inline in path template | `{petId}` → `Uri.EscapeDataString(...)` |

For **form + explode: true** (scalars and arrays), the template retains the existing pattern — `queryParams.Add(new KeyValuePair<string, string?>(name, value.ToString()))` per value:

```csharp
// Scalar: form + explode: true (unchanged from current)
queryParams.Add(new KeyValuePair<string, string?>("status", status.ToString()));

// Array: form + explode: true (unchanged from current)
foreach (var item in colors)
    queryParams.Add(new KeyValuePair<string, string?>("color", item.ToString()));
```

For **form + explode: false** (comma-separated arrays), the template emits `string.Join`:

```csharp
// Array: form + explode: false
if (colors is not null)
    queryParams.Add(new KeyValuePair<string, string?>("color", string.Join(",", colors)));
```

For **deepObject** (object-typed query parameters), the template emits bracket-notation per property:

```csharp
// Object: deepObject + explode: true
if (filter is not null)
{
    if (filter.Status is not null)
        queryParams.Add(new KeyValuePair<string, string?>("filter[status]", filter.Status));
    if (filter.Type is not null)
        queryParams.Add(new KeyValuePair<string, string?>("filter[type]", filter.Type));
}
```

The emitter's `BuildQueryParamModel` method gains a `style` and `explode` field on each query parameter model, and the template branches accordingly.

**Extensibility via partial methods**: Each generated client class is `partial`. Users who need non-standard serialization for a specific API can add a partial class with a method that manipulates the query string. Since all generated types are already `partial`, this is a zero-cost extension point with no runtime overhead.

**`BuildQueryString` helper**: Unchanged — it still joins `List<KeyValuePair<string, string?>>` with `&` and URL-encodes. The per-parameter serialization feeds into the same list.

**Alternative considered**: `IParameterSerializer` interface with DI-resolved implementations. Rejected because: (1) the `object? value` parameter forces boxing on value types, (2) boxing breaks AOT/trimming guarantees since the serializer needs runtime type inspection, (3) the current DI registration uses manual `new Client(...)` construction which bypasses `[FromKeyedServices]` entirely, (4) the emitter already knows all types at generation time making runtime dispatch unnecessary, and (5) the added DI/interface complexity is disproportionate to the problem. Partial methods provide extensibility without any of these costs.

### D4: FileResponse for stream responses

**Decision**: Emit a new `FileResponse` type in the generated code for operations that return binary/stream content types. Use an async factory method to avoid synchronous I/O. Implement both `IAsyncDisposable` and `IDisposable`.

```csharp
[GeneratedCode("ApiStitch", null)]
public sealed class FileResponse : IAsyncDisposable, IDisposable
{
    private readonly HttpResponseMessage _response;

    private FileResponse(HttpResponseMessage response, Stream content)
    {
        _response = response;
        Content = content;
        FileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        ContentType = response.Content.Headers.ContentType?.MediaType;
        ContentLength = response.Content.Headers.ContentLength;
    }

    internal static async Task<FileResponse> CreateAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return new FileResponse(response, stream);
    }

    public Stream Content { get; }
    public string? FileName { get; }
    public string? ContentType { get; }
    public long? ContentLength { get; }

    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync().ConfigureAwait(false);
        _response.Dispose();
    }

    public void Dispose()
    {
        Content.Dispose();
        _response.Dispose();
    }
}
```

**Why async factory**: The constructor used `ReadAsStream()` (synchronous), which can block the thread when combined with `HttpCompletionOption.ResponseHeadersRead` — the response body may not be fully buffered yet. `ReadAsStreamAsync` is the correct async alternative. A private constructor + `static async Task<FileResponse> CreateAsync(...)` factory method enforces this.

**Ownership transfer**: Methods returning `FileResponse` do NOT use `using var response = ...` for the `HttpResponseMessage`. The response ownership transfers to `FileResponse`. However, `HttpRequestMessage` is still disposed with `using` — it has already been sent and is not needed after `SendAsync` returns.

```csharp
public async Task<FileResponse> DownloadReportAsync(..., CancellationToken cancellationToken = default)
{
    using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
    using var request = new HttpRequestMessage(HttpMethod.Get, $"reports/{...}");
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));
    var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    return await FileResponse.CreateAsync(response, cancellationToken).ConfigureAwait(false);
}
```

Key differences from the JSON response path:
- `HttpCompletionOption.ResponseHeadersRead` prevents buffering the entire response
- `request` still uses `using` (already sent, safe to dispose)
- `response` has NO `using` — ownership transfers to `FileResponse`
- The caller is responsible for disposing `FileResponse`

**Both `IAsyncDisposable` and `IDisposable`**: Some callers (finally blocks, legacy sync code) need synchronous disposal. The `sealed` class makes implementing both safe — no inheritance concerns.

**Interface return type**: `Task<FileResponse>` in the interface. The `FileResponse` type is emitted once per generated namespace.

**`FileResponse.sbn-cs` template**: A new embedded resource template, following the same pattern as `ApiException.sbn-cs`.

### D5: Multipart body handling

**Decision**: Each property in the multipart schema becomes a separate method parameter. Binary properties become a `Stream` + `string fileName` parameter pair. Non-binary properties become regular typed parameters.

Given this OpenAPI:
```yaml
requestBody:
  content:
    multipart/form-data:
      schema:
        type: object
        properties:
          file:
            type: string
            format: binary
          description:
            type: string
          metadata:
            $ref: '#/components/schemas/FileMetadata'
      encoding:
        metadata:
          contentType: application/json
```

The generated method signature:
```csharp
Task UploadFileAsync(
    Stream file,
    string fileName,
    string? description = null,
    FileMetadata? metadata = null,
    CancellationToken cancellationToken = default);
```

The generated method body:
```csharp
using var content = new MultipartFormDataContent();
content.Add(new StreamContent(file), "file", fileName);
if (description is not null)
    content.Add(new StringContent(description), "description");
if (metadata is not null)
    content.Add(JsonContent.Create(metadata, mediaType: null, _jsonOptions), "metadata");
request.Content = content;
```

**Encoding object**: When the OpenAPI `encoding` object specifies `contentType: application/json` for a property, that property is serialized as `JsonContent.Create` inside the multipart. Without an `encoding` entry, non-binary properties default to `StringContent` with `.ToString()` serialization.

**Schema flattening**: The multipart schema's properties are flattened into method parameters. The schema itself is not emitted as a C# type — there is no `UploadFileRequest` record. This matches the natural C# calling convention for file uploads.

**Stream disposal convention**: When `MultipartFormDataContent` is disposed (via `using`), it disposes its child `StreamContent`, which in turn calls `Dispose()` on the caller's `Stream`. This is an accepted convention in the .NET HTTP ecosystem — ASP.NET Core's `FileStreamResult` follows the same pattern. The generated XML doc on the method SHALL document this: the caller should not reuse the stream after calling the method. The caller is responsible for creating the stream; the generated method consumes and disposes it.

**fileName parameter naming**: For multiple binary properties, each gets a `{propertyName}FileName` companion parameter (e.g., `Stream thumbnail, string thumbnailFileName, Stream document, string documentFileName`).

**`ApiRequestBody` for multipart**: The `Schema` property on `ApiRequestBody` still holds the schema, but the emitter reads its `Properties` to determine the individual method parameters. A new `MultipartPropertyEncodings` dictionary on `ApiRequestBody` captures the per-property encoding:

```csharp
public class ApiRequestBody
{
    public required ApiSchema Schema { get; init; }
    public bool IsRequired { get; init; }
    public required ContentKind ContentKind { get; init; }
    public required string MediaType { get; init; }
    public IReadOnlyDictionary<string, MultipartEncoding>? PropertyEncodings { get; init; }
}

public class MultipartEncoding
{
    public required string ContentType { get; init; }
}
```

### D6: Content negotiation (request body)

**Decision**: When an operation offers multiple content types for the request body, pick one in preference order:

1. `application/json`
2. `application/x-www-form-urlencoded`
3. `multipart/form-data`
4. `application/octet-stream`
5. `text/plain`

Emit a diagnostic (Info severity) noting which alternatives were skipped:

```
AS409 (Info): Operation 'createToken' offers content types [application/json, application/x-www-form-urlencoded]. Selected application/json.
```

**Rationale**: Generating multiple overloads (one per content type) complicates the interface, confuses consumers, and increases the template complexity substantially. Picking one content type with a deterministic preference order gives a clean API. JSON is preferred because it supports the richest type mapping. Form-encoded is next because it is simple and common (OAuth). Multipart follows because it handles files. Octet-stream and plain text are last-resort.

**Alternative considered**: Generate one overload per content type. Rejected because it doubles/triples the interface surface area for marginal benefit — users can always construct `HttpRequestMessage` manually for the alternative content type.

### D7: Accept header generation

**Decision**: Set the `Accept` header on every request based on the response content type.

```csharp
request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
```

The header value comes from `ApiResponse.MediaType`:

| ContentKind | MediaType example | Accept header |
|-------------|-------------------|---------------|
| Json | `application/json` | `application/json` |
| OctetStream | `application/pdf` | `application/pdf` |
| OctetStream | `application/octet-stream` | `application/octet-stream` |
| PlainText | `text/plain` | `text/plain` |

For operations with no response body (204), no `Accept` header is set.

**Rationale**: Without an explicit `Accept` header, some servers perform content negotiation and may return XML or HTML when the client expects JSON. Setting it explicitly prevents mismatches and makes the client's expectations clear to the server.

### D8: Content-Type response validation

**Decision**: Before calling `ReadFromJsonAsync`, check the response `Content-Type`:

```csharp
var mediaType = response.Content.Headers.ContentType?.MediaType;
if (mediaType is not null
    && !mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
    && !mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase))
{
    using var errorStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    using var errorReader = new StreamReader(errorStream);
    var errorBuffer = new char[8192];
    var errorRead = await errorReader.ReadAsync(errorBuffer, cancellationToken).ConfigureAwait(false);
    var body = new string(errorBuffer, 0, errorRead);
    throw new ApiException(
        response.StatusCode,
        $"Expected application/json response but received {mediaType}. Body: {body}",
        response.Headers);
}
```

This check is emitted only in the JSON response deserialization path (not for stream or plain text responses).

**Rationale**: When a WAF, proxy, or load balancer intercepts a request and returns an HTML error page with a 200 status code, `ReadFromJsonAsync` throws a `JsonException` with an unhelpful message like "'\<' is an invalid start of a value". Checking the Content-Type first produces a descriptive exception that includes the actual response body (truncated), making debugging dramatically easier.

**Placement**: The check is in the generated method body, not in `EnsureSuccessAsync`. `EnsureSuccessAsync` handles non-2xx status codes. Content-Type validation handles 2xx responses where the body is wrong.

### D9: ProblemDetails on ApiException

**Decision**: Add a `ProblemDetails` property to `ApiException` and attempt structured deserialization in `EnsureSuccessAsync` when the response Content-Type is `application/problem+json` or `application/json`.

Generated `ProblemDetails` type (emitted alongside `ApiException`). Uses a non-positional record with `init` properties and `[JsonPropertyName]` attributes for robust STJ source generation (positional records rely on constructor-based deserialization which is less predictable with source generators):

```csharp
[GeneratedCode("ApiStitch", null)]
public sealed record ProblemDetails
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("status")]
    public int? Status { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("instance")]
    public string? Instance { get; init; }
}
```

Updated `ApiException`:

```csharp
[GeneratedCode("ApiStitch", null)]
public sealed class ApiException : HttpRequestException
{
    public ApiException(
        HttpStatusCode statusCode,
        string? responseBody,
        HttpResponseHeaders? responseHeaders,
        ProblemDetails? problem = null)
        : base($"HTTP {(int)statusCode} ({statusCode})", inner: null, statusCode)
    {
        ResponseBody = responseBody;
        ResponseHeaders = responseHeaders;
        Problem = problem;
    }

    public string? ResponseBody { get; }
    public HttpResponseHeaders? ResponseHeaders { get; }
    public ProblemDetails? Problem { get; }
}
```

Updated `EnsureSuccessAsync`:

```csharp
private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
{
    if (response.IsSuccessStatusCode)
        return;

    string? body = null;
    ProblemDetails? problem = null;

    if (response.Content.Headers.ContentLength is not 0)
    {
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        var buffer = new char[8192];
        var read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        body = new string(buffer, 0, read);

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is "application/problem+json" or "application/json")
        {
            try
            {
                problem = JsonSerializer.Deserialize<ProblemDetails>(body, _jsonOptions);
            }
            catch (JsonException)
            {
            }
        }
    }

    throw new ApiException(response.StatusCode, body, response.Headers, problem);
}
```

**Key design choices**:
- The `ProblemDetails` type is a generated record in the output namespace, not `Microsoft.AspNetCore.Mvc.ProblemDetails`. This avoids an ASP.NET Core dependency in client code.
- The record has only the five core RFC 9457 fields. Extension fields (like `errors`) are not included — users who need them can deserialize from `ResponseBody`.
- `[JsonSerializable(typeof(ProblemDetails))]` is added to the generated `JsonSerializerContext` to maintain AOT compatibility.
- `EnsureSuccessAsync` changes from `static` to instance method because it now needs `_jsonOptions`.
- Deserialization failure is silently caught — the raw `ResponseBody` is always available as a fallback.
- Both `application/problem+json` (RFC 9457) and `application/json` (many APIs return problem JSON with this content type) are attempted.

### D10: ParameterStyle and Explode on ApiParameter

**Decision**: Add `Style` and `Explode` properties to `ApiParameter`:

```csharp
public enum ParameterStyle
{
    Form,
    Simple,
    DeepObject,
    PipeDelimited,
    SpaceDelimited,
}

public class ApiParameter
{
    public required string Name { get; init; }
    public required string CSharpName { get; init; }
    public required ParameterLocation Location { get; init; }
    public required ApiSchema Schema { get; init; }
    public bool IsRequired { get; init; }
    public string? Description { get; init; }
    public required ParameterStyle Style { get; init; }
    public required bool Explode { get; init; }
}
```

**Default values per OpenAPI spec**:
- Query parameters: `style: form`, `explode: true`
- Path parameters: `style: simple`, `explode: false`
- Header parameters: `style: simple`, `explode: false`

Both `Style` and `Explode` are `required` properties — the `OperationTransformer` must always set them explicitly based on the OpenAPI parameter's `style`/`explode` fields, applying the OpenAPI defaults above when not specified. Making them `required` avoids misleading default values (e.g., `Form` as a default would be wrong for path parameters). The emitter uses these properties to select the inline serialization strategy per parameter.

**DeepObject for complex query parameters**: The current `OperationTransformer` rejects inline complex objects in query parameters (AS401). With `style: deepObject`, complex objects are valid — properties are serialized as `filter[status]=active&filter[type]=user`. When a query parameter has `style: deepObject` and its schema is an object type with properties, the transformer resolves it against the schema map (for `$ref`) or processes inline properties. The emitter generates bracket-notation serialization inline in the template.

**Simple style for path and header**: Path and header parameters use `simple` style, which is the comma-separated value without a parameter name prefix. For single-value parameters (the common case), this is just the value itself. The existing inline `Uri.EscapeDataString(param.ToString())` in the path template handles this. For arrays in path parameters with `simple` style, the emitter joins values with commas before escaping.

### D11: Form-encoded and plain text request bodies

**Decision**: Emit `FormUrlEncodedContent` for form-encoded bodies and `StringContent` for plain text bodies.

**Form-encoded**: The schema properties become form fields. The emitter flattens the schema properties into a `List<KeyValuePair<string, string>>`:

```csharp
var formFields = new List<KeyValuePair<string, string>>();
formFields.Add(new KeyValuePair<string, string>("grant_type", grantType));
if (scope is not null)
    formFields.Add(new KeyValuePair<string, string>("scope", scope));
request.Content = new FormUrlEncodedContent(formFields);
```

Like multipart (D5), form-encoded schemas are flattened into method parameters — no request body wrapper type is generated.

**Plain text**: Maps to a single `string` parameter:

```csharp
request.Content = new StringContent(body, Encoding.UTF8, "text/plain");
```

**Octet-stream request**: Maps to a `Stream` parameter:

```csharp
request.Content = new StreamContent(body);
request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
```

### D12: Diagnostic changes

**Narrowed diagnostics**:
- **AS404** (UnsupportedContentType): Now only fires for `application/xml`, `application/msgpack`, and other content types not in the supported set. Form, multipart, octet-stream, and plain text are no longer rejected.
- **AS405** (UnsupportedQueryParameterStyle): Removed. `explode: false` is now supported via inline comma-join serialization.

**New diagnostics**:
- **AS407** (UnsupportedParameterStyleCombination): Fires for style/explode combinations that are undefined per the OpenAPI spec (e.g., `deepObject` + `explode: false`) or not yet supported (e.g., `pipeDelimited`, `spaceDelimited`). Warning severity, operation parameter skipped.
- **AS408** (UnknownEncodingProperty): Fires when a multipart `encoding` object references a property name that does not exist in the schema. Info severity, encoding entry ignored.
- **AS409** (ContentTypeNegotiated): Fires when multiple content types are available and one was selected over others. Info severity, informational only.

The `DiagnosticCodes` class is updated:

```csharp
public static class DiagnosticCodes
{
    public const string MissingOperationId = "AS400";
    public const string UnsupportedInlineSchema = "AS401";
    public const string CookieParameterSkipped = "AS402";
    public const string MethodNameCollision = "AS403";
    public const string UnsupportedContentType = "AS404";
    // AS405 removed (explode: false now supported)
    public const string UnsupportedHttpMethod = "AS406";
    public const string UnsupportedParameterStyleCombination = "AS407";
    public const string UnknownEncodingProperty = "AS408";
    public const string ContentTypeNegotiated = "AS409";
    public const string TypeReused = "AS500";
    public const string TypeExcludedFromReuse = "AS501";
}
```

### D13: Template strategy for content kind branching

**Decision**: Use Scriban conditionals in the existing `ClientImplementation.sbn-cs` template, branching on `content_kind` for both request and response handling. Add new template variables to the operation model rather than splitting into separate template files.

The emitter's `BuildOperationModels` method adds content-kind-specific properties:

```csharp
result.Add(new
{
    method_name = op.CSharpMethodName,
    return_type = returnType,
    response_type = responseType,
    http_method = op.HttpMethod.ToString(),
    path_template = pathTemplate,
    is_deprecated = op.IsDeprecated,
    has_body = hasBody,
    has_response_body = hasResponseBody,
    has_query_params = hasQueryParams,
    body_content_kind = op.RequestBody?.ContentKind.ToString().ToLowerInvariant(),
    response_content_kind = responseContentKind,
    accept_header = acceptHeader,
    body_param_name = ...,
    multipart_parts = ...,
    form_fields = ...,
    parameters = allParams,
    query_params = queryParams,
    header_params = headerParams,
    serializer_fields = ...,
});
```

The template branches on `body_content_kind`:

```
{{~ if op.body_content_kind == "json" ~}}
        request.Content = JsonContent.Create({{ op.body_param_name }}, mediaType: null, _jsonOptions);
{{~ else if op.body_content_kind == "formurlencoded" ~}}
        var formFields = new List<KeyValuePair<string, string>>();
        ...
        request.Content = new FormUrlEncodedContent(formFields);
{{~ else if op.body_content_kind == "multipartformdata" ~}}
        using var content = new MultipartFormDataContent();
        ...
        request.Content = content;
{{~ else if op.body_content_kind == "octetstream" ~}}
        request.Content = new StreamContent({{ op.body_param_name }});
{{~ else if op.body_content_kind == "plaintext" ~}}
        request.Content = new StringContent({{ op.body_param_name }}, Encoding.UTF8, "text/plain");
{{~ end ~}}
```

**Scriban partials**: If the template grows past ~200 lines of conditional logic, extract each content kind's request/response block into a Scriban partial (`_MultipartBody.sbn-cs`, `_StreamResponse.sbn-cs`, etc.) loaded as additional embedded resources. This is a refactoring decision made during implementation based on template readability, not a structural design choice.

**New template files**:

| Template | Output |
|----------|--------|
| `FileResponse.sbn-cs` | `FileResponse.cs` |
| `ProblemDetails.sbn-cs` | `ProblemDetails.cs` |

These are emitted conditionally — `FileResponse` only when at least one operation returns a stream, `ProblemDetails` only when client emission is active.

**`BuildOperationModels` refactoring**: The current method (lines 94-168 of `ScribanClientEmitter.cs`) is already complex. Adding content-kind properties, multipart parts, form fields, and per-parameter serialization style will push it past maintainability limits. Break it into smaller private methods before adding the new logic: `BuildRequestBodyModel`, `BuildResponseModel`, `BuildQueryModel`. This is a prep refactoring step, not a structural design change.

### D14: DI registration — no changes needed for serialization

With D3's decision to inline serialization in templates, there are no new DI registrations needed for parameter serialization. The client constructor signature is unchanged — `IHttpClientFactory` + `{ApiName}JsonOptions`. No keyed services, no extra interfaces, no factory lambdas.

The only DI-related change in this feature set is that `EnsureSuccessAsync` becomes an instance method (it needs `_jsonOptions` for ProblemDetails deserialization), but this does not change the constructor or registration.

## Risks / Trade-offs

**[Breaking internal API]**: `ContentType` (string) changes to `ContentKind` (enum) + `MediaType` (string) on `ApiRequestBody` and `ApiResponse`. Every reference to `ContentType` in `OperationTransformer`, `ScribanClientEmitter`, and tests must be updated. Mitigation: these are internal types with no external consumers. The change is mechanical — find-and-replace followed by compiler-driven fixes.

**[Template complexity]**: `ClientImplementation.sbn-cs` grows substantially with conditional paths for five content kinds in request bodies, three response handling modes, Accept header logic, Content-Type validation, and inline serialization styles. Mitigation: (1) refactor `BuildOperationModels` into smaller methods first, (2) extract content-kind-specific blocks into Scriban partials if the template exceeds ~200 lines of conditional logic.

**[Multipart disposal]**: `MultipartFormDataContent` disposes its child `StreamContent`, which disposes the caller's `Stream`. This is an accepted convention (ASP.NET Core's `FileStreamResult` does the same). Mitigation: document in generated XML doc that the stream is consumed by the method. The caller creates the stream; the generated method disposes it via `MultipartFormDataContent.Dispose()`.

**[FileResponse disposal responsibility]**: `FileResponse` wraps the `HttpResponseMessage` and transfers disposal responsibility to the caller. If the caller forgets to dispose, the HTTP connection leaks. Mitigation: `FileResponse` implements both `IAsyncDisposable` and `IDisposable`, enabling `await using` and `using` syntax. The generated interface's XML doc comment includes a disposal reminder. Analyzers like CA2000 will flag undisposed instances.

**[ProblemDetails version]**: RFC 9457 superseded RFC 7807 and many APIs include extension fields like `errors` (validation errors). The generated `ProblemDetails` record has only the five core fields. Mitigation: the full JSON body is always available as `ResponseBody` on `ApiException`. Users who need extension fields can deserialize from `ResponseBody` using their own type. The minimal record avoids imposing a specific `errors` schema.

**[EnsureSuccessAsync becomes instance method]**: Adding ProblemDetails deserialization requires `_jsonOptions`, changing `EnsureSuccessAsync` from `private static` to `private`. This is a minor change with no external impact. The method was private before and remains private.

**[Response content negotiation]**: D6 covers request body content negotiation but the same preference ordering and AS409 diagnostic should apply to response content types too. When an operation defines both `application/json` and `application/xml` as response types, the transformer picks JSON and emits AS409 for the skipped alternative.

**[Conditional template emission]**: `FileResponse` and `ProblemDetails` are only emitted when needed. The emitter must track which features are used across all operations to decide which files to emit. Mitigation: collect feature usage during `BuildOperationModels` pass and use boolean flags to gate emission. This is the same pattern already used for `has_query_methods` and `queryEnums`.

**[ConfigureAwait(false) consistency]**: All new `await` expressions in generated code must include `.ConfigureAwait(false)`. This is library code, not app code. The existing template is consistent; new code paths (FileResponse.CreateAsync, multipart construction, ProblemDetails deserialization) must maintain this.
