## Why

ApiStitch currently emits `ProblemDetails.cs` for any generated client with operations, even when the OpenAPI spec does not advertise `application/problem+json` (or otherwise indicate ProblemDetails usage). This creates unexpected generated files and weakens trust that output is driven by the source contract.

## What Changes

- Change ProblemDetails emission to be spec-signaled instead of operation-count driven.
- Emit ProblemDetails support only when error responses indicate RFC 9457/problem JSON usage (or explicit ProblemDetails schema signal).
- Update client/model emission rules so `ApiException` and JsonSerializerContext stay coherent with conditional ProblemDetails support.
- Add tests covering both positive and negative emission paths.

## Capabilities

### Modified Capabilities
- `client-emission`: Make `ProblemDetails.cs` and related `ApiException` behavior conditional on explicit spec signals.
- `model-emission`: Include ProblemDetails metadata in JsonSerializerContext only when ProblemDetails support is active.
- `error-handling-enrichment`: Align error deserialization behavior with explicit spec signals.

## Impact

- Affected code: operation analysis, client/model emitters, templates, integration tests.
- Breaking surface: potential generated API shape change (`ApiException.Problem` behavior) for specs without ProblemDetails signals.
- Documentation: README and CHANGELOG updates required for behavior change.
