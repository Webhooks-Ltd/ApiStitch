## MODIFIED Requirements

### Requirement: Support selectable generated file layouts

The system SHALL support two generated file layouts for TypedClient output: `TypedClientStructured` and `TypedClientFlat`.

When `TypedClientStructured` is selected, generated namespaces SHALL align with folder roles:
- `Contracts/*` -> `{RootNamespace}.Contracts`
- `Clients/*` -> `{RootNamespace}.Clients`
- `Models/*` -> `{RootNamespace}.Models`
- `Infrastructure/*` -> `{RootNamespace}.Infrastructure`
- `Configuration/*` -> `{RootNamespace}.Configuration`

When `TypedClientFlat` is selected, generated namespaces SHALL remain `{RootNamespace}`.

#### Scenario: Structured layout selected
- **WHEN** output style is `TypedClientStructured`
- **THEN** generated files are emitted into deterministic subfolders by role (Clients, Contracts, Models, Infrastructure, Configuration)
- **THEN** each generated file namespace matches the corresponding folder role namespace

#### Scenario: Flat layout selected
- **WHEN** output style is `TypedClientFlat`
- **THEN** generated files are emitted in a flat single output folder
- **THEN** generated files use the root namespace without role suffixes
