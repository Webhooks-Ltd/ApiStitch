## 1. Model Layer Changes

- [x] 1.1 Add `ContentKind` enum (Json, FormUrlEncoded, MultipartFormData, OctetStream, PlainText) to `ApiStitch.Model`
- [x] 1.2 Add `ParameterStyle` enum (Form, Simple, DeepObject, PipeDelimited, SpaceDelimited) to `ApiStitch.Model`
- [x] 1.3 Add `PrimitiveType.Stream` to the `PrimitiveType` enum
- [x] 1.4 Update `CSharpTypeMapper.MapPrimitive` to map `Stream` → `"Stream"` and `IsValueType` to return `false`
- [x] 1.5 Replace `string ContentType` on `ApiRequestBody` with `required ContentKind ContentKind` + `required string MediaType`, add `IReadOnlyDictionary<string, MultipartEncoding>? PropertyEncodings`
- [x] 1.6 Replace `string ContentType` on `ApiResponse` with `ContentKind? ContentKind` + `string? MediaType`
- [x] 1.7 Add `required ParameterStyle Style` and `required bool Explode` to `ApiParameter`
- [x] 1.8 Add `MultipartEncoding` class with `required string ContentType`
- [x] 1.9 Fix all compilation errors from model changes (OperationTransformer, ScribanClientEmitter, tests)

## 2. Diagnostic Changes

- [x] 2.1 Add `AS407` (UnsupportedParameterStyleCombination), `AS408` (UnknownEncodingProperty), `AS409` (ContentTypeNegotiated) to `DiagnosticCodes`
- [x] 2.2 Remove `AS405` constant and all references in `OperationTransformer`
- [x] 2.3 Update AS404 message text ("only application/json supported" → "only application/xml and other unsupported types")

## 3. Operation Parsing — Content Type Support

