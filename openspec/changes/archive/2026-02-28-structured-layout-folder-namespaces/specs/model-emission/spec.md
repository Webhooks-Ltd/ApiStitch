## ADDED Requirements

### Requirement: Structured model namespaces align with folder roles

When output style is `TypedClientStructured`, model and serialization files SHALL use folder-aligned namespaces.

Required mapping:
- Model files in `Models/` use `{RootNamespace}.Models`
- JsonSerializerContext in `Infrastructure/` uses `{RootNamespace}.Infrastructure`

#### Scenario: Structured model namespace mapping
- **WHEN** object/enum models are emitted in structured mode
- **THEN** generated model namespaces are `{RootNamespace}.Models`

#### Scenario: Structured JsonSerializerContext namespace mapping
- **WHEN** JsonSerializerContext is emitted in structured mode
- **THEN** generated context namespace is `{RootNamespace}.Infrastructure`
- **THEN** context references model types correctly via segmented namespaces

#### Scenario: Flat mode keeps root model/context namespaces
- **WHEN** output style is `TypedClientFlat`
- **THEN** model and JsonSerializerContext files use `{RootNamespace}`
