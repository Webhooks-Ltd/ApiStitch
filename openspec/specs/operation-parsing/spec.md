# operation-parsing Specification

## Purpose
TBD - created by archiving change typed-httpclient-generation. Update Purpose after archive.
## Requirements
### Requirement: Parse OpenAPI paths into ApiOperation instances

The system SHALL parse each operation in `document.Paths` into an `ApiOperation` instance, populating HTTP method, path, operationId, tag, parameters, request body, and success response.

#### Scenario: Simple GET operation with operationId and tag
- **WHEN** the spec defines `GET /pets` with `operationId: listPets` and `tags: [Pets]`
- **THEN** an ApiOperation is created with Path = "pets", HttpMethod = Get, OperationId = "listPets", Tag = "Pets"

#### Scenario: Operation with no operationId
- **WHEN** an operation has no `operationId` field
- **THEN** an ApiOperation is still created with OperationId derived from HTTP method + path (e.g., `GET /pets/{petId}` → `GetPetsByPetId`)
- **THEN** a warning diagnostic with code `AS400` is emitted recommending the spec author add `operationId`

#### Scenario: Operation with no tags
- **WHEN** an operation has no `tags` field or an empty tags array
- **THEN** the ApiOperation has Tag set to the default client name (the API name without tag suffix)

#### Scenario: Operation with multiple tags
- **WHEN** an operation has `tags: [Pets, Admin]`
- **THEN** two ApiOperation instances are created — one with Tag = "Pets" and one with Tag = "Admin"
- **THEN** both operations have the same method signature and semantics

#### Scenario: Deprecated operation
- **WHEN** an operation has `deprecated: true`
- **THEN** ApiOperation.IsDeprecated = true

#### Scenario: Operation description
- **WHEN** an operation has `description: "Lists all pets"`
- **THEN** ApiOperation.Description = "Lists all pets"

### Requirement: Classify parameters by location

The system SHALL classify each operation parameter as Path, Query, or Header based on the OpenAPI `in` field, creating ApiParameter instances with the correct ParameterLocation.

#### Scenario: Path parameter
- **WHEN** a parameter has `in: path` with `name: petId` and schema `type: integer, format: int64`
- **THEN** an ApiParameter is created with Location = Path, Name = "petId", IsRequired = true
- **THEN** the Schema points to the resolved ApiSchema for int64

#### Scenario: Required query parameter
- **WHEN** a parameter has `in: query`, `name: status`, `required: true`, and schema referencing a string enum
- **THEN** an ApiParameter is created with Location = Query, IsRequired = true
- **THEN** the Schema points to the resolved enum ApiSchema

#### Scenario: Optional query parameter
- **WHEN** a parameter has `in: query`, `name: limit`, `required: false`, and schema `type: integer, format: int32`
- **THEN** an ApiParameter is created with Location = Query, IsRequired = false

#### Scenario: Header parameter
- **WHEN** a parameter has `in: header`, `name: X-Request-Id`, and schema `type: string`
- **THEN** an ApiParameter is created with Location = Header

#### Scenario: Cookie parameter emits diagnostic and is skipped
- **WHEN** a parameter has `in: cookie`
- **THEN** the parameter is not included in the ApiOperation
- **THEN** a warning diagnostic with code `AS402` is emitted

#### Scenario: Path parameters are always required
- **WHEN** a parameter has `in: path` regardless of the `required` field value
- **THEN** ApiParameter.IsRequired = true (OpenAPI mandates path params are required)

### Requirement: Resolve parameter schemas against schema map

The system SHALL resolve parameter schemas that use `$ref` to the corresponding ApiSchema instance from the schema map produced by SchemaTransformer. Inline primitive schemas SHALL be created as new ApiSchema instances.

#### Scenario: Parameter with $ref schema
- **WHEN** a parameter schema is `$ref: '#/components/schemas/PetStatus'`
- **THEN** the ApiParameter.Schema points to the same ApiSchema instance as the component schema (reference equality)

#### Scenario: Parameter with inline primitive schema
- **WHEN** a parameter has an inline `type: string` schema (no $ref)
- **THEN** a new ApiSchema with Kind = Primitive and PrimitiveType = String is created for the parameter

#### Scenario: Parameter with inline array of $ref items
- **WHEN** a parameter has `type: array` with `items: {$ref: '#/components/schemas/PetStatus'}`
- **THEN** an ApiSchema with Kind = Array is created, with ArrayItemSchema pointing to the PetStatus ApiSchema

#### Scenario: Parameter with inline complex object schema
- **WHEN** a parameter has an inline `type: object` with properties
- **THEN** the parameter is skipped (not included in ApiOperation)
- **THEN** a warning diagnostic with code `AS401` is emitted

#### Scenario: Parameter with inline allOf composition
- **WHEN** a parameter has an inline `allOf` composition
- **THEN** the parameter is skipped (not included in ApiOperation)
- **THEN** a warning diagnostic with code `AS401` is emitted

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

