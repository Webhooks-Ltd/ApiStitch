## ADDED Requirements

### Requirement: ApiSchema includes VendorTypeHint field
`ApiSchema` SHALL include a `string? VendorTypeHint` property that stores the raw value of the `x-apistitch-type` vendor extension read during schema transformation. This field SHALL be set by `SchemaTransformer` and consumed by `ExternalTypeResolver`.

#### Scenario: VendorTypeHint populated from extension
- **WHEN** a component schema has `"x-apistitch-type": "SampleApi.Models.Pet"`
- **THEN** `ApiSchema.VendorTypeHint` is `"SampleApi.Models.Pet"`

#### Scenario: VendorTypeHint null when no extension
- **WHEN** a component schema has no `x-apistitch-type` extension
- **THEN** `ApiSchema.VendorTypeHint` is `null`

### Requirement: ApiSchema includes ExternalClrTypeName field and computed IsExternal
`ApiSchema` SHALL include a `string? ExternalClrTypeName` property set by `ExternalTypeResolver` after applying exclusion logic and nested type normalisation. `ApiSchema` SHALL include a computed `bool IsExternal` property that returns `ExternalClrTypeName is not null`.

#### Scenario: ExternalClrTypeName set after resolution
- **WHEN** `ExternalTypeResolver` resolves a vendor type hint of `"SampleApi.Models.Pet"` with no exclusions
- **THEN** `ApiSchema.ExternalClrTypeName` is `"SampleApi.Models.Pet"` and `IsExternal` is `true`

#### Scenario: ExternalClrTypeName null when excluded
- **WHEN** `ExternalTypeResolver` finds a vendor type hint that is excluded by configuration
- **THEN** `ApiSchema.ExternalClrTypeName` remains `null` and `IsExternal` is `false`

#### Scenario: ExternalClrTypeName null when no vendor hint
- **WHEN** a schema has no `VendorTypeHint`
- **THEN** `ApiSchema.ExternalClrTypeName` is `null` and `IsExternal` is `false`

### Requirement: Setting external fields does not change structural properties
Setting `ExternalClrTypeName` and `IsExternal` on an `ApiSchema` SHALL NOT change the schema's `Kind`, `Properties`, `EnumValues`, `BaseSchema`, or any other structural field. The schema retains its full semantic model structure.

#### Scenario: External object schema retains Kind and Properties
- **WHEN** an object schema with 3 properties is marked as external
- **THEN** `Kind` remains `SchemaKind.Object` and `Properties` still contains all 3 properties

#### Scenario: External enum schema retains Kind and EnumValues
- **WHEN** an enum schema with members `Active`, `Inactive`, `Pending` is marked as external
- **THEN** `Kind` remains `SchemaKind.Enum` and `EnumValues` still contains all 3 members
