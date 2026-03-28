# real-spec-regression-testing Specification

## Purpose
Define the repository-local real-world OpenAPI regression corpus and the expected library/CLI behavior used to validate it.
## Requirements
### Requirement: Maintain a curated real-world OpenAPI regression corpus
The system SHALL maintain a curated, repository-local corpus of real-world OpenAPI test fixtures derived from public specifications. Each corpus entry SHALL record enough metadata to identify its upstream source and pinned revision or snapshot provenance.

#### Scenario: Corpus entry records public source metadata
- **WHEN** a new real-world fixture is added to the corpus
- **THEN** the fixture metadata records its upstream source identifier
- **THEN** the fixture metadata records the pinned revision, release, or snapshot provenance used for the checked-in test input

#### Scenario: Test execution does not require network access
- **WHEN** the real-world regression tests are executed in CI or on a developer machine
- **THEN** the tests use repository-local fixtures
- **THEN** the tests do not fetch live public specs over the network

### Requirement: Real-world corpus entries declare expected generator outcomes
Each real-world corpus entry SHALL declare an expected generator outcome so the regression harness can distinguish passing behavior, warning-tolerant behavior, and known controlled failures.

#### Scenario: Success entry
- **WHEN** a corpus entry is classified as `Success`
- **THEN** generation completes without error diagnostics
- **THEN** the emitted output is treated as a passing regression case

#### Scenario: Success-with-warnings entry
- **WHEN** a corpus entry is classified as `SuccessWithWarnings`
- **THEN** generation completes without error diagnostics
- **THEN** warning diagnostics are permitted for that entry
- **THEN** the emitted output is treated as a passing regression case

#### Scenario: Expected diagnostic failure entry
- **WHEN** a corpus entry is classified as `ExpectedDiagnosticFailure`
- **THEN** generation fails with one or more expected diagnostics
- **THEN** the harness treats the entry as passing only when the failure remains controlled and matches the declared expectation

### Requirement: Successful real-world corpus entries compile generated output
The regression harness SHALL compile generated code for corpus entries whose expected outcome is `Success` or `SuccessWithWarnings`.

#### Scenario: Successful entry compiles generated code
- **WHEN** a real-world corpus entry completes generation without error diagnostics
- **THEN** the harness compiles the emitted C# output
- **THEN** the entry passes only when compilation succeeds

### Requirement: Real-world corpus runs are crash-safe
The system SHALL fail real-world corpus runs with structured diagnostics or controlled CLI exit behavior, not with unhandled exceptions or stack traces.

#### Scenario: Library path failure remains controlled
- **WHEN** a real-world corpus entry cannot be generated successfully
- **THEN** the library path reports diagnostics describing the failure
- **THEN** the library path does not terminate with an unhandled exception

#### Scenario: CLI path failure remains controlled
- **WHEN** the CLI is executed against a real-world corpus entry that fails generation
- **THEN** the CLI returns a non-zero exit code
- **THEN** stderr contains a clean error or diagnostic output
- **THEN** stderr does not contain a stack trace

### Requirement: Future field failures can be promoted into permanent regression coverage
The project SHALL support promoting newly discovered real-world failures into durable regression tests through either the pinned corpus or minimized repro fixtures.

#### Scenario: Production-discovered failure is captured for future regression coverage
- **WHEN** a new failure is found against a real-world OpenAPI document
- **THEN** the failure can be added as a new corpus entry or converted into a minimized fixture
- **THEN** the resulting test captures the expected current behavior until the failure is fixed
