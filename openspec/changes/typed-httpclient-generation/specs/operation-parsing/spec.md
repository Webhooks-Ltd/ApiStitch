## ADDED Requirements

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

The system SHALL parse request body definitions into ApiRequestBody instances, supporting `application/json` content type only.

#### Scenario: JSON request body with $ref schema
- **WHEN** an operation has `requestBody` with `content: application/json` and schema `$ref: '#/components/schemas/Pet'`
- **THEN** ApiOperation.RequestBody is populated with Schema pointing to the Pet ApiSchema, ContentType = "application/json"

#### Scenario: Required request body
- **WHEN** the request body has `required: true`
- **THEN** ApiRequestBody.IsRequired = true

#### Scenario: Optional request body
- **WHEN** the request body has `required: false` or no `required` field
- **THEN** ApiRequestBody.IsRequired = false

#### Scenario: JSON request body with inline array of $ref items
- **WHEN** an operation has `requestBody` with `content: application/json` and schema `type: array, items: {$ref: '#/components/schemas/Pet'}`
- **THEN** ApiRequestBody.Schema has Kind = Array with ArrayItemSchema pointing to the Pet ApiSchema

#### Scenario: Non-JSON content type
- **WHEN** the request body content type is `application/xml`, `multipart/form-data`, or `application/x-www-form-urlencoded`
- **THEN** the request body is skipped
- **THEN** a warning diagnostic with code `AS404` is emitted indicating unsupported content type

#### Scenario: Inline complex schema in required request body
- **WHEN** the request body schema is an inline object (not a $ref) and `required: true`
- **THEN** the operation is skipped (method not generated)
- **THEN** a warning diagnostic with code `AS401` is emitted

#### Scenario: Inline complex schema in optional request body
- **WHEN** the request body schema is an inline object (not a $ref) and `required: false`
- **THEN** the operation is skipped (method not generated) — MMVP does not partially emit methods with unsupported bodies
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

#### Scenario: No 2xx response defined
- **WHEN** an operation has no 2xx response codes
- **THEN** ApiOperation.SuccessResponse is null (method returns Task, not Task<T>)

#### Scenario: Inline complex schema in response
- **WHEN** the response schema is an inline object (not a $ref, not an array of $ref, not a primitive)
- **THEN** the operation is skipped
- **THEN** a warning diagnostic with code `AS401` is emitted

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

The system SHALL support `explode: true` (the OpenAPI default for query parameters) for array-typed query parameters, producing repeated key=value pairs.

#### Scenario: Array query parameter with default explode
- **WHEN** a query parameter has `type: array` with `items: {type: string}` and no explicit style/explode settings
- **THEN** the parameter is accepted (explode: true is the default)

#### Scenario: Array query parameter with explicit non-explode style
- **WHEN** a query parameter has `style: form, explode: false`
- **THEN** the parameter is skipped
- **THEN** a warning diagnostic with code `AS405` is emitted indicating unsupported query parameter style

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
