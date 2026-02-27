## MODIFIED Requirements

### Requirement: Load OpenAPI 3.0 documents from local file paths

The system SHALL load OpenAPI documents from local file paths in both JSON and YAML formats using Microsoft.OpenApi 3.x APIs (`OpenApiDocument.Parse`) and YAML reader settings.

#### Scenario: Load a valid YAML spec
- **WHEN** a valid OpenAPI YAML file path is provided
- **THEN** the system returns a parsed OpenApiDocument with no errors

#### Scenario: Load a valid JSON spec
- **WHEN** a valid OpenAPI JSON file path is provided
- **THEN** the system returns a parsed OpenApiDocument with no errors

### Requirement: Reject unsupported OpenAPI versions

The system SHALL accept OpenAPI 2.0 (Swagger, via upconversion), OpenAPI 3.0, and OpenAPI 3.1 documents supported by Microsoft.OpenApi 3.x.

#### Scenario: OpenAPI 2.0 spec provided
- **WHEN** an OpenAPI 2.0 (Swagger) spec is provided
- **THEN** the system parses the spec successfully and continues generation

#### Scenario: OpenAPI 3.1 spec provided
- **WHEN** an OpenAPI 3.1 spec is provided
- **THEN** the system parses the spec successfully and continues generation
