## MODIFIED Requirements

### Requirement: Parse request bodies

The system SHALL parse request body definitions into ApiRequestBody instances. When an operation offers multiple content types, the system SHALL select the best one using a deterministic preference order: `application/json` > `application/x-www-form-urlencoded` > `multipart/form-data` > `application/octet-stream` > `text/plain`. The selected content type SHALL be represented as a `ContentKind` enum value with the original media type string preserved in `MediaType`. Content types not in the supported set (e.g., `application/xml`) SHALL emit AS404 and the request body SHALL be skipped.

#### Scenario: JSON request body with $ref schema
- **WHEN** an operation has `requestBody` with `content: application/json` and schema `$ref: '#/components/schemas/Pet'`
- **THEN** ApiOperation.RequestBody is populated with Schema pointing to the Pet ApiSchema, ContentKind = Json, MediaType = "application/json"

#### Scenario: Required request body
- **WHEN** the request body has `required: true`
- **THEN** ApiRequestBody.IsRequired = true

#### Scenario: Optional request body
- **WHEN** the request body has `required: false` or no `required` field
- **THEN** ApiRequestBody.IsRequired = false

#### Scenario: JSON request body with inline array of $ref items
- **WHEN** an operation has `requestBody` with `content: application/json` and schema `type: array, items: {$ref: '#/components/schemas/Pet'}`
- **THEN** ApiRequestBody.Schema has Kind = Array with ArrayItemSchema pointing to the Pet ApiSchema

#### Scenario: Form-encoded request body
- **WHEN** the request body content type is `application/x-www-form-urlencoded` with a schema containing properties `grant_type` (string) and `scope` (string)
- **THEN** ApiRequestBody has ContentKind = FormUrlEncoded, MediaType = "application/x-www-form-urlencoded"
- **THEN** ApiRequestBody.Schema contains the resolved schema with its properties

#### Scenario: Multipart form-data request body
- **WHEN** the request body content type is `multipart/form-data` with a schema containing `file` (format: binary) and `description` (string)
- **THEN** ApiRequestBody has ContentKind = MultipartFormData, MediaType = "multipart/form-data"
- **THEN** the `file` property's schema has PrimitiveType = Stream (not ByteArray)

#### Scenario: Octet-stream request body
- **WHEN** the request body content type is `application/octet-stream`
- **THEN** ApiRequestBody has ContentKind = OctetStream, MediaType = "application/octet-stream"
- **THEN** ApiRequestBody.Schema has PrimitiveType = Stream

#### Scenario: Plain text request body
- **WHEN** the request body content type is `text/plain`
- **THEN** ApiRequestBody has ContentKind = PlainText, MediaType = "text/plain"
- **THEN** ApiRequestBody.Schema has PrimitiveType = String

#### Scenario: Unsupported content type (XML)
- **WHEN** the request body content type is `application/xml`
- **THEN** the request body is skipped
- **THEN** a warning diagnostic with code `AS404` is emitted indicating unsupported content type

#### Scenario: Content negotiation selects best type
- **WHEN** an operation offers `content: {application/json: ..., application/x-www-form-urlencoded: ...}`
- **THEN** `application/json` is selected (highest preference)
- **THEN** an info diagnostic with code `AS409` is emitted noting which content type was selected and which alternatives were skipped

#### Scenario: Content negotiation with only non-JSON types
- **WHEN** an operation offers `content: {multipart/form-data: ..., application/x-www-form-urlencoded: ...}`
- **THEN** `application/x-www-form-urlencoded` is selected (higher preference than multipart)
- **THEN** an info diagnostic with code `AS409` is emitted

#### Scenario: Inline complex schema in required request body
- **WHEN** the request body schema is an inline object (not a $ref) and `required: true`
- **THEN** the operation is skipped (method not generated)
- **THEN** a warning diagnostic with code `AS401` is emitted

#### Scenario: Inline complex schema in optional request body
- **WHEN** the request body schema is an inline object (not a $ref) and `required: false`
- **THEN** the operation is skipped (method not generated)
- **THEN** a warning diagnostic with code `AS401` is emitted

