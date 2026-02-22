## Purpose

Semantic model for the API surface. Transforms OpenAPI component schemas into a language-agnostic representation covering objects, arrays, primitives, enums, allOf composition (flattening and detected inheritance), nullable/required modifiers, circular reference detection, inline schema hoisting, and PascalCase naming.

## Requirements

### Requirement: Transform object schemas to ApiSchema with Kind Object

The system SHALL transform OpenAPI object schemas (type: object with properties) into ApiSchema instances with Kind = Object, populating Properties with an ApiProperty for each declared property.

#### Scenario: Simple object with primitive properties
- **WHEN** a schema has `type: object` with properties `id` (integer/int64), `name` (string), and `active` (boolean)
- **THEN** the resulting ApiSchema has Kind = Object and three ApiProperty entries with correct schemas

#### Scenario: Object with nested object reference
- **WHEN** a schema has a property whose type is a `$ref` to another component schema
- **THEN** the ApiProperty's Schema points to the same ApiSchema instance as the referenced component (reference equality)

#### Scenario: Object with array property
- **WHEN** a schema has a property of `type: array` with `items` referencing another schema
- **THEN** the ApiProperty's Schema has Kind = Array and ArrayItemSchema pointing to the referenced schema

### Requirement: Resolve all $ref pointers during transformation

The system SHALL resolve all `$ref` pointers during the transformation phase. The resulting semantic model SHALL contain no unresolved references — only direct object graph references with shared instances for identity.

#### Scenario: Two properties referencing the same schema
- **WHEN** two different schemas each have a property that `$ref`s the same component schema
- **THEN** both ApiProperty.Schema fields point to the same ApiSchema instance (reference equality)

#### Scenario: Nested $ref chains
- **WHEN** schema A references schema B which references schema C
- **THEN** all references are fully resolved — no ApiSchema contains a reference to resolve

### Requirement: Handle required and nullable property modifiers

The system SHALL map OpenAPI `required` and `nullable` modifiers to ApiProperty.IsRequired and the computed ApiProperty.IsNullable.

#### Scenario: Required non-nullable property
- **WHEN** a property is listed in the parent schema's `required` array and is not marked `nullable: true`
- **THEN** ApiProperty.IsRequired = true and IsNullable = false

#### Scenario: Required nullable property
- **WHEN** a property is listed in `required` and is marked `nullable: true`
- **THEN** ApiProperty.IsRequired = true and IsNullable = true

