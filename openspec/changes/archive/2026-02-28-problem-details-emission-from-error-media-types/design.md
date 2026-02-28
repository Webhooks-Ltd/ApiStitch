## Context

Current behavior emits generated `ProblemDetails` whenever at least one operation exists. This is broader than many specs and causes output that appears uncorrelated with declared response contracts.

## Goals / Non-Goals

**Goals**
- Emit ProblemDetails support only when the OpenAPI contract signals it.
- Keep generator behavior deterministic and easy to reason about.
- Preserve existing external-type reuse behavior when `ProblemDetails` is explicitly provided via schema/type reuse.

**Non-Goals**
- Introduce full per-operation typed error models.
- Infer undocumented error contracts from runtime heuristics.

## Decisions

1. **Introduce a single semantic flag for ProblemDetails support**
   - Add a computed semantic flag on `ApiSpecification` (or equivalent emission model): `HasProblemDetailsSupport`.
   - This flag is true when any of the following is true:
     - a non-2xx response advertises `application/problem+json`, or
     - a non-2xx response schema resolves to `ProblemDetails`, or
     - external ProblemDetails type reuse is explicitly configured and referenced by schema.

2. **Conditionally emit ProblemDetails artifacts based on semantic flag**
   - `ProblemDetails.cs` is emitted only when `HasProblemDetailsSupport` is true and no external ProblemDetails type is selected.
   - `JsonSerializerContext` includes ProblemDetails type metadata only when `HasProblemDetailsSupport` is true.

3. **Keep ApiException coherent with conditional support**
   - When `HasProblemDetailsSupport` is false, generated `ApiException` should not require `ProblemDetails` type references.
   - Ensure templates avoid dangling references for specs without ProblemDetails signals.

4. **Conservative backward compatibility path**
   - For specs explicitly advertising problem details, behavior remains unchanged.
   - For specs without signals (e.g., Petstore), ProblemDetails file should not be generated.

## Risks / Trade-offs

- **[Risk] False negatives for undocumented servers that still return problem+json** -> **Mitigation:** document that error parsing follows spec-advertised contracts.
- **[Risk] Template branching complexity** -> **Mitigation:** centralize decision in one semantic flag consumed by all emitters.
- **[Trade-off] Potential generated API surface change** -> **Mitigation:** call out in CHANGELOG and add regression tests.