### Requirement: Handle array query parameters with explode style

The system SHALL support array-typed query parameters with both `explode: true` (repeated key=value pairs) and `explode: false` (comma-separated values). The system SHALL read the `style` and `explode` fields from each parameter and select the appropriate serialization strategy.

#### Scenario: Array query parameter with default explode
- **WHEN** a query parameter has `type: array` with `items: {type: string}` and no explicit style/explode settings
- **THEN** the parameter is accepted with Style = Form, Explode = true (OpenAPI defaults)

#### Scenario: Array query parameter with explicit explode false
- **WHEN** a query parameter has `type: array` with `style: form, explode: false`
- **THEN** the parameter is accepted with Style = Form, Explode = false
- **THEN** the emitter uses inline comma-join serialization (`string.Join(",", ...)`) for this parameter

## ADDED Requirements

### Requirement: Parse parameter style and explode from OpenAPI parameters

The system SHALL read the `style` and `explode` fields from each OpenAPI parameter and map them to `ParameterStyle` and `Explode` properties on `ApiParameter`. When `style` and `explode` are not explicitly specified, the system SHALL apply OpenAPI defaults: query parameters default to `style: form, explode: true`; path parameters default to `style: simple, explode: false`; header parameters default to `style: simple, explode: false`. The system SHALL emit AS407 for unsupported style/explode combinations and skip the parameter.

#### Scenario: Query parameter with default style and explode
- **WHEN** a query parameter has no explicit `style` or `explode` fields
- **THEN** ApiParameter.Style = Form and ApiParameter.Explode = true

#### Scenario: Path parameter with default style and explode
- **WHEN** a path parameter has no explicit `style` or `explode` fields
- **THEN** ApiParameter.Style = Simple and ApiParameter.Explode = false

#### Scenario: Header parameter with default style and explode
- **WHEN** a header parameter has no explicit `style` or `explode` fields
- **THEN** ApiParameter.Style = Simple and ApiParameter.Explode = false

#### Scenario: Query parameter with explicit deepObject style
- **WHEN** a query parameter has `style: deepObject, explode: true` and schema `type: object` with properties
- **THEN** ApiParameter.Style = DeepObject and ApiParameter.Explode = true
- **THEN** the object schema is resolved (via $ref or inline properties) and the parameter is accepted

#### Scenario: deepObject lifts AS401 for object-typed query params
- **WHEN** a query parameter has `style: deepObject` and an inline object schema with properties
- **THEN** the parameter is NOT skipped with AS401 (deepObject supports object-typed query params)
- **THEN** the parameter is accepted with Style = DeepObject

#### Scenario: Unsupported style combination (deepObject + explode false)
- **WHEN** a query parameter has `style: deepObject, explode: false`
- **THEN** the parameter is skipped
- **THEN** a warning diagnostic with code `AS407` is emitted indicating unsupported style/explode combination

#### Scenario: Unsupported style (pipeDelimited)
- **WHEN** a query parameter has `style: pipeDelimited`
- **THEN** the parameter is skipped
- **THEN** a warning diagnostic with code `AS407` is emitted

#### Scenario: Unsupported style (spaceDelimited)
- **WHEN** a query parameter has `style: spaceDelimited`
- **THEN** the parameter is skipped
- **THEN** a warning diagnostic with code `AS407` is emitted

#### Scenario: Simple style for path parameter with array
- **WHEN** a path parameter has `style: simple` and schema `type: array`
- **THEN** ApiParameter.Style = Simple (comma-separated values in path, handled by emitter)

### Requirement: Parse success responses with non-JSON content types

The system SHALL parse success responses (2xx) with content types beyond `application/json`. The system SHALL support `text/plain` responses (mapped to PrimitiveType.String), `application/octet-stream` responses (mapped to PrimitiveType.Stream), and other binary content types (`application/pdf`, `image/*`) mapped to PrimitiveType.Stream. The `ContentKind` and `MediaType` on `ApiResponse` SHALL be set accordingly.