- [x] 3.1 Add content-type-to-ContentKind mapping method in `OperationTransformer` (json, form, multipart, octet-stream, text/plain, pdf, image/*)
- [x] 3.2 Implement content negotiation in `TransformRequestBody` — pick best content type from preference order (JSON > form > multipart > octet-stream > text), emit AS409 for skipped alternatives
- [x] 3.3 Parse `application/x-www-form-urlencoded` request bodies — flatten schema into form fields, set ContentKind.FormUrlEncoded
- [x] 3.4 Parse `multipart/form-data` request bodies — resolve schema properties, map `format: binary` to PrimitiveType.Stream, set ContentKind.MultipartFormData
- [x] 3.5 Parse multipart `encoding` object — read per-property encoding entries, populate `PropertyEncodings` on ApiRequestBody, emit AS408 for unknown property names
- [x] 3.6 Parse `application/octet-stream` request bodies — set ContentKind.OctetStream, schema with PrimitiveType.Stream
- [x] 3.7 Parse `text/plain` request bodies — set ContentKind.PlainText, schema with PrimitiveType.String
- [x] 3.8 Parse non-JSON success responses — text/plain → string (synthesize string schema when no schema defined), octet-stream/pdf/image → Stream
- [x] 3.8a Implement response content negotiation — same preference ordering as request bodies, emit AS409 for skipped response content type alternatives
- [x] 3.9 Narrow AS404 to only fire for truly unsupported types (application/xml, application/msgpack, etc.)
- [x] 3.10 Reject $ref to complex objects in form-encoded body properties with AS401
- [x] 3.11 Write tests for each new content type parsing path (including response negotiation, text/plain no-schema synthesis, form $ref rejection)

## 4. Operation Parsing — Parameter Style Support

- [x] 4.1 Read `style` and `explode` fields from OpenAPI parameters in `TransformParameters`
- [x] 4.2 Apply OpenAPI defaults per location (query: form/true, path: simple/false, header: simple/false)
- [x] 4.3 Set `required ParameterStyle Style` and `required bool Explode` on every `ApiParameter`
- [x] 4.4 Remove AS405 rejection for `explode: false` — now accepted for form style
- [x] 4.5 Accept `deepObject` + `explode: true` on object-typed query params (lift AS401 for this case)
- [x] 4.6 Emit AS407 for unsupported combinations (deepObject+explode:false, pipeDelimited, spaceDelimited)
- [x] 4.7 Write tests for style/explode parsing, defaults, and AS407 rejection

## 5. Client Emission — Request Body Templates

- [x] 5.1 Refactor `BuildOperationModels` into smaller methods: `BuildRequestBodyModel`, `BuildResponseModel`, `BuildQueryModel`
- [x] 5.2 Add `body_content_kind`, `response_content_kind`, `accept_header` to the operation template model
- [x] 5.3 Add form-encoded template path — build `List<KeyValuePair<string, string>>` from schema properties, emit `FormUrlEncodedContent`
- [x] 5.4 Add multipart template path — emit `MultipartFormDataContent`, `StreamContent` for binary, `StringContent`/`JsonContent` for non-binary parts based on encoding
- [x] 5.5 Add `multipart_parts` model with per-part info (wire_name, param_name, is_binary, has_json_encoding, fileName_param_name)
- [x] 5.6 Add octet-stream template path — emit `StreamContent` with content-type header
- [x] 5.7 Add plain-text template path — emit `StringContent` with UTF-8 encoding
- [x] 5.8 Update parameter ordering for multipart: flatten schema properties into method params, binary gets Stream + fileName pair
- [x] 5.9 Write tests for each request body content kind emission

## 6. Client Emission — Response Handling

- [x] 6.1 Add `FileResponse.sbn-cs` template — private ctor, async `CreateAsync` factory, IAsyncDisposable + IDisposable, Stream/FileName/ContentType/ContentLength properties
- [x] 6.2 Conditionally emit `FileResponse.cs` when any operation has ContentKind.OctetStream response
- [x] 6.3 Add stream response template path — `HttpCompletionOption.ResponseHeadersRead`, no `using` on response, `using` on request, return `FileResponse.CreateAsync`
- [x] 6.4 Add plain-text response template path — `ReadAsStringAsync`, return `Task<string>`
- [x] 6.5 Add Accept header emission — `request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType))` for all operations with response body
- [x] 6.6 Add Content-Type response validation — check `ContentType?.MediaType` before `ReadFromJsonAsync`, throw `ApiException` for non-JSON content
- [x] 6.7 Write tests for each response content kind emission

## 7. Client Emission — Query Parameter Serialization

- [x] 7.1 Update `BuildQueryParamModel` to include `style` and `explode` fields
- [x] 7.2 Add form+comma template branch — `string.Join(",", items)` for array params with explode:false
- [x] 7.3 Add deepObject template branch — bracket-notation `foreach` over object properties
- [x] 7.4 Update `ClientImplementation.sbn-cs` with conditional blocks for each serialization style
- [x] 7.5 Write tests for comma-separated and deepObject query parameter emission

## 8. Error Handling Enrichment (depends on 5.1 refactoring)

- [x] 8.1 Add `ProblemDetails.sbn-cs` template — non-positional record with `[JsonPropertyName]` attributes, five core RFC 9457 fields
- [x] 8.2 Conditionally emit `ProblemDetails.cs` when client emission is active
- [x] 8.3 Update `ApiException.sbn-cs` — add `ProblemDetails? Problem` property and constructor parameter
- [x] 8.4 Update `EnsureSuccessAsync` in `ClientImplementation.sbn-cs` — change to instance method, add Content-Type check, attempt ProblemDetails deserialization with try/catch fallback
- [x] 8.5 Add `[JsonSerializable(typeof(ProblemDetails))]` to JsonSerializerContext when client emission is active
- [x] 8.6 Write tests for ProblemDetails deserialization, fallback on failure, and null when not applicable

## 9. Integration Testing

- [x] 9.1 Create test OpenAPI spec with form-encoded endpoint (e.g., OAuth token endpoint)
- [x] 9.2 Create test OpenAPI spec with multipart file upload endpoint (binary + metadata)
- [x] 9.3 Create test OpenAPI spec with octet-stream response (file download)
- [x] 9.4 Create test OpenAPI spec with text/plain request and response
- [x] 9.5 Create test OpenAPI spec with deepObject and comma-separated query params
- [x] 9.6 Create test OpenAPI spec with multiple content types per operation (content negotiation)
- [x] 9.7 Create test OpenAPI spec with multipart request + JSON response (cross-content-kind composition)
- [x] 9.8 Create test for ProblemDetails deserialization in generated EnsureSuccessAsync output
- [x] 9.9 Verify generated code compiles and matches expected output for each spec
- [x] 9.10 Verify existing tests still pass (no regressions from model changes)
