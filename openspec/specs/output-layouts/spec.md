# output-layouts Specification

## Purpose
TBD - created by archiving change configurable-output-layouts. Update Purpose after archive.
## Requirements
### Requirement: Support selectable generated file layouts

The system SHALL support two generated file layouts for TypedClient output: `TypedClientStructured` and `TypedClientFlat`.

#### Scenario: Structured layout selected
- **WHEN** output style is `TypedClientStructured`
- **THEN** generated files are emitted into deterministic subfolders by role (Clients, Contracts, Models, Infrastructure, Configuration)

#### Scenario: Flat layout selected
- **WHEN** output style is `TypedClientFlat`
- **THEN** generated files are emitted in a flat single output folder

### Requirement: Default output layout is structured

The system SHALL default to structured layout when output style is not explicitly configured.

#### Scenario: output style omitted
- **WHEN** config and CLI do not specify output style
- **THEN** generation behaves as `TypedClientStructured`