### Requirement: Parse success responses

The system SHALL identify the success response (2xx) and create an ApiResponse instance with status code, content type, and resolved schema.

#### Scenario: 200 response with JSON body
- **WHEN** an operation has `responses: 200` with `content: application/json` and schema `$ref: '#/components/schemas/Pet'`
- **THEN** ApiOperation.SuccessResponse has StatusCode = 200, ContentType = "application/json", Schema pointing to Pet

#### Scenario: 200 response with array body
- **WHEN** the response schema is `type: array` with `items: {$ref: '#/components/schemas/Pet'}`
- **THEN** ApiResponse.Schema has Kind = Array with ArrayItemSchema pointing to Pet

#### Scenario: 201 response (created)
- **WHEN** an operation has `responses: 201` with JSON body
- **THEN** ApiResponse.StatusCode = 201 and HasBody = true

#### Scenario: 204 response (no content)
- **WHEN** an operation has `responses: 204` with no content
- **THEN** ApiResponse.StatusCode = 204 and HasBody = false (Schema is null)

#### Scenario: Multiple 2xx responses
- **WHEN** an operation has both `200` and `201` responses
- **THEN** the lowest 2xx status code with a body is used as the success response

#### Scenario: Lower 2xx unsupported, later 2xx supported
- **WHEN** an operation has `200` with unsupported inline response composition and `201` with a supported response schema
- **THEN** the operation is NOT skipped
- **THEN** the `201` response is selected as the success response
- **THEN** an `AS401` warning is emitted for the unsupported `200` response shape with a response-inline reason

#### Scenario: No 2xx response defined
- **WHEN** an operation has no 2xx response codes
- **THEN** ApiOperation.SuccessResponse is null (method returns Task, not Task<T>)

#### Scenario: Inline complex object schema in response
- **WHEN** the response schema is an inline object (not a $ref) with properties and/or additionalProperties
- **THEN** the operation is NOT skipped
- **THEN** a synthetic generated ApiSchema is created and assigned as ApiResponse.Schema
- **THEN** no `AS401` warning is emitted for this supported inline object case

#### Scenario: Inline primitive schema in response
- **WHEN** the response schema is inline primitive (for example `type: string`)
- **THEN** the operation is NOT skipped
- **THEN** ApiResponse.Schema is mapped as a primitive schema type
- **THEN** no `AS401` warning is emitted for this supported inline primitive case

#### Scenario: Inline array with primitive items in response
- **WHEN** the response schema is inline `type: array` with primitive `items`
- **THEN** the operation is NOT skipped
- **THEN** ApiResponse.Schema is mapped as an array schema with primitive item schema
- **THEN** no `AS401` warning is emitted for this supported inline array case

#### Scenario: Inline response with additionalProperties primitive values
- **WHEN** the response schema is inline `type: object` with `additionalProperties: { type: integer }`
- **THEN** the operation is NOT skipped
- **THEN** a synthetic generated ApiSchema is created for the response

#### Scenario: Inline response with nested inline object property
- **WHEN** the response schema is inline `type: object` and includes a property whose schema is an inline `type: object`
- **THEN** the operation is skipped for this response shape
- **THEN** warning diagnostic `AS401` is emitted with reason indicating nested inline object members are unsupported in v1

#### Scenario: Inline complex unsupported composition in response
- **WHEN** the response schema is inline and uses unsupported composition (`oneOf`, `anyOf`, `allOf`, or `not`) that cannot be represented by current semantic mapping
- **THEN** the operation is skipped
- **THEN** a warning diagnostic with code `AS401` is emitted with a reason indicating unsupported inline response composition

#### Scenario: AS401 response-inline message contract
- **WHEN** an operation is skipped due to unsupported inline response schema
- **THEN** `AS401` includes the response location, explicit reason category, and remediation hint to move the schema into `components` and reference via `$ref`

### Requirement: Derive C# method names from operationId or path

The system SHALL compute CSharpMethodName for each ApiOperation using PascalCase conversion of the operationId (with `Async` suffix), or by deriving from HTTP method + path when operationId is absent.

#### Scenario: operationId in camelCase
- **WHEN** operationId is `listPets`
- **THEN** CSharpMethodName = "ListPetsAsync"

#### Scenario: operationId in snake_case
- **WHEN** operationId is `get_pet_by_id`
- **THEN** CSharpMethodName = "GetPetByIdAsync"

#### Scenario: operationId in kebab-case
- **WHEN** operationId is `get-pet-by-id`
- **THEN** CSharpMethodName = "GetPetByIdAsync"

#### Scenario: operationId already has Async suffix
- **WHEN** operationId is `listPetsAsync`
- **THEN** CSharpMethodName = "ListPetsAsync" (no double Async suffix)

