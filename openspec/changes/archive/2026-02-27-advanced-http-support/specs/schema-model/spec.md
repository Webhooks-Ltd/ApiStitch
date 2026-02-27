## MODIFIED Requirements

### Requirement: Map OpenAPI primitive types and formats to semantic model primitives

The system SHALL map OpenAPI `type` + `format` combinations to ApiSchema PrimitiveType values according to the type mapping table in the proposal. When `format: binary` appears inside a multipart or octet-stream content type context, the system SHALL map to `PrimitiveType.Stream` instead of `PrimitiveType.ByteArray`.

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

#### Scenario: string with binary format in JSON context
- **WHEN** a schema has `type: string, format: binary` and appears inside a JSON request body or JSON response
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = ByteArray

#### Scenario: string with binary format in multipart context
- **WHEN** a schema property has `type: string, format: binary` and appears inside a `multipart/form-data` request body
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = Stream

#### Scenario: string with binary format in octet-stream context
- **WHEN** a schema has `type: string, format: binary` and appears as the schema of an `application/octet-stream` request body
- **THEN** the ApiSchema has Kind = Primitive and PrimitiveType = Stream

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

## ADDED Requirements

### Requirement: PrimitiveType enum includes Stream variant

The `PrimitiveType` enum SHALL include a `Stream` variant representing `System.IO.Stream` for binary body parameters in multipart and octet-stream contexts. `CSharpTypeMapper.MapPrimitive` SHALL map `PrimitiveType.Stream` to `"Stream"`. `CSharpTypeMapper.IsValueType` SHALL return `false` for `PrimitiveType.Stream`.

#### Scenario: Stream maps to C# Stream type
- **WHEN** `CSharpTypeMapper.MapPrimitive` is called with `PrimitiveType.Stream`
- **THEN** the result is `"Stream"`

#### Scenario: Stream is not a value type
- **WHEN** `CSharpTypeMapper.IsValueType` is called with `PrimitiveType.Stream`
- **THEN** the result is `false`

#### Scenario: Stream is distinct from ByteArray
- **WHEN** the PrimitiveType enum is defined
- **THEN** `PrimitiveType.Stream` and `PrimitiveType.ByteArray` are separate enum members with different values