#### Scenario: Optional non-nullable property
- **WHEN** a property is NOT in the `required` array and is not marked `nullable: true`
- **THEN** ApiProperty.IsRequired = false and IsNullable = true (optional = nullable in C#)

#### Scenario: Optional nullable property
- **WHEN** a property is NOT in `required` and IS marked `nullable: true`
- **THEN** ApiProperty.IsRequired = false and IsNullable = true

### Requirement: Transform enum schemas to ApiSchema with Kind Enum

The system SHALL transform OpenAPI string schemas with `enum` values into ApiSchema instances with Kind = Enum, populating EnumValues with an ApiEnumMember for each value.

#### Scenario: String enum with three values
- **WHEN** a schema has `type: string` and `enum: [active, inactive, pending]`
- **THEN** the resulting ApiSchema has Kind = Enum with three ApiEnumMember entries
- **THEN** each ApiEnumMember.Name contains the original wire value (`active`, `inactive`, `pending`)
- **THEN** each ApiEnumMember.CSharpName contains the PascalCased name (`Active`, `Inactive`, `Pending`)

#### Scenario: Integer enum
- **WHEN** a schema has `type: integer` and `enum` values
- **THEN** the system emits a warning diagnostic with code `AS200` indicating only string enums are supported
- **THEN** the schema is treated as its primitive type (int) rather than as an enum

### Requirement: Flatten allOf composition by default

The system SHALL flatten all `allOf` entries into a single ApiSchema by merging properties from all component schemas and inline schemas.

#### Scenario: allOf with two $ref schemas
- **WHEN** a schema has `allOf: [{$ref: '#/components/schemas/A'}, {$ref: '#/components/schemas/B'}]`
- **THEN** the resulting ApiSchema has Kind = Object with all properties from both A and B merged

#### Scenario: allOf with $ref and inline properties
- **WHEN** a schema has `allOf: [{$ref: '#/components/schemas/Base'}, {type: object, properties: {extra: {type: string}}}]`
- **THEN** the resulting ApiSchema has all properties from Base plus the `extra` property

#### Scenario: Property name conflict in allOf merge
- **WHEN** two allOf entries both define a property with the same name
- **THEN** the system emits a warning diagnostic with code `AS201` and keeps the property from the last entry

### Requirement: Detect inheritance pattern in allOf schemas

After all schemas are transformed, the system SHALL detect the inheritance pattern: an allOf with exactly one `$ref` and one inline schema with additional properties, where the referenced schema is used as a base by two or more schemas. Detected inheritance sets BaseSchema on the derived schema and removes inherited properties.

#### Scenario: Two schemas inheriting from the same base
- **WHEN** schemas `Dog` and `Cat` both have `allOf: [{$ref: '#/components/schemas/Animal'}, {inline props}]`
- **THEN** both Dog and Cat have BaseSchema pointing to Animal
- **THEN** Dog and Cat do NOT include Animal's properties in their own Properties list

#### Scenario: Single use of allOf with $ref — no inheritance
- **WHEN** only one schema has `allOf` referencing `BaseType`
- **THEN** the schema is flattened (BaseSchema remains null) because the base is not reused

### Requirement: Detect and break circular references

The system SHALL detect circular references during transformation using depth-first traversal in alphabetical schema order. When a cycle is detected, the back-edge property SHALL be made optional/nullable to break the cycle.

#### Scenario: Direct circular reference (A → B → A)
- **WHEN** schema A has a property of type B, and schema B has a property of type A
- **THEN** the back-edge property (on the schema visited second alphabetically) has IsRequired set to false
- **THEN** a warning diagnostic with code `AS003` is emitted identifying which property was relaxed

#### Scenario: Self-referencing schema
- **WHEN** schema A has a property of type A (self-reference)
- **THEN** the self-referencing property has IsRequired set to false
- **THEN** a warning diagnostic with code `AS003` is emitted

### Requirement: Hoist inline schemas to top-level types

The system SHALL hoist inline object schemas (schemas defined inline within properties rather than under `components/schemas`) to top-level ApiSchema entries with generated names following the pattern `{ParentType}{PropertyName}`.

#### Scenario: Inline object schema on a property
- **WHEN** a property `address` on schema `User` has an inline `type: object` with properties
- **THEN** a new ApiSchema named `UserAddress` is created in the specification's Schemas list
- **THEN** the property's Schema points to the hoisted `UserAddress` schema

#### Scenario: Generated name collides with existing schema
- **WHEN** the generated name `UserAddress` already exists as a component schema
- **THEN** the hoisted schema is named `UserAddress2` (ordinal suffix)
- **THEN** a warning diagnostic with code `AS202` is emitted

### Requirement: Assign unique PascalCased names to all schemas

The system SHALL convert all OpenAPI schema names to PascalCase for C# type names during transformation. Name collisions SHALL be resolved by appending an ordinal suffix.

#### Scenario: snake_case schema name
- **WHEN** a schema is named `pet_status`
- **THEN** the ApiSchema.Name is `PetStatus` and ApiSchema.OriginalName is `pet_status`

#### Scenario: kebab-case schema name
- **WHEN** a schema is named `pet-status`
- **THEN** the ApiSchema.Name is `PetStatus`

#### Scenario: Name collision after PascalCase conversion
- **WHEN** two schemas `pet_status` and `PetStatus` both convert to `PetStatus`
- **THEN** the first (alphabetically by original name) is `PetStatus` and the second is `PetStatus2`
- **THEN** a warning diagnostic with code `AS203` is emitted

### Requirement: Map OpenAPI primitive types and formats to semantic model primitives

The system SHALL map OpenAPI `type` + `format` combinations to ApiSchema PrimitiveType values according to the type mapping table in the proposal.

#### Scenario: string with date-time format
- **WHEN** a schema has `type: string, format: date-time`
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = DateTimeOffset

#### Scenario: string with uuid format
- **WHEN** a schema has `type: string, format: uuid`
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = Guid

#### Scenario: integer with no format
- **WHEN** a schema has `type: integer` with no format
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = Int32

#### Scenario: string with date format
- **WHEN** a schema has `type: string, format: date`
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = DateOnly

#### Scenario: string with time format
- **WHEN** a schema has `type: string, format: time`
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = TimeOnly

#### Scenario: string with duration format
- **WHEN** a schema has `type: string, format: duration`
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = TimeSpan

#### Scenario: string with uri format
- **WHEN** a schema has `type: string, format: uri`
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = Uri

#### Scenario: string with byte format (base64)
- **WHEN** a schema has `type: string, format: byte`
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = ByteArray

#### Scenario: string with binary format
- **WHEN** a schema has `type: string, format: binary`
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = ByteArray

#### Scenario: string with no format
- **WHEN** a schema has `type: string` with no format
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = String

#### Scenario: integer with int32 format
- **WHEN** a schema has `type: integer, format: int32`
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = Int32

#### Scenario: integer with int64 format
- **WHEN** a schema has `type: integer, format: int64`
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = Int64

#### Scenario: number with float format
- **WHEN** a schema has `type: number, format: float`
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = Float

#### Scenario: number with double format
- **WHEN** a schema has `type: number, format: double`
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = Double

#### Scenario: number with no format
- **WHEN** a schema has `type: number` with no format
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = Double

#### Scenario: number with decimal format
- **WHEN** a schema has `type: number, format: decimal`
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = Decimal

#### Scenario: boolean type
- **WHEN** a schema has `type: boolean`
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = Bool

#### Scenario: string with unknown format
- **WHEN** a schema has `type: string, format: custom-thing`
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = String
- **THEN** a warning diagnostic with code `AS204` is emitted indicating the unknown format was treated as string

### Requirement: Capture deprecation flags from OpenAPI schemas and properties

The system SHALL set IsDeprecated = true on ApiSchema and ApiProperty when the corresponding OpenAPI element has `deprecated: true`.

#### Scenario: Deprecated schema
- **WHEN** a component schema has `deprecated: true`
- **THEN** ApiSchema.IsDeprecated = true

#### Scenario: Deprecated property
- **WHEN** a property within a schema has `deprecated: true`
- **THEN** ApiProperty.IsDeprecated = true

### Requirement: Handle additionalProperties on object schemas

The system SHALL set HasAdditionalProperties = true on ApiSchema when the OpenAPI schema specifies `additionalProperties`. When `additionalProperties` is a schema (not just `true`), the system SHALL set AdditionalPropertiesSchema to the corresponding ApiSchema.

#### Scenario: additionalProperties set to true
- **WHEN** a schema has `additionalProperties: true`
- **THEN** ApiSchema.HasAdditionalProperties = true and AdditionalPropertiesSchema = null

#### Scenario: additionalProperties with typed schema
- **WHEN** a schema has `additionalProperties: { type: string }`
- **THEN** ApiSchema.HasAdditionalProperties = true and AdditionalPropertiesSchema is a Primitive/String ApiSchema

#### Scenario: No additionalProperties specified
- **WHEN** a schema does not specify `additionalProperties`
- **THEN** ApiSchema.HasAdditionalProperties = false

### Requirement: Assign PascalCased names to property and enum member names

The system SHALL convert OpenAPI property names to PascalCase for C# property names and enum member wire values to PascalCase for C# enum member names.

#### Scenario: snake_case property name
- **WHEN** a property is named `first_name`
- **THEN** ApiProperty.CSharpName is `FirstName` and ApiProperty.Name is `first_name`

#### Scenario: camelCase property name
- **WHEN** a property is named `firstName`
- **THEN** ApiProperty.CSharpName is `FirstName`

#### Scenario: kebab-case property name
- **WHEN** a property is named `first-name`
- **THEN** ApiProperty.CSharpName is `FirstName`

### Requirement: Populate description fields from OpenAPI descriptions

The system SHALL populate ApiSchema.Description and ApiProperty.Description from the corresponding OpenAPI `description` fields.

#### Scenario: Schema with description
- **WHEN** a component schema has `description: "A pet in the store"`
- **THEN** ApiSchema.Description is `"A pet in the store"`

#### Scenario: Property with description
- **WHEN** a property has `description: "The pet's name"`
- **THEN** ApiProperty.Description is `"The pet's name"`

#### Scenario: Schema without description
- **WHEN** a component schema has no `description` field
- **THEN** ApiSchema.Description is null

### Requirement: Handle edge case schemas

The system SHALL handle schemas that are empty, have no properties, are top-level primitives, or are top-level arrays.

#### Scenario: Object schema with no properties
- **WHEN** a component schema has `type: object` with no `properties` defined
- **THEN** the ApiSchema has Kind = Object with an empty Properties list
- **THEN** no error or warning diagnostic is emitted

#### Scenario: Primitive type alias (top-level string schema)
- **WHEN** a component schema is `type: string` with no enum values and no properties
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = String

#### Scenario: Top-level array schema
- **WHEN** a component schema has `type: array` with `items` referencing another schema
- **THEN** the ApiSchema has Kind = Array and ArrayItemSchema pointing to the referenced schema

### Requirement: Handle allOf edge cases

The system SHALL handle allOf entries that contain only inline schemas and allOf with $ref where the inline has no additional properties.

#### Scenario: allOf with only inline schemas (no $ref)
- **WHEN** a schema has `allOf` containing two inline object schemas with properties
- **THEN** all properties from both inline schemas are merged into the resulting ApiSchema
- **THEN** no inheritance is detected (inline schemas are never inheritance bases)

#### Scenario: allOf with $ref and empty inline schema
- **WHEN** a schema has `allOf: [{$ref: '#/components/schemas/Base'}, {type: object}]` where the inline has no properties
- **THEN** the schema is flattened with Base's properties (no inheritance detected because there are no additional properties)

### Requirement: Handle circular references with three or more nodes

The system SHALL detect cycles of any length, not just two-node or self-referencing cycles.

#### Scenario: Three-node circular reference (A → B → C → A)
- **WHEN** schema A references B, B references C, and C references A
- **THEN** the back-edge property (on the schema visited last during alphabetical DFS) has IsRequired set to false
- **THEN** a warning diagnostic with code `AS003` is emitted

### Requirement: Stub operation, parameter, and response types in the semantic model

The system SHALL include empty placeholder types (ApiOperation, ApiParameter, ApiResponse) in the semantic model with an Operations collection on ApiSpecification. These types SHALL be unpopulated in this change.

#### Scenario: Semantic model includes empty operations list
- **WHEN** a spec is transformed
- **THEN** ApiSpecification.Operations is an empty IReadOnlyList<ApiOperation> (not null)
