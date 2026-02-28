## ADDED Requirements

### Requirement: Create deterministic synthetic models for inline success responses

The system SHALL create deterministic synthetic ApiSchema models for supported inline success-response object schemas so they can be emitted and referenced like component schemas.

#### Scenario: Synthetic type naming is deterministic
- **WHEN** the same OpenAPI spec is processed repeatedly
- **THEN** the same synthetic inline response schema names are produced in stable order

#### Scenario: Synthetic naming seed is stable across runs
- **WHEN** a synthetic response model name is generated
- **THEN** the name is derived from a stable operation identity and status code (not hash/random/time-based values)

#### Scenario: Synthetic name collision is resolved deterministically
- **WHEN** a synthetic inline response schema name collides with an existing schema name
- **THEN** the system applies deterministic collision resolution and still emits a unique generated model type

#### Scenario: Synthetic-to-synthetic collision is resolved deterministically
- **WHEN** two synthetic inline response models would produce the same base type name
- **THEN** deterministic suffixing/collision resolution produces unique names with stable ordering across runs