#### Scenario: 200 response with JSON body
- **WHEN** an operation has `responses: 200` with `content: application/json` and schema `$ref: '#/components/schemas/Pet'`
- **THEN** ApiResponse has StatusCode = 200, ContentKind = Json, MediaType = "application/json", Schema pointing to Pet

#### Scenario: 200 response with text/plain body
- **WHEN** an operation has `responses: 200` with `content: text/plain` and schema `type: string`
- **THEN** ApiResponse has StatusCode = 200, ContentKind = PlainText, MediaType = "text/plain"
- **THEN** ApiResponse.Schema has PrimitiveType = String

#### Scenario: 200 response with octet-stream body (file download)
- **WHEN** an operation has `responses: 200` with `content: application/octet-stream` and schema `type: string, format: binary`
- **THEN** ApiResponse has StatusCode = 200, ContentKind = OctetStream, MediaType = "application/octet-stream"
- **THEN** ApiResponse.Schema has PrimitiveType = Stream

#### Scenario: 200 response with application/pdf body
- **WHEN** an operation has `responses: 200` with `content: application/pdf` and schema `type: string, format: binary`
- **THEN** ApiResponse has StatusCode = 200, ContentKind = OctetStream, MediaType = "application/pdf"
- **THEN** ApiResponse.Schema has PrimitiveType = Stream

#### Scenario: 200 response with image content type
- **WHEN** an operation has `responses: 200` with `content: image/png` and schema `type: string, format: binary`
- **THEN** ApiResponse has StatusCode = 200, ContentKind = OctetStream, MediaType = "image/png"
- **THEN** ApiResponse.Schema has PrimitiveType = Stream

#### Scenario: Unsupported response content type (XML)
- **WHEN** an operation has `responses: 200` with `content: application/xml`
- **THEN** the response body is skipped (treated as no-content)
- **THEN** a warning diagnostic with code `AS404` is emitted

#### Scenario: Response content negotiation selects best type
- **WHEN** an operation has `responses: 200` with `content: {application/json: ..., application/xml: ...}`
- **THEN** `application/json` is selected (highest preference)
- **THEN** an info diagnostic with code `AS409` is emitted noting which content type was selected and which alternatives were skipped

#### Scenario: text/plain response with no schema synthesizes string schema
- **WHEN** an operation has `responses: 200` with `content: text/plain` and no schema defined
- **THEN** the OperationTransformer synthesizes an `ApiSchema` with Kind = Primitive and PrimitiveType = String
- **THEN** `ApiResponse.HasBody` is true (Schema is not null)
- **THEN** the generated method returns `Task<string>`

### Requirement: Parse multipart encoding object

The system SHALL read `encoding` entries from multipart request body schemas and map them to `PropertyEncodings` on `ApiRequestBody`. Each encoding entry specifies the `contentType` for a named property in the multipart schema. When an encoding entry references a property name that does not exist in the schema, the system SHALL emit AS408 and ignore the entry.

#### Scenario: Multipart with encoding for JSON metadata part
- **WHEN** a multipart request body has `encoding: {metadata: {contentType: application/json}}`
- **THEN** ApiRequestBody.PropertyEncodings contains an entry for "metadata" with ContentType = "application/json"

#### Scenario: Multipart with no encoding object
- **WHEN** a multipart request body has no `encoding` field
- **THEN** ApiRequestBody.PropertyEncodings is null

#### Scenario: Encoding references unknown property
- **WHEN** a multipart encoding references property name "nonExistent" which is not in the schema's properties
- **THEN** the encoding entry is ignored
- **THEN** an info diagnostic with code `AS408` is emitted identifying the unknown property name

#### Scenario: Multiple encoding entries
- **WHEN** a multipart request body has `encoding: {metadata: {contentType: application/json}, tags: {contentType: application/json}}`
- **THEN** ApiRequestBody.PropertyEncodings contains entries for both "metadata" and "tags"
