## MODIFIED Requirements

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
