## 1. Semantic detection and model wiring

- [x] 1.1 Add a single semantic flag for ProblemDetails support based on non-2xx response contract signals
- [x] 1.2 Ensure signal supports explicit ProblemDetails schema usage and external type reuse paths
- [x] 1.3 Thread the semantic flag through client and model emission inputs

## 2. Emitter/template behavior

- [x] 2.1 Update client emitter to skip `ProblemDetails.cs` when no ProblemDetails signal exists
- [x] 2.2 Update `ApiException`/`EnsureSuccessAsync` generation to avoid ProblemDetails references when not signaled
- [x] 2.3 Update model emitter JsonSerializerContext generation to include ProblemDetails metadata only when signaled

## 3. Tests and verification

- [x] 3.1 Add regression test: Petstore-like spec with no ProblemDetails signal does not emit `ProblemDetails.cs`
- [x] 3.2 Add positive test: spec with non-2xx `application/problem+json` emits ProblemDetails support
- [x] 3.3 Add coverage for external ProblemDetails reuse with signaled and non-signaled specs
- [x] 3.4 Run targeted client/model emission tests and fix failures
- [x] 3.5 Run full solution build/tests and confirm deterministic output unchanged

## 4. Documentation updates

- [x] 4.1 Update `README.md` to document spec-signaled ProblemDetails emission/deserialization behavior
- [x] 4.2 Add `CHANGELOG.md` `Unreleased` entry describing the new conditional ProblemDetails behavior
