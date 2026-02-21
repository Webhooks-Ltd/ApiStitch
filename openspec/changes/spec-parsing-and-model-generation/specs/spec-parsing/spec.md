## ADDED Requirements

### Requirement: Load OpenAPI 3.0 documents from local file paths

The system SHALL load OpenAPI 3.0 documents from local file paths in both JSON and YAML formats using Microsoft.OpenApi.Readers.

#### Scenario: Load a valid YAML spec
- **WHEN** a valid OpenAPI 3.0 YAML file path is provided
- **THEN** the system returns a parsed OpenApiDocument with no errors

#### Scenario: Load a valid JSON spec
- **WHEN** a valid OpenAPI 3.0 JSON file path is provided
- **THEN** the system returns a parsed OpenApiDocument with no errors

#### Scenario: File does not exist
- **WHEN** a file path that does not exist is provided
- **THEN** the system returns an error diagnostic with code `AS100` and a message containing the file path

#### Scenario: File is not valid OpenAPI
- **WHEN** a file containing invalid YAML/JSON or non-OpenAPI content is provided
- **THEN** the system returns error diagnostics from the Microsoft.OpenApi parser

### Requirement: Report parser warnings as diagnostics

The system SHALL convert Microsoft.OpenApi parser warnings into ApiStitch diagnostics without halting generation.

#### Scenario: Spec with parser warnings
- **WHEN** a valid OpenAPI 3.0 spec with non-fatal parser warnings is loaded
- **THEN** the system returns the parsed document AND warning diagnostics for each parser warning
- **THEN** generation continues (warnings do not halt the pipeline)

### Requirement: Reject unsupported OpenAPI versions

The system SHALL reject OpenAPI 2.0 (Swagger) and OpenAPI 3.1 documents with a clear error diagnostic.

#### Scenario: OpenAPI 2.0 spec provided
- **WHEN** an OpenAPI 2.0 (Swagger) spec is provided
- **THEN** the system returns an error diagnostic with code `AS101` and a message indicating OpenAPI 2.0 is not supported in this version

#### Scenario: OpenAPI 3.1 spec provided
- **WHEN** an OpenAPI 3.1 spec is provided
- **THEN** the system returns an error diagnostic with code `AS102` and a message indicating OpenAPI 3.1 is not yet supported

### Requirement: Extract component schemas from parsed documents

The system SHALL extract all schemas from `#/components/schemas` in the parsed OpenAPI document and pass them to the schema transformer.

#### Scenario: Spec with component schemas
- **WHEN** a spec with 5 schemas under `components/schemas` is loaded
- **THEN** the system passes all 5 schemas to the transformer

#### Scenario: Spec with no component schemas
- **WHEN** a spec with no `components/schemas` section is loaded
- **THEN** the system passes an empty collection to the transformer and emits a warning diagnostic with code `AS103`
