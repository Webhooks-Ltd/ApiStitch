## MODIFIED Requirements

### Requirement: Support outputStyle configuration property

The system SHALL support an optional `outputStyle` configuration property in `openapi-stitch.yaml`.

Supported values are:
- `TypedClientStructured` (default)
- `TypedClientFlat`

When omitted, the system SHALL default to `TypedClientStructured`.

#### Scenario: outputStyle set to TypedClientStructured
- **WHEN** config contains `outputStyle: TypedClientStructured`
- **THEN** `ApiStitchConfig.OutputStyle` is set to `OutputStyle.TypedClientStructured`

#### Scenario: outputStyle set to TypedClientFlat
- **WHEN** config contains `outputStyle: TypedClientFlat`
- **THEN** `ApiStitchConfig.OutputStyle` is set to `OutputStyle.TypedClientFlat`

#### Scenario: outputStyle missing
- **WHEN** config omits `outputStyle`
- **THEN** `ApiStitchConfig.OutputStyle` defaults to `OutputStyle.TypedClientStructured`

#### Scenario: outputStyle case-insensitive parsing
- **WHEN** config contains `outputStyle: typedclientflat` (different casing)
- **THEN** value is parsed successfully as `OutputStyle.TypedClientFlat`

#### Scenario: outputStyle invalid value
- **WHEN** config contains `outputStyle: UnknownStyle`
- **THEN** loader returns error diagnostic with code `AS302`
- **THEN** diagnostic message lists supported values including `TypedClientStructured` and `TypedClientFlat`
