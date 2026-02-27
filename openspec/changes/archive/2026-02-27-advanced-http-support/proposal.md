## Why

ApiStitch currently only supports `application/json` request/response bodies and basic scalar/array query parameters with `explode: true`. This blocks generation of clients for any API that uses form-encoded bodies (OAuth, payment gateways), file uploads (document management), file downloads (report exports), or non-default parameter serialization styles (Stripe/Shopify filters). These are common patterns — not edge cases — and their absence is the single largest adoption gap in the MMVP.

## What Changes

- Add `ContentKind` enum to semantic model (`Json`, `FormUrlEncoded`, `MultipartFormData`, `OctetStream`, `PlainText`) replacing raw content-type strings on `ApiRequestBody` and `ApiResponse`
- Add `PrimitiveType.Stream` for binary body parameters and streaming responses
- Add `ParameterStyle` enum and `Explode` property to `ApiParameter` (form, simple, deepObject, pipeDelimited, spaceDelimited)
- Emit `FileResponse` runtime type wrapping `Stream` + filename + content type + content length for streaming downloads
- Emit `IParameterSerializer` interface with built-in implementations (`FormExplodeSerializer`, `FormCommaSerializer`, `DeepObjectSerializer`) in generated runtime
- Parse and emit `application/x-www-form-urlencoded` request bodies as `FormUrlEncodedContent`
- Parse and emit `multipart/form-data` request bodies with `MultipartFormDataContent`, mapping `format: binary` properties to `Stream` parameters with filename
- Parse and emit mixed multipart bodies (JSON metadata parts + binary file parts) using OpenAPI `encoding` object
- Parse and emit `application/octet-stream` request bodies as `StreamContent`
- Parse and emit `text/plain` request and response bodies as `string` / `StringContent`
- Parse and emit stream response bodies (`application/octet-stream`, `application/pdf`, `image/*`) returning `FileResponse`
- Emit `Accept` header on every request matching the expected response content type
- Emit `Content-Type` validation before deserialization (guard against HTML error pages from WAFs/proxies)
- Add `ProblemDetails` deserialization to `ApiException` for `application/problem+json` error responses
- Support `form` + `explode: false` (comma-separated arrays), `deepObject` (bracket notation for objects), and content negotiation (prefer JSON when multiple content types available)
- Remove AS404 diagnostic for now-supported content types; keep AS404 for `application/xml`
- Remove AS405 diagnostic for `explode: false`; add new diagnostics for unsupported combinations (e.g., `spaceDelimited` + `explode: false`)

## Capabilities

### New Capabilities
- `content-type-handling`: ContentKind enum, content type parsing/selection, Accept header generation, Content-Type response validation, content negotiation when multiple types offered
- `form-and-file-bodies`: Form-encoded request bodies, multipart/form-data with file uploads, mixed multipart (JSON + binary parts), octet-stream request bodies, text/plain request/response bodies
- `stream-responses`: Streaming response bodies (file downloads), FileResponse runtime type with Content-Disposition/filename, disposal semantics
- `parameter-serialization`: IParameterSerializer abstraction, ParameterStyle/Explode on ApiParameter, built-in serializers (form-explode, form-comma, deep-object), object-typed query parameters via deepObject
- `error-handling-enrichment`: ProblemDetails deserialization on ApiException for application/problem+json errors

### Modified Capabilities
- `schema-model`: Add `PrimitiveType.Stream` for binary body parameters; `ContentKind` enum on `ApiRequestBody` and `ApiResponse` replacing string `ContentType`
- `operation-parsing`: Lift AS404 for form/multipart/octet-stream/text content types; lift AS405 for explode:false; parse `style`/`explode` from OpenAPI params; parse `encoding` object on multipart schemas; parse non-JSON response content types
- `client-emission`: New template paths for form bodies, multipart bodies, stream content, text content, stream responses, Accept headers, Content-Type validation; IParameterSerializer injection; FileResponse return type; ProblemDetails in EnsureSuccessAsync
- `model-emission`: Add `[JsonSerializable(typeof(ProblemDetails))]` to JsonSerializerContext when client emission is active

## Impact

- **Model layer**: `ApiRequestBody`, `ApiResponse`, `ApiParameter`, `PrimitiveType` all get new/changed properties
- **Parsing**: `OperationTransformer` gains multiple new code paths for content types, parameter styles, and encoding objects
- **Emission**: `ScribanClientEmitter` needs new templates (or template branches) for each content kind; new generated runtime types (`FileResponse`, `IParameterSerializer`, serializer implementations, `ProblemDetails`)
- **Templates**: `ClientImplementation.sbn-cs` and `ClientInterface.sbn-cs` gain significant new conditional blocks
- **DI registration**: Serializer implementations registered; `FileResponse` available
- **Generated output dependencies**: `System.Net.Http`, `System.IO` (Stream), `System.Text` (Encoding) — all already in the BCL, no new NuGet dependencies
- **Breaking**: `ApiRequestBody.ContentType` and `ApiResponse.ContentType` change from `string` to `ContentKind` enum — this is an internal model change, not a user-facing API break
- **Diagnostics**: AS404 scope narrowed (only XML now), AS405 removed, new diagnostic codes for unsupported style combinations
