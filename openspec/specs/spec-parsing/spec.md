## Purpose

Loading and validating OpenAPI 3.0 documents from local file paths via Microsoft.OpenApi. Supports JSON and YAML formats, reports parser warnings as diagnostics, rejects unsupported OpenAPI versions, and extracts component schemas for downstream transformation.
## Requirements
### Requirement: Load OpenAPI 3.0 documents from local file paths

The system SHALL load OpenAPI documents from local file paths in both JSON and YAML formats using Microsoft.OpenApi 3.x APIs (`OpenApiDocument.Parse`) and YAML reader settings.

#### Scenario: Load a valid YAML spec
- **WHEN** a valid OpenAPI YAML file path is provided
- **THEN** the system returns a parsed OpenApiDocument with no errors

#### Scenario: Load a valid JSON spec
- **WHEN** a valid OpenAPI JSON file path is provided
- **THEN** the system returns a parsed OpenApiDocument with no errors

### Requirement: Report parser warnings as diagnostics

The system SHALL convert Microsoft.OpenApi parser warnings into ApiStitch diagnostics without halting generation.

#### Scenario: Spec with parser warnings
- **WHEN** a valid OpenAPI 3.0 spec with non-fatal parser warnings is loaded
- **THEN** the system returns the parsed document AND warning diagnostics for each parser warning
- **THEN** generation continues (warnings do not halt the pipeline)

### Requirement: Reject unsupported OpenAPI versions

The system SHALL accept OpenAPI 2.0 (Swagger, via upconversion), OpenAPI 3.0, and OpenAPI 3.1 documents supported by Microsoft.OpenApi 3.x.

#### Scenario: OpenAPI 2.0 spec provided
- **WHEN** an OpenAPI 2.0 (Swagger) spec is provided
- **THEN** the system parses the spec successfully and continues generation

#### Scenario: OpenAPI 3.1 spec provided
- **WHEN** an OpenAPI 3.1 spec is provided
- **THEN** the system parses the spec successfully and continues generation

### Requirement: Extract component schemas from parsed documents

The system SHALL extract all schemas from `#/components/schemas` in the parsed OpenAPI document and pass them to the schema transformer.

#### Scenario: Spec with component schemas
- **WHEN** a spec with 5 schemas under `components/schemas` is loaded
- **THEN** the system passes all 5 schemas to the transformer

#### Scenario: Spec with no component schemas
- **WHEN** a spec with no `components/schemas` section is loaded
- **THEN** the system passes an empty collection to the transformer and emits a warning diagnostic with code `AS103`

