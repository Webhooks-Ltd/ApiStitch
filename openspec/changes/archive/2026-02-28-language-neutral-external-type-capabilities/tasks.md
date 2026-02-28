## 1. Model and parsing foundations

- [x] 1.1 Add canonical `ExternalTypeKind` classification to `ApiSchema`
- [x] 1.2 Update `ExternalTypeResolver` to assign classification for reused external types after namespace mapping
- [x] 1.3 Add/extend unit tests for resolver classification (including `JsonPatchDocument` and non-special external types)

## 2. Compatibility policy refactor

- [x] 2.1 Refactor `JsonSerializationCompatibility` to use schema semantic classification only (remove type-name string matching)
- [x] 2.2 Keep recursive, cycle-safe schema graph traversal covering properties, arrays, additionalProperties, and inheritance/allOf
- [x] 2.3 Add conservative runtime fallback behavior when `oneOf`/`anyOf`/`not` composition cannot be represented in semantic links
- [x] 2.4 Ensure policy exposes a single schema-based decision used by emitters for generated metadata and runtime JSON fallback

## 3. Emitter integration

- [x] 3.1 Update `ScribanModelEmitter` to include/exclude JsonSerializerContext types via schema compatibility policy
- [x] 3.2 Update `ScribanClientEmitter` request-body JSON generation to use compatibility policy
- [x] 3.3 Update `ScribanClientEmitter` response-body JSON generation to use compatibility policy
- [x] 3.4 Update multipart JSON-part generation to apply compatibility policy per part
- [x] 3.5 Verify templates reflect conditional `_jsonOptions` usage for request, response, and multipart JSON paths

## 4. Regression coverage and verification

- [x] 4.1 Add/adjust integration specs and assertions for direct JSON Patch, wrapped collections, wrapper-object, and multipart JSON-part scenarios
- [x] 4.2 Add verification that compatibility decisions do not use C# type-name pattern matching in emitter paths
- [x] 4.3 Add regression coverage for composition fallback (`oneOf`/`anyOf`/`not`) when not represented in semantic links
- [x] 4.4 Run targeted `TypeReuse_` integration tests and resolve failures
- [x] 4.5 Run full solution build/tests and confirm deterministic output expectations remain unchanged
- [x] 4.6 Confirm zero unintended runtime fallback in non-JsonPatch type-reuse fixtures and record result
- [x] 4.7 Capture baseline and post-change generation/test durations for compatible fixtures and verify <=10% regression threshold
- [x] 4.8 Capture phase-2 trigger decision owner and review checkpoint in change notes (second unsupported kind or second emitter implementation)

### Verification Notes

- 4.6: Verified non-JsonPatch decoy reuse (`SampleApi.Models.JsonPatchDocumentWrapper`) uses generated metadata path (`_jsonOptions`) for both request and response.
- 4.7: Compatible fixture timing (no-build, two-test filter): baseline 1344.14 ms, post-change 1424.16 ms (+5.95%), within <=10% threshold.
- 4.8: Phase-2 trigger owner/checkpoint documented in `design.md` under "Phase Gate" (owner: ApiStitch maintainers; checkpoint: next emitter design kickoff or quarterly planning).
