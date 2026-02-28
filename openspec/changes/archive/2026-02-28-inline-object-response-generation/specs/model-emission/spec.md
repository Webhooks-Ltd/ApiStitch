## ADDED Requirements

### Requirement: Emit synthetic inline response models

The system SHALL emit generated model files for synthetic schemas created from supported inline success-response object schemas.

#### Scenario: Synthetic inline response model is emitted
- **WHEN** operation parsing creates a synthetic inline response ApiSchema for a supported success response object
- **THEN** model emission writes a corresponding `.cs` model file using the same conventions as other generated object schemas

#### Scenario: Synthetic inline response model is included in JsonSerializerContext
- **WHEN** a synthetic inline response ApiSchema is present in ApiSpecification.Schemas
- **THEN** JsonSerializerContext includes metadata for the synthetic type unless excluded by compatibility rules

#### Scenario: Inline primitive response does not create synthetic model file
- **WHEN** an operation uses a supported inline primitive success-response schema
- **THEN** no synthetic model file is created for that primitive response
- **THEN** client emission uses the mapped primitive type directly
