# error-handling-enrichment Specification

## Purpose
TBD - created by archiving change advanced-http-support. Update Purpose after archive.
## Requirements
### Requirement: Emit ProblemDetails record type

The system SHALL emit a `public sealed record ProblemDetails` with the five core RFC 9457 fields: `string? Type`, `string? Title`, `int? Status`, `string? Detail`, and `string? Instance`. The record SHALL be emitted in the generated namespace with `[GeneratedCode("ApiStitch", null)]`. The type SHALL be a generated record in the output namespace, NOT a reference to `Microsoft.AspNetCore.Mvc.ProblemDetails`.

#### Scenario: ProblemDetails record structure
- **WHEN** `ProblemDetails` is emitted
- **THEN** the type is a `public sealed record ProblemDetails` with non-positional syntax (init properties, not constructor parameters)
- **THEN** each property has a `[JsonPropertyName]` attribute for explicit wire-name mapping (e.g., `[JsonPropertyName("type")] public string? Type { get; init; }`)
- **THEN** the file has `[GeneratedCode("ApiStitch", null)]` on the type
- **THEN** the file has `#nullable enable` at the top

#### Scenario: ProblemDetails has exactly five properties
- **WHEN** `ProblemDetails` is emitted
- **THEN** only the five core RFC 9457 fields are present (no `errors`, `extensions`, or other properties)

#### Scenario: ProblemDetails emitted alongside ApiException
- **WHEN** client emission is active
- **THEN** `ProblemDetails.cs` is emitted in the same namespace as `ApiException.cs`

#### Scenario: No ASP.NET Core dependency
- **WHEN** `ProblemDetails` is emitted
- **THEN** the generated file does NOT reference `Microsoft.AspNetCore.Mvc` or any ASP.NET Core namespace

### Requirement: Add ProblemDetails property to ApiException

The system SHALL add a `public ProblemDetails? Problem { get; }` property to the generated `ApiException` class. The constructor SHALL accept an optional `ProblemDetails? problem = null` parameter.

#### Scenario: ApiException constructor with ProblemDetails
- **WHEN** `ApiException` is emitted
- **THEN** the constructor signature includes `ProblemDetails? problem = null` as the last parameter (before any CancellationToken)
- **THEN** the `Problem` property is set from the constructor parameter

#### Scenario: Problem is null when no ProblemDetails available
- **WHEN** an `ApiException` is created without a `ProblemDetails` argument
- **THEN** `Problem` is null

#### Scenario: Problem populated from error response
- **WHEN** an `ApiException` is created with a deserialized `ProblemDetails` instance
- **THEN** `Problem.Type`, `Problem.Title`, `Problem.Status`, `Problem.Detail`, and `Problem.Instance` are accessible

#### Scenario: ResponseBody still available alongside Problem
- **WHEN** an `ApiException` has both `Problem` and `ResponseBody` set
- **THEN** both properties are independently accessible (the raw body is not discarded when ProblemDetails is parsed)

### Requirement: Attempt ProblemDetails deserialization in EnsureSuccessAsync

The system SHALL attempt ProblemDetails deserialization only when ProblemDetails support is signaled by the specification.

#### Scenario: No signal means no ProblemDetails deserialization path
- **WHEN** a specification has no ProblemDetails support signal
- **THEN** generated `EnsureSuccessAsync` does not include ProblemDetails deserialization logic

#### Scenario: Signal enables ProblemDetails deserialization path
- **WHEN** a specification signals ProblemDetails support
- **THEN** generated `EnsureSuccessAsync` includes ProblemDetails deserialization behavior for qualifying JSON error responses

### Requirement: Fall back gracefully on deserialization failure

The system SHALL silently catch `JsonException` during ProblemDetails deserialization. When deserialization fails, the `ApiException` SHALL still be thrown with the raw `ResponseBody` and `Problem` set to null.

#### Scenario: Malformed JSON body
- **WHEN** a non-2xx response has `Content-Type: application/problem+json` and body `{invalid json}`
- **THEN** the thrown `ApiException.Problem` is null
- **THEN** `ApiException.ResponseBody` = `"{invalid json}"`
- **THEN** no exception other than the `ApiException` propagates to the caller

#### Scenario: Valid JSON but not ProblemDetails shape
- **WHEN** a non-2xx response has `Content-Type: application/json` and body `{"error":"something went wrong"}`
- **THEN** the thrown `ApiException.Problem` MAY be non-null but with all fields null (JSON deserializer creates record with null values for unmatched properties)
- **THEN** `ApiException.ResponseBody` = `"{\"error\":\"something went wrong\"}"`

#### Scenario: Empty body does not attempt deserialization
- **WHEN** a non-2xx response has `Content-Length: 0`
- **THEN** ProblemDetails deserialization is NOT attempted
- **THEN** `ApiException.ResponseBody` is null and `ApiException.Problem` is null

### Requirement: Register ProblemDetails in JsonSerializerContext

The system SHALL add `[JsonSerializable(typeof(ProblemDetails))]` to the generated `JsonSerializerContext` when client emission is active. This ensures ProblemDetails deserialization is AOT/trimming compatible.

#### Scenario: ProblemDetails registered in context
- **WHEN** the API has at least one operation (client emission is active)
- **THEN** the generated `JsonSerializerContext` includes `[JsonSerializable(typeof(ProblemDetails))]`

#### Scenario: Model-only emission does not register ProblemDetails
- **WHEN** the API has no operations (model-only emission, no client code)
- **THEN** the generated `JsonSerializerContext` does NOT include `[JsonSerializable(typeof(ProblemDetails))]`

#### Scenario: ProblemDetails registration is deduplicated
- **WHEN** multiple clients exist in the same namespace
- **THEN** `[JsonSerializable(typeof(ProblemDetails))]` appears exactly once on the shared `JsonSerializerContext`

