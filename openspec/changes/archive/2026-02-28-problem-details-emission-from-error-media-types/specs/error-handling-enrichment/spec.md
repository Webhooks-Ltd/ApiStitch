## MODIFIED Requirements

### Requirement: Attempt ProblemDetails deserialization in EnsureSuccessAsync

The system SHALL attempt ProblemDetails deserialization only when ProblemDetails support is signaled by the specification.

#### Scenario: No signal means no ProblemDetails deserialization path
- **WHEN** a specification has no ProblemDetails support signal
- **THEN** generated `EnsureSuccessAsync` does not include ProblemDetails deserialization logic

#### Scenario: Signal enables ProblemDetails deserialization path
- **WHEN** a specification signals ProblemDetails support
- **THEN** generated `EnsureSuccessAsync` includes ProblemDetails deserialization behavior for qualifying JSON error responses