#### Scenario: Derived method name from path
- **WHEN** operationId is absent and the operation is `DELETE /pets/{petId}/tags/{tagId}`
- **THEN** CSharpMethodName = "DeletePetsByPetIdTagsByTagIdAsync"

#### Scenario: Method name collision within same tag
- **WHEN** two operations in the same tag produce identical CSharpMethodName (e.g., `get_pets` and `getPets` both → `GetPetsAsync`)
- **THEN** the second operation gets a deduplicated name (e.g., `GetPetsAsync2`)
- **THEN** a warning diagnostic with code `AS403` is emitted

### Requirement: Compute CSharpName for parameters

The system SHALL convert parameter names to camelCase for C# method parameter names (C# convention for method parameters).

#### Scenario: snake_case parameter name
- **WHEN** a parameter is named `pet_id`
- **THEN** ApiParameter.CSharpName = "petId" (camelCase for method parameters)

#### Scenario: Path parameter already in camelCase
- **WHEN** a parameter is named `petId`
- **THEN** ApiParameter.CSharpName = "petId"

#### Scenario: Header parameter with dashes
- **WHEN** a parameter is named `X-Request-Id`
- **THEN** ApiParameter.CSharpName = "xRequestId"

### Requirement: Expose schema map from SchemaTransformer

SchemaTransformer.Transform() SHALL return the internal schema map as `IReadOnlyDictionary<OpenApiSchema, ApiSchema>` alongside the ApiSpecification and diagnostics, so that OperationTransformer can resolve $ref schemas to existing ApiSchema instances.

#### Scenario: Schema map contains all component schemas
- **WHEN** a spec has 5 component schemas
- **THEN** the schema map has at least 5 entries mapping OpenApiSchema to ApiSchema

#### Scenario: OperationTransformer receives schema map
- **WHEN** OperationTransformer.Transform is called with the schema map from SchemaTransformer
- **THEN** parameter and response schemas that use $ref are resolved to the same ApiSchema instances in the map

### Requirement: Handle path-level parameters

The system SHALL merge path-level parameters with operation-level parameters. Operation-level parameters override path-level parameters with the same `name` + `in` combination.

#### Scenario: Path-level parameter inherited by operation
- **WHEN** a path `/pets/{petId}` defines parameter `petId` at the path level, and the `GET` operation has no parameters
- **THEN** the GET operation's ApiOperation includes the path-level `petId` parameter

#### Scenario: Operation-level parameter overrides path-level
- **WHEN** a path defines parameter `limit` at the path level, and the `GET` operation also defines `limit` with different schema
- **THEN** the operation-level `limit` is used (path-level is overridden)

### Requirement: Strip leading slash from operation paths

The system SHALL strip the leading `/` from OpenAPI paths when storing them in ApiOperation.Path. Generated URIs MUST be relative to support base address path preservation.

#### Scenario: Root-relative path
- **WHEN** the OpenAPI path is `/pets/{petId}`
- **THEN** ApiOperation.Path = "pets/{petId}"

#### Scenario: Nested path
- **WHEN** the OpenAPI path is `/store/inventory`
- **THEN** ApiOperation.Path = "store/inventory"

### Requirement: Handle array query parameters with explode style

The system SHALL support array-typed query parameters with both `explode: true` (repeated key=value pairs) and `explode: false` (comma-separated values). The system SHALL read the `style` and `explode` fields from each parameter and select the appropriate serialization strategy.

#### Scenario: Array query parameter with default explode
- **WHEN** a query parameter has `type: array` with `items: {type: string}` and no explicit style/explode settings
- **THEN** the parameter is accepted with Style = Form, Explode = true (OpenAPI defaults)

#### Scenario: Array query parameter with explicit explode false
- **WHEN** a query parameter has `type: array` with `style: form, explode: false`
- **THEN** the parameter is accepted with Style = Form, Explode = false
- **THEN** the emitter uses inline comma-join serialization (`string.Join(",", ...)`) for this parameter

### Requirement: OperationTransformer runs independently of SchemaTransformer

OperationTransformer SHALL be a separate class that does not extend or modify SchemaTransformer. It SHALL accept the OpenApiDocument, schema map, and ApiStitchConfig as inputs.

#### Scenario: OperationTransformer signature
- **WHEN** OperationTransformer.Transform is called
- **THEN** it accepts `(OpenApiDocument document, IReadOnlyDictionary<OpenApiSchema, ApiSchema> schemaMap, ApiStitchConfig config)`
- **THEN** it returns `(IReadOnlyList<ApiOperation>, IReadOnlyList<Diagnostic>)`

#### Scenario: OperationTransformer does not modify schemas
- **WHEN** OperationTransformer processes operations
- **THEN** the schema map and existing ApiSchema instances are not modified

#### Scenario: OperationTransformer uses config for client name derivation
- **WHEN** `config.ClientName` is "MyApi"
- **THEN** untagged operations use "MyApi" as the default tag/client name prefix

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

