## MODIFIED Requirements

### Requirement: Stub operation, parameter, and response types in the semantic model

The system SHALL include fully populated types (ApiOperation, ApiParameter, ApiResponse, ApiRequestBody) in the semantic model. ApiSpecification.Operations SHALL contain parsed operations from the OpenAPI document's paths. ApiSpecification SHALL be a record class to support `with` expressions for immutable updates.

#### Scenario: ApiSpecification is a record class
- **WHEN** a spec is transformed
- **THEN** ApiSpecification is a `record class` supporting `with` expressions
- **THEN** ApiSpecification.Operations is `IReadOnlyList<ApiOperation>` initialized to `[]`

#### Scenario: Operations populated after OperationTransformer runs
- **WHEN** OperationTransformer completes
- **THEN** the pipeline creates a new ApiSpecification instance with Operations populated using `with { Operations = operations }`

#### Scenario: ApiOperation has full properties
- **WHEN** an operation is transformed
- **THEN** ApiOperation has properties: OperationId, Path, HttpMethod (ApiHttpMethod enum), Tag, CSharpMethodName, Parameters, RequestBody, SuccessResponse, IsDeprecated, Description, Diagnostics (mutable `List<Diagnostic>` — deliberately mutable so the transformer can accumulate diagnostics during parsing)

#### Scenario: ApiHttpMethod is a custom enum
- **WHEN** the semantic model defines HTTP methods
- **THEN** it uses `ApiHttpMethod { Get, Post, Put, Delete, Patch, Head, Options }` (not System.Net.Http.HttpMethod)

#### Scenario: ApiParameter has full properties
- **WHEN** a parameter is transformed
- **THEN** ApiParameter has properties: Name, CSharpName, Location (ParameterLocation enum), Schema, IsRequired, Description

#### Scenario: ParameterLocation is an enum
- **WHEN** a parameter location is set
- **THEN** ParameterLocation is `enum { Path, Query, Header }` (cookie is excluded — emits diagnostic)

#### Scenario: ApiRequestBody is a separate type
- **WHEN** a request body is parsed
- **THEN** ApiRequestBody has properties: Schema, IsRequired, ContentType

#### Scenario: ApiResponse has full properties
- **WHEN** a response is parsed
- **THEN** ApiResponse has properties: Schema (nullable), StatusCode, ContentType, HasBody (computed from Schema is not null)

## ADDED Requirements

### Requirement: SchemaTransformer exposes schema map

SchemaTransformer.Transform() SHALL return the internal schema map as `IReadOnlyDictionary<OpenApiSchema, ApiSchema>` alongside the ApiSpecification and diagnostics, enabling OperationTransformer to resolve `$ref` schemas.

#### Scenario: Transform return type includes schema map
- **WHEN** SchemaTransformer.Transform() completes
- **THEN** it returns `(ApiSpecification, IReadOnlyDictionary<OpenApiSchema, ApiSchema>, IReadOnlyList<Diagnostic>)`

#### Scenario: Schema map contains all processed schemas
- **WHEN** a spec with 5 component schemas is transformed
- **THEN** the schema map contains entries for all 5 OpenApiSchema → ApiSchema mappings
