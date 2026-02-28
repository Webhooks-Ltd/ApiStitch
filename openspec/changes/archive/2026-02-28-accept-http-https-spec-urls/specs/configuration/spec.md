## MODIFIED Requirements

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
