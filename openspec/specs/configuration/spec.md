## Purpose

YAML configuration parsing for ApiStitch generation pipeline. Loads settings from `apistitch.yaml` including spec path, output namespace, and output directory. Validates required properties, applies sensible defaults, and is extensible for future configuration options.
## Requirements
### Requirement: Load configuration from a YAML file

The system SHALL load configuration from an `openapi-stitch.yaml` file using YamlDotNet. The configuration file supports three properties for this change: `spec`, `namespace`, and `outputDir`.

The `spec` value SHALL accept either:
- a local file path, or
- a full HTTP(S) URL.

#### Scenario: Valid config with all properties
- **WHEN** an `openapi-stitch.yaml` contains `spec: ./petstore.yaml`, `namespace: MyApi.Models`, `outputDir: ./Generated`
- **THEN** the config is loaded with all three values populated

#### Scenario: Config with only spec path
- **WHEN** an `openapi-stitch.yaml` contains only `spec: ./petstore.yaml`
- **THEN** `namespace` defaults to `ApiStitch.Generated` and `outputDir` defaults to `./Generated`

#### Scenario: Config with HTTPS spec URL
- **WHEN** an `openapi-stitch.yaml` contains `spec: https://example.test/openapi.yaml`
- **THEN** the config is loaded successfully
- **THEN** the `spec` value is preserved for remote loading

#### Scenario: Config file does not exist
- **WHEN** the specified config file path does not exist
- **THEN** the system returns an error diagnostic with code `AS300` and a message containing the file path

#### Scenario: Config with invalid YAML syntax
- **WHEN** the config file contains invalid YAML
- **THEN** the system returns an error diagnostic with code `AS301` with the YAML parse error details

### Requirement: Validate required configuration properties

The system SHALL validate that the `spec` property is present and non-empty. Missing `spec` is an error that halts generation.

#### Scenario: Missing spec property
- **WHEN** the config file exists but does not contain a `spec` property
- **THEN** the system returns an error diagnostic with code `AS302` indicating that `spec` is required

#### Scenario: Empty spec property
- **WHEN** the config file has `spec: ""`
- **THEN** the system returns an error diagnostic with code `AS302` indicating that `spec` must not be empty

### Requirement: Apply sensible defaults for optional properties

The system SHALL apply default values for optional configuration properties when they are not specified.

#### Scenario: Default namespace
- **WHEN** `namespace` is not specified in the config
- **THEN** the default value `ApiStitch.Generated` is used

#### Scenario: Default output directory
- **WHEN** `outputDir` is not specified in the config
- **THEN** the default value `./Generated` is used

### Requirement: Configuration is extensible for future properties

The system SHALL ignore unknown properties in the YAML config file without error, to support forward compatibility as new configuration options are added.

#### Scenario: Config with unknown properties
- **WHEN** the config file contains `spec: ./petstore.yaml` and `typeMappings: { Foo: Bar }` (not yet implemented)
- **THEN** the system loads successfully, ignoring the `typeMappings` property
- **THEN** no warning or error diagnostic is emitted for the unknown property

### Requirement: Pipeline receives config object, not file path

The GenerationPipeline SHALL accept an `ApiStitchConfig` object, not a file path. Config loading is the caller's responsibility, decoupling the pipeline from file system access.

#### Scenario: Pipeline invoked with config object
- **WHEN** the GenerationPipeline is called with a populated ApiStitchConfig
- **THEN** the pipeline uses the config values without accessing the file system for configuration

### Requirement: TypeReuse configuration section in apistitch.yaml
`ApiStitchConfig` SHALL include a `TypeReuse` property of type `TypeReuseConfig` that defaults to an empty configuration (no exclusions). The YAML config file SHALL support a `typeReuse` section with `excludeNamespaces` and `excludeTypes` list properties.

#### Scenario: Config with typeReuse section
- **WHEN** `apistitch.yaml` contains:
  ```yaml
  spec: openapi.json
  typeReuse:
    excludeNamespaces:
      - "Microsoft.AspNetCore.*"
    excludeTypes:
      - "System.String"
  ```
- **THEN** `ApiStitchConfig.TypeReuse.ExcludeNamespaces` contains `["Microsoft.AspNetCore.*"]`
- **THEN** `ApiStitchConfig.TypeReuse.ExcludeTypes` contains `["System.String"]`

#### Scenario: Config without typeReuse section
- **WHEN** `apistitch.yaml` does not contain a `typeReuse` section
- **THEN** `ApiStitchConfig.TypeReuse` is a `TypeReuseConfig` with empty `ExcludeNamespaces` and `ExcludeTypes` lists

#### Scenario: Config with empty typeReuse section
- **WHEN** `apistitch.yaml` contains `typeReuse:` with no sub-properties
- **THEN** `ApiStitchConfig.TypeReuse` is a `TypeReuseConfig` with empty lists

#### Scenario: Config with only excludeNamespaces
- **WHEN** `apistitch.yaml` contains `typeReuse:` with only `excludeNamespaces`
- **THEN** `ExcludeNamespaces` is populated and `ExcludeTypes` is an empty list

### Requirement: Empty entries in exclusion lists are ignored
Empty or whitespace-only entries in `excludeNamespaces` and `excludeTypes` SHALL be ignored during config loading.

#### Scenario: Empty string in excludeNamespaces
- **WHEN** `apistitch.yaml` contains `excludeNamespaces: ["", "System.*"]`
- **THEN** `ExcludeNamespaces` contains only `["System.*"]` (the empty string is filtered out)

#### Scenario: Whitespace string in excludeTypes
- **WHEN** `apistitch.yaml` contains `excludeTypes: ["  ", "SampleApi.Models.Pet"]`
- **THEN** `ExcludeTypes` contains only `["SampleApi.Models.Pet"]`

### Requirement: TypeReuseConfig model
`TypeReuseConfig` SHALL be a class with `List<string> ExcludeNamespaces` (glob patterns for namespaces to exclude from reuse) and `List<string> ExcludeTypes` (exact fully-qualified type names to exclude from reuse). Both SHALL default to empty lists.

#### Scenario: Default TypeReuseConfig
- **WHEN** a `TypeReuseConfig` is created with default values
- **THEN** `ExcludeNamespaces` is an empty list and `ExcludeTypes` is an empty list

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

