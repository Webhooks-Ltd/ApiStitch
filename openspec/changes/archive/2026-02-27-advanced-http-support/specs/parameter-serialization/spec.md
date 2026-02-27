## ADDED Requirements

### Requirement: Add ParameterStyle enum and Style/Explode properties to ApiParameter

The system SHALL add a `ParameterStyle` enum with values `Form`, `Simple`, `DeepObject`, `PipeDelimited`, `SpaceDelimited`. The system SHALL add `required ParameterStyle Style` and `required bool Explode` properties to `ApiParameter`. The OperationTransformer SHALL read `style` and `explode` from each OpenAPI parameter, applying OpenAPI defaults when not specified.

#### Scenario: Default query parameter style
- **WHEN** a query parameter has no explicit `style` or `explode` specified
- **THEN** `ApiParameter.Style` = `Form` and `ApiParameter.Explode` = `true`

#### Scenario: Explicit form + explode false
- **WHEN** a query parameter has `style: form` and `explode: false`
- **THEN** `ApiParameter.Style` = `Form` and `ApiParameter.Explode` = `false`

#### Scenario: DeepObject style
- **WHEN** a query parameter has `style: deepObject` and `explode: true`
- **THEN** `ApiParameter.Style` = `DeepObject` and `ApiParameter.Explode` = `true`

#### Scenario: Default path parameter style
- **WHEN** a path parameter has no explicit `style` or `explode` specified
- **THEN** `ApiParameter.Style` = `Simple` and `ApiParameter.Explode` = `false`

#### Scenario: Default header parameter style
- **WHEN** a header parameter has no explicit `style` or `explode` specified
- **THEN** `ApiParameter.Style` = `Simple` and `ApiParameter.Explode` = `false`

#### Scenario: Style and Explode are required properties
- **WHEN** `ApiParameter` is constructed
- **THEN** both `Style` and `Explode` must be set explicitly (they are `required` properties with no default values)

### Requirement: Reject unsupported parameter style combinations

The system SHALL emit diagnostic `AS407` (Warning) and skip the parameter for style/explode combinations that are undefined per the OpenAPI spec or not yet supported.

#### Scenario: DeepObject with explode false
- **WHEN** a query parameter has `style: deepObject` and `explode: false`
- **THEN** a warning diagnostic with code `AS407` is emitted
- **THEN** the parameter is skipped (not included in the generated method)

#### Scenario: PipeDelimited style
- **WHEN** a query parameter has `style: pipeDelimited`
- **THEN** a warning diagnostic with code `AS407` is emitted
- **THEN** the parameter is skipped

#### Scenario: SpaceDelimited style
- **WHEN** a query parameter has `style: spaceDelimited`
- **THEN** a warning diagnostic with code `AS407` is emitted
- **THEN** the parameter is skipped

### Requirement: Emit inline form+explode serialization for query parameters

The system SHALL emit inline code for `form` + `explode: true` query parameter serialization. For scalar values, a single key-value pair SHALL be added. For array values, one key-value pair per element SHALL be added with the same key. This is the existing behavior, unchanged.

#### Scenario: Scalar form+explode query parameter
- **WHEN** a scalar query parameter has Style = Form and Explode = true
- **THEN** the generated code adds `queryParams.Add(new KeyValuePair<string, string?>("name", value.ToString()))`

#### Scenario: Array form+explode query parameter
- **WHEN** an array query parameter has Style = Form and Explode = true
- **THEN** the generated code iterates the array and adds one key-value pair per element with the same key

### Requirement: Emit inline form+comma serialization for query parameters

The system SHALL emit inline code for `form` + `explode: false` query parameter serialization. For array values, a single key-value pair SHALL be added with values joined by commas.

#### Scenario: Array form+comma query parameter
- **WHEN** an array query parameter has Style = Form and Explode = false
- **THEN** the generated code adds `queryParams.Add(new KeyValuePair<string, string?>("color", string.Join(",", colors)))` (single pair with comma-joined values)

#### Scenario: Scalar form+comma query parameter
- **WHEN** a scalar query parameter has Style = Form and Explode = false
- **THEN** the generated code uses the same single key-value pair as form+explode (comma-join only applies to arrays)

### Requirement: Emit inline deepObject serialization for query parameters

The system SHALL emit inline code for `deepObject` + `explode: true` query parameter serialization. Each property of the object-typed parameter SHALL produce a separate key-value pair with the key in bracket notation (`name[property]=value`). Null properties SHALL be excluded.

#### Scenario: Object deepObject query parameter
- **WHEN** an object query parameter has Style = DeepObject and Explode = true with properties `status` (string) and `type` (string)
- **THEN** the generated code adds `queryParams.Add(new KeyValuePair<string, string?>("filter[status]", filter.Status))` and `queryParams.Add(new KeyValuePair<string, string?>("filter[type]", filter.Type))`

#### Scenario: Null property excluded
- **WHEN** a deepObject parameter has property `status` = "active" and property `type` = null
- **THEN** only `filter[status]=active` is added (null properties excluded via null-check)

#### Scenario: Null deepObject parameter excluded entirely
- **WHEN** a deepObject parameter value is null (optional parameter)
- **THEN** no key-value pairs are added

### Requirement: Extensibility via partial methods

The system SHALL emit all generated client classes as `partial`, enabling users to add companion partial classes that override or extend parameter serialization behavior. The generated `BuildQueryString` helper SHALL remain `private static` and accept the same `List<KeyValuePair<string, string?>>` input.

#### Scenario: Generated client is partial
- **WHEN** a client implementation class is emitted
- **THEN** it is declared as `internal sealed partial class`

#### Scenario: User extends via partial class
- **WHEN** a user creates a companion partial class file for a generated client
- **THEN** the user can add methods that manipulate query parameters, override behavior, or add custom serialization logic
