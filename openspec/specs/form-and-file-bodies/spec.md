# form-and-file-bodies Specification

## Purpose
TBD - created by archiving change advanced-http-support. Update Purpose after archive.
## Requirements
### Requirement: Emit FormUrlEncodedContent for form-encoded request bodies

The system SHALL flatten `application/x-www-form-urlencoded` schema properties into individual method parameters and emit `FormUrlEncodedContent` from a `List<KeyValuePair<string, string>>` of form fields. Required properties SHALL be non-nullable parameters; optional properties SHALL be nullable parameters with `= null` default. Optional fields SHALL only be added to the form content when the value is not null.

#### Scenario: Simple form with required and optional fields
- **WHEN** an operation has a form-encoded request body with schema properties `grant_type` (string, required) and `scope` (string, optional)
- **THEN** the generated method has parameters `string grantType` and `string? scope = null`
- **THEN** the method body creates `FormUrlEncodedContent` from form field key-value pairs
- **THEN** `grant_type` is always added to the form fields
- **THEN** `scope` is only added when not null

#### Scenario: Form fields use original property names as keys
- **WHEN** a form-encoded schema has property `grant_type`
- **THEN** the form field key is `"grant_type"` (the original OpenAPI property name, not the C# parameter name `grantType`)

#### Scenario: No wrapper type is generated for form schemas
- **WHEN** an operation has a form-encoded request body
- **THEN** no C# record or class is generated for the request body schema
- **THEN** each schema property becomes a direct method parameter

#### Scenario: Integer form field converted to string
- **WHEN** a form-encoded schema has property `count` with type `integer, format: int32`
- **THEN** the generated method has parameter `int count`
- **THEN** the form field value is `count.ToString()` (string conversion for FormUrlEncodedContent)

#### Scenario: Form-encoded body with $ref to complex object is rejected
- **WHEN** a form-encoded schema has a property that is a `$ref` to a complex object type (e.g., `address: {$ref: '#/components/schemas/Address'}`)
- **THEN** the property is skipped (not included as a form field)
- **THEN** a warning diagnostic with code `AS401` is emitted indicating complex objects cannot be serialized as form fields

### Requirement: Emit MultipartFormDataContent for multipart request bodies

The system SHALL flatten `multipart/form-data` schema properties into individual method parameters and emit `MultipartFormDataContent`. Non-binary properties SHALL be added as `StringContent`. The system SHALL use `using var content = new MultipartFormDataContent()` so that MultipartFormDataContent is disposed after `SendAsync` completes.

#### Scenario: Multipart with string property
- **WHEN** a multipart schema has property `description` (type: string, optional)
- **THEN** the generated method has parameter `string? description = null`
- **THEN** the method body adds `new StringContent(description)` with part name `"description"` when not null

#### Scenario: Multipart with required string property
- **WHEN** a multipart schema has property `title` (type: string, required)
- **THEN** the generated method has parameter `string title`
- **THEN** the method body always adds `new StringContent(title)` with part name `"title"`

#### Scenario: No wrapper type is generated for multipart schemas
- **WHEN** an operation has a multipart request body
- **THEN** no C# record or class is generated for the request body schema
- **THEN** each schema property becomes a direct method parameter

#### Scenario: Multipart with only non-binary properties
- **WHEN** a multipart schema has only string properties (`name`, `email`, `message`) and no binary properties
- **THEN** no `fileName` parameters are generated
- **THEN** all properties are added as `StringContent` parts

### Requirement: Handle binary properties in multipart as Stream and fileName parameters

The system SHALL map `format: binary` properties in multipart schemas to a `Stream` parameter and a `string fileName` parameter. The stream SHALL be added as `StreamContent` with the filename passed to `MultipartFormDataContent.Add`.

#### Scenario: Single binary file upload
- **WHEN** a multipart schema has property `file` (type: string, format: binary, required)
- **THEN** the generated method has parameters `Stream file` and `string fileName`
- **THEN** the method body adds `new StreamContent(file)` with part name `"file"` and filename `fileName`

#### Scenario: Multiple binary properties
- **WHEN** a multipart schema has properties `photo` (format: binary) and `thumbnail` (format: binary)
- **THEN** the generated method has parameters `Stream photo`, `string photoFileName`, `Stream thumbnail`, `string thumbnailFileName`

#### Scenario: Mixed binary and non-binary properties
- **WHEN** a multipart schema has `file` (format: binary, required), `description` (string, optional)
- **THEN** the generated method has parameters `Stream file`, `string fileName`, `string? description = null`, `CancellationToken cancellationToken = default`
- **THEN** binary properties map to `PrimitiveType.Stream` in the semantic model

#### Scenario: Binary property uses PrimitiveType.Stream
- **WHEN** a multipart schema property has `format: binary`
- **THEN** the corresponding `ApiSchema` has `PrimitiveType` = `Stream`
- **THEN** `CSharpTypeMapper` maps `PrimitiveType.Stream` to the C# type `"Stream"`

### Requirement: Support OpenAPI encoding object for JSON parts in multipart

The system SHALL read the OpenAPI `encoding` object on multipart request bodies. When a property's encoding specifies `contentType: application/json`, the system SHALL serialize that property as `JsonContent.Create` instead of `StringContent`. The encoding property names SHALL be validated against the schema properties; unknown encoding properties SHALL emit an `AS408` info diagnostic and be ignored.

#### Scenario: JSON-encoded property in multipart
- **WHEN** a multipart schema has property `metadata` ($ref to `FileMetadata`) and encoding `metadata: { contentType: application/json }`
- **THEN** the generated method has parameter `FileMetadata? metadata = null`
- **THEN** the method body adds `JsonContent.Create(metadata, mediaType: null, _jsonOptions)` with part name `"metadata"`

#### Scenario: Property without encoding entry defaults to StringContent
- **WHEN** a multipart schema has property `description` (string) with no encoding entry
- **THEN** the method body adds `new StringContent(description)` (not `JsonContent.Create`)

#### Scenario: Unknown encoding property name emits diagnostic
- **WHEN** the encoding object has key `nonExistentProp` that does not match any schema property
- **THEN** an info diagnostic with code `AS408` is emitted
- **THEN** the unknown encoding entry is ignored

#### Scenario: Encoding object stored on ApiRequestBody
- **WHEN** a multipart request body has encoding entries
- **THEN** `ApiRequestBody.PropertyEncodings` is a non-null dictionary mapping property names to `MultipartEncoding` instances
- **THEN** each `MultipartEncoding` has its `ContentType` set from the encoding object

### Requirement: Emit StreamContent for octet-stream request bodies

The system SHALL map `application/octet-stream` request bodies to a `Stream` method parameter and emit `StreamContent` with the `Content-Type` header set to `application/octet-stream`.

#### Scenario: Octet-stream request body
- **WHEN** an operation has a request body with content type `application/octet-stream`
- **THEN** the generated method has a `Stream body` parameter
- **THEN** the method body sets `request.Content = new StreamContent(body)`
- **THEN** the method body sets `request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream")`

#### Scenario: Octet-stream schema maps to PrimitiveType.Stream
- **WHEN** a request body has content type `application/octet-stream` with schema `type: string, format: binary`
- **THEN** `ApiRequestBody.Schema.PrimitiveType` = `Stream`

### Requirement: Emit StringContent for text/plain request bodies

The system SHALL map `text/plain` request bodies to a `string` method parameter and emit `StringContent` with UTF-8 encoding and `text/plain` media type.

#### Scenario: Text/plain request body
- **WHEN** an operation has a request body with content type `text/plain`
- **THEN** the generated method has a `string body` parameter
- **THEN** the method body sets `request.Content = new StringContent(body, Encoding.UTF8, "text/plain")`

#### Scenario: Text/plain schema maps to PrimitiveType.String
- **WHEN** a request body has content type `text/plain` with schema `type: string`
- **THEN** `ApiRequestBody.Schema.PrimitiveType` = `String`

### Requirement: Deserialize text/plain responses as string

The system SHALL map `text/plain` success responses to a `string` return type and emit `ReadAsStringAsync` for deserialization.

#### Scenario: Text/plain response returns string
- **WHEN** an operation has a success response with content type `text/plain` and schema `type: string`
- **THEN** the generated method returns `Task<string>`
- **THEN** the method body uses `await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)`

#### Scenario: Text/plain response with no schema returns string
- **WHEN** an operation has a success response with content type `text/plain` and no schema defined
- **THEN** the generated method returns `Task<string>`
- **THEN** deserialization uses `ReadAsStringAsync`

