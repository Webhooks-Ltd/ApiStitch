## MODIFIED Requirements

### Requirement: Emit ProblemDetails record type

The system SHALL conditionally emit a `public sealed record ProblemDetails` only when the OpenAPI contract signals ProblemDetails support. Contract signals include non-2xx `application/problem+json` responses or explicit ProblemDetails schema usage.

#### Scenario: ProblemDetails omitted when no spec signal
- **WHEN** a specification has operations but no non-2xx `application/problem+json` responses and no explicit ProblemDetails schema usage
- **THEN** no `ProblemDetails.cs` file is emitted

#### Scenario: ProblemDetails emitted when problem+json exists
- **WHEN** any non-2xx response advertises `application/problem+json`
- **THEN** `ProblemDetails.cs` is emitted unless an external ProblemDetails type is selected

### Requirement: Emit ApiException class

`ApiException` generation SHALL remain coherent with conditional ProblemDetails support.

#### Scenario: ApiException without ProblemDetails signal
- **WHEN** ProblemDetails support is not signaled by the specification
- **THEN** generated `ApiException` does not require a generated `ProblemDetails` type reference

#### Scenario: ApiException with ProblemDetails signal
- **WHEN** ProblemDetails support is signaled
- **THEN** generated `ApiException` preserves ProblemDetails attachment behavior for qualifying error responses
