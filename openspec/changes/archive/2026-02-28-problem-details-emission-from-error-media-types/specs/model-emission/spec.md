## MODIFIED Requirements

### Requirement: Emit a partial JsonSerializerContext for all generated models

When client emission is active, JsonSerializerContext SHALL include ProblemDetails metadata only when ProblemDetails support is signaled by the specification.

#### Scenario: Context omits ProblemDetails without signal
- **WHEN** operations exist but no ProblemDetails support signal is present
- **THEN** JsonSerializerContext does not include ProblemDetails `JsonSerializable` metadata

#### Scenario: Context includes ProblemDetails with signal
- **WHEN** ProblemDetails support is signaled by response media types or schema usage
- **THEN** JsonSerializerContext includes the selected ProblemDetails type metadata (generated or external)
