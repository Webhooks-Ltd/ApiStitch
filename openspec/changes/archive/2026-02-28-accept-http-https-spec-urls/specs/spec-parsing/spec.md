## MODIFIED Requirements

### Requirement: Load OpenAPI 3.0 documents from local file paths

The system SHALL load OpenAPI documents from local file paths AND full HTTP(S) URLs in both JSON and YAML formats using Microsoft.OpenApi 3.x APIs (`OpenApiDocument.Parse`) and YAML reader settings.

#### Scenario: Load a valid YAML spec
- **WHEN** a valid OpenAPI YAML local file path is provided
- **THEN** the system returns a parsed OpenApiDocument with no errors

#### Scenario: Load a valid JSON spec
- **WHEN** a valid OpenAPI JSON local file path is provided
- **THEN** the system returns a parsed OpenApiDocument with no errors

#### Scenario: Load a valid YAML spec from HTTPS URL
- **WHEN** a valid OpenAPI YAML document is provided via HTTPS URL
- **THEN** the system returns a parsed OpenApiDocument with no errors

#### Scenario: Load a valid JSON spec from HTTPS URL
- **WHEN** a valid OpenAPI JSON document is provided via HTTPS URL
- **THEN** the system returns a parsed OpenApiDocument with no errors

#### Scenario: Windows absolute path is treated as local path
- **WHEN** a Windows absolute path such as `C:\\specs\\petstore.yaml` is provided
- **THEN** the system treats the value as a local filesystem path
- **THEN** the system does not classify it as an unsupported URI scheme
