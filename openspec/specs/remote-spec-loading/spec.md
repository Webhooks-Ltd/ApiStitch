# remote-spec-loading Specification

## Purpose
TBD - created by archiving change accept-http-https-spec-urls. Update Purpose after archive.
## Requirements
### Requirement: Load OpenAPI documents from HTTP(S) URLs
The system SHALL load OpenAPI documents from full HTTP(S) URLs in addition to local files.

#### Scenario: Load a valid YAML spec from HTTPS URL
- **WHEN** `spec` is `https://example.test/openapi.yaml` and the endpoint returns a valid OpenAPI YAML document
- **THEN** the system fetches the document successfully
- **THEN** the system parses and returns an `OpenApiDocument` with no error diagnostics

#### Scenario: Load a valid JSON spec from HTTP URL
- **WHEN** `spec` is `http://example.test/openapi.json` and the endpoint returns a valid OpenAPI JSON document
- **THEN** the system fetches the document successfully
- **THEN** the system parses and returns an `OpenApiDocument` with no error diagnostics

### Requirement: Report URL loading failures as diagnostics
The system SHALL surface remote fetch failures as error diagnostics and SHALL stop generation when the spec cannot be fetched.

#### Scenario: Unsupported URI scheme
- **WHEN** `spec` is an absolute URI with unsupported scheme (for example `ftp://example.test/openapi.yaml`)
- **THEN** the system returns an error diagnostic indicating only `http` and `https` are supported

#### Scenario: Non-success HTTP status code
- **WHEN** the remote endpoint returns HTTP 404 or another non-success status
- **THEN** the system returns an error diagnostic containing the URL and status code

#### Scenario: Network exception while fetching remote spec
- **WHEN** the HTTP request fails due to timeout, DNS, or connection errors
- **THEN** the system returns an error diagnostic containing the URL and exception message

### Requirement: Remote fetch enforces bounded network safeguards
The remote spec loader SHALL enforce bounded network behavior for timeout, payload size, and redirects.

#### Scenario: Request exceeds timeout
- **WHEN** a remote spec request exceeds the configured timeout (30 seconds)
- **THEN** the system returns an error diagnostic indicating the fetch timed out

#### Scenario: Remote payload exceeds maximum size
- **WHEN** a remote spec response body exceeds 10 MiB
- **THEN** the system stops reading the response body
- **THEN** the system returns an error diagnostic indicating the response is too large

#### Scenario: Redirect chain exceeds limit
- **WHEN** the remote endpoint requires more than 5 redirects to resolve the spec URL
- **THEN** the system returns an error diagnostic indicating redirect limit exceeded

#### Scenario: Redirect to unsupported scheme
- **WHEN** the remote endpoint redirects to a non-HTTP(S) scheme
- **THEN** the system returns an error diagnostic indicating only `http` and `https` are supported

### Requirement: Remote loading preserves cancellation semantics
The remote spec loader SHALL honor cancellation and SHALL not misreport cancellation as a generic fetch failure.

#### Scenario: Cancellation requested during remote fetch
- **WHEN** cancellation is requested while fetching a remote spec
- **THEN** the operation is canceled
- **THEN** the system does not emit a misleading network-failure diagnostic for the canceled request

