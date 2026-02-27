## ADDED Requirements

### Requirement: Emit FileResponse type with stream and metadata properties

The system SHALL emit a `public sealed class FileResponse : IAsyncDisposable, IDisposable` with properties `Stream Content`, `string? FileName`, `string? ContentType`, and `long? ContentLength`. The class SHALL use a private constructor with an `internal static async Task<FileResponse> CreateAsync(HttpResponseMessage, CancellationToken)` factory method to avoid synchronous I/O. The `FileName` SHALL be parsed from the `Content-Disposition` response header, trimming surrounding quotes. The type SHALL have the `[GeneratedCode("ApiStitch", null)]` attribute.

#### Scenario: FileResponse class structure
- **WHEN** `FileResponse` is emitted
- **THEN** the class is `public sealed` and implements both `IAsyncDisposable` and `IDisposable`
- **THEN** the constructor is `private` and accepts `HttpResponseMessage response` and `Stream content`
- **THEN** it has an `internal static async Task<FileResponse> CreateAsync(HttpResponseMessage response, CancellationToken cancellationToken)` factory method
- **THEN** the factory method calls `await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false)`
- **THEN** it has a `public Stream Content { get; }` property
- **THEN** it has a `public string? FileName { get; }` property
- **THEN** it has a `public string? ContentType { get; }` property
- **THEN** it has a `public long? ContentLength { get; }` property
- **THEN** the file has `[GeneratedCode("ApiStitch", null)]` on the type

#### Scenario: FileName parsed from Content-Disposition header
- **WHEN** the response has header `Content-Disposition: attachment; filename="report.pdf"`
- **THEN** `FileResponse.FileName` = `"report.pdf"` (quotes trimmed)

#### Scenario: FileName is null when Content-Disposition absent
- **WHEN** the response has no `Content-Disposition` header
- **THEN** `FileResponse.FileName` is null

#### Scenario: ContentType parsed from response
- **WHEN** the response has `Content-Type: application/pdf`
- **THEN** `FileResponse.ContentType` = `"application/pdf"`

#### Scenario: ContentLength parsed from response
- **WHEN** the response has `Content-Length: 1048576`
- **THEN** `FileResponse.ContentLength` = `1048576`

#### Scenario: ContentLength is null when header absent
- **WHEN** the response has no `Content-Length` header (e.g., chunked transfer)
- **THEN** `FileResponse.ContentLength` is null

#### Scenario: DisposeAsync disposes stream and response
- **WHEN** `FileResponse.DisposeAsync()` is called
- **THEN** the `Content` stream is disposed via `await Content.DisposeAsync().ConfigureAwait(false)`
- **THEN** the underlying `HttpResponseMessage` is disposed

#### Scenario: Dispose (synchronous) disposes stream and response
- **WHEN** `FileResponse.Dispose()` is called
- **THEN** the `Content` stream is disposed via `Content.Dispose()`
- **THEN** the underlying `HttpResponseMessage` is disposed

### Requirement: Return FileResponse for stream response operations

The system SHALL generate methods that return `Task<FileResponse>` for operations whose success response has `ContentKind` = `OctetStream`. This includes `application/octet-stream`, `application/pdf`, `image/*`, and any other binary content type mapped to `OctetStream`.

#### Scenario: Octet-stream response returns FileResponse
- **WHEN** an operation has a success response with content type `application/octet-stream`
- **THEN** the generated method returns `Task<FileResponse>`

#### Scenario: PDF response returns FileResponse
- **WHEN** an operation has a success response with content type `application/pdf`
- **THEN** the generated method returns `Task<FileResponse>`

#### Scenario: Image response returns FileResponse
- **WHEN** an operation has a success response with content type `image/png`
- **THEN** the generated method returns `Task<FileResponse>`

#### Scenario: FileResponse construction in generated method
- **WHEN** a stream-response method is emitted
- **THEN** the method body calls `await FileResponse.CreateAsync(response, cancellationToken).ConfigureAwait(false)`
- **THEN** `EnsureSuccessAsync` is called before constructing `FileResponse`

### Requirement: Use ResponseHeadersRead completion option for stream responses

The system SHALL use `HttpCompletionOption.ResponseHeadersRead` when calling `SendAsync` for operations that return `FileResponse`. This prevents the entire response body from being buffered in memory.

#### Scenario: Stream response uses ResponseHeadersRead
- **WHEN** a generated method returns `Task<FileResponse>`
- **THEN** the `SendAsync` call uses `HttpCompletionOption.ResponseHeadersRead` as the second argument

#### Scenario: JSON response does not use ResponseHeadersRead
- **WHEN** a generated method returns a JSON-deserialized type (e.g., `Task<Pet>`)
- **THEN** the `SendAsync` call does NOT specify `HttpCompletionOption.ResponseHeadersRead`

### Requirement: Transfer HttpResponseMessage ownership to FileResponse

The system SHALL NOT wrap the `HttpResponseMessage` in a `using` statement for stream response methods. Ownership of the `HttpResponseMessage` SHALL transfer to `FileResponse`, which is responsible for disposing it when the caller disposes `FileResponse`. The `HttpRequestMessage` SHALL still be disposed with `using` (it has already been sent).

#### Scenario: No using on HttpResponseMessage for stream responses
- **WHEN** a generated method returns `Task<FileResponse>`
- **THEN** the `HttpResponseMessage` variable is NOT declared with `using`
- **THEN** the `HttpResponseMessage` is passed to `FileResponse.CreateAsync(response, cancellationToken)` which takes ownership

#### Scenario: HttpRequestMessage still uses using for stream responses
- **WHEN** a generated method returns `Task<FileResponse>`
- **THEN** the `HttpRequestMessage` IS declared with `using` (it has already been sent and is safe to dispose)

#### Scenario: JSON response still uses using on HttpResponseMessage
- **WHEN** a generated method returns a JSON-deserialized type
- **THEN** the `HttpResponseMessage` variable IS declared with `using` (existing behavior)

#### Scenario: Caller disposes FileResponse
- **WHEN** the caller uses `await using var file = await client.DownloadReportAsync(...)`
- **THEN** `FileResponse.DisposeAsync` disposes the stream and the underlying `HttpResponseMessage`

### Requirement: Emit FileResponse conditionally

The system SHALL only emit the `FileResponse.cs` file when at least one operation across all clients returns a stream response (`ContentKind.OctetStream`). When no operations return stream responses, `FileResponse.cs` SHALL NOT be emitted.

#### Scenario: At least one stream response triggers emission
- **WHEN** an API has 10 operations and one returns `application/octet-stream`
- **THEN** `FileResponse.cs` is emitted

#### Scenario: No stream responses skips emission
- **WHEN** all operations in the API return only JSON or no-content responses
- **THEN** `FileResponse.cs` is NOT emitted
- **THEN** no `FileResponse` type appears in the generated output

#### Scenario: FileResponse emitted once per namespace
- **WHEN** two clients in the same namespace both have stream-returning operations
- **THEN** only one `FileResponse.cs` file is emitted (shared across clients)
