## Purpose

YAML configuration parsing for ApiStitch generation pipeline. Loads settings from `apistitch.yaml` including spec path, output namespace, and output directory. Validates required properties, applies sensible defaults, and is extensible for future configuration options.

## Requirements

### Requirement: Load configuration from a YAML file

The system SHALL load configuration from an `apistitch.yaml` file using YamlDotNet. The configuration file supports three properties for this change: `spec`, `namespace`, and `outputDir`.

#### Scenario: Valid config with all properties
- **WHEN** an `apistitch.yaml` contains `spec: ./petstore.yaml`, `namespace: MyApi.Models`, `outputDir: ./Generated`
- **THEN** the config is loaded with all three values populated

#### Scenario: Config with only spec path
- **WHEN** an `apistitch.yaml` contains only `spec: ./petstore.yaml`
- **THEN** `namespace` defaults to `ApiStitch.Generated` and `outputDir` defaults to `./Generated`

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
