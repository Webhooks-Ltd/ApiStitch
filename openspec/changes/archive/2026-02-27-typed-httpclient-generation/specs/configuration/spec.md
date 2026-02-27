## ADDED Requirements

### Requirement: Support outputStyle configuration property

The system SHALL parse an `outputStyle` property from the YAML config file. The value SHALL map to an `OutputStyle` enum. The default value SHALL be `OutputStyle.TypedClient`.

#### Scenario: Config with outputStyle set to TypedClient
- **WHEN** the config contains `outputStyle: TypedClient`
- **THEN** ApiStitchConfig.OutputStyle = OutputStyle.TypedClient

#### Scenario: Config with no outputStyle
- **WHEN** the config does not contain an `outputStyle` property
- **THEN** ApiStitchConfig.OutputStyle defaults to OutputStyle.TypedClient

#### Scenario: Config with outputStyle is case-insensitive
- **WHEN** the config contains `outputStyle: typedclient` (lowercase)
- **THEN** ApiStitchConfig.OutputStyle = OutputStyle.TypedClient (case-insensitive parsing)

#### Scenario: Config with unknown outputStyle value (forward-looking validation)
- **WHEN** the config contains `outputStyle: SomeUnknownStyle`
- **THEN** the system returns an error diagnostic indicating the unrecognized output style
- **THEN** the diagnostic lists the valid values (currently only `TypedClient`; `ExtensionMethods` and `Refit` will be added in future changes)

### Requirement: Support clientName configuration property

The system SHALL parse an optional `clientName` property from the YAML config file. When absent, the client name SHALL be derived from the OpenAPI spec's `info.title`.

#### Scenario: Config with explicit clientName
- **WHEN** the config contains `clientName: PetStoreApi`
- **THEN** ApiStitchConfig.ClientName = "PetStoreApi"

#### Scenario: Config with no clientName
- **WHEN** the config does not contain a `clientName` property
- **THEN** ApiStitchConfig.ClientName is null (pipeline derives from spec title at runtime)

#### Scenario: Client name derivation from spec title
- **WHEN** ClientName is null and the spec has `info.title: "Pet Store API"`
- **THEN** the pipeline derives the client name as "PetStoreApi" (PascalCased, "Api" suffix if not present)

#### Scenario: Spec title already ends with Api
- **WHEN** ClientName is null and the spec has `info.title: "PetStoreApi"`
- **THEN** the derived client name is "PetStoreApi" (no double "Api" suffix)

#### Scenario: Spec title with special characters
- **WHEN** ClientName is null and the spec has `info.title: "My API (v2)"`
- **THEN** the derived client name is "MyApiV2Api" (non-alphanumeric characters stripped, PascalCased, "Api" suffix added)

#### Scenario: Spec title is empty or missing
- **WHEN** ClientName is null and the spec has no `info.title` or an empty title
- **THEN** the system returns a warning diagnostic and falls back to client name "ApiClient"
