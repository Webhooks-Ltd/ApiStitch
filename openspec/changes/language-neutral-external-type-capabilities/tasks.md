## 1. Model and parsing foundations

- [ ] 1.1 Add canonical `ExternalTypeKind` classification to `ApiSchema`
- [ ] 1.2 Update `ExternalTypeResolver` to assign classification for reused external types after namespace mapping
- [ ] 1.3 Add/extend unit tests for resolver classification (including `JsonPatchDocument` and non-special external types)

## 2. Compatibility policy refactor

- [ ] 2.1 Refactor `JsonSerializationCompatibility` to use schema semantic classification only (remove type-name string matching)
- [ ] 2.2 Keep recursive, cycle-safe schema graph traversal covering properties, arrays, additionalProperties, and inheritance/allOf
- [ ] 2.3 Add conservative runtime fallback behavior when `oneOf`/`anyOf`/`not` composition cannot be represented in semantic links
- [ ] 2.4 Ensure policy exposes a single schema-based decision used by emitters for generated metadata and runtime JSON fallback

## 3. Emitter integration

- [ ] 3.1 Update `ScribanModelEmitter` to include/exclude JsonSerializerContext types via schema compatibility policy
- [ ] 3.2 Update `ScribanClientEmitter` request-body JSON generation to use compatibility policy
- [ ] 3.3 Update `ScribanClientEmitter` response-body JSON generation to use compatibility policy
- [ ] 3.4 Update multipart JSON-part generation to apply compatibility policy per part
- [ ] 3.5 Verify templates reflect conditional `_jsonOptions` usage for request, response, and multipart JSON paths

## 4. Regression coverage and verification

- [ ] 4.1 Add/adjust integration specs and assertions for direct JSON Patch, wrapped collections, wrapper-object, and multipart JSON-part scenarios
- [ ] 4.2 Add verification that compatibility decisions do not use C# type-name pattern matching in emitter paths
- [ ] 4.3 Add regression coverage for composition fallback (`oneOf`/`anyOf`/`not`) when not represented in semantic links
- [ ] 4.4 Run targeted `TypeReuse_` integration tests and resolve failures
- [ ] 4.5 Run full solution build/tests and confirm deterministic output expectations remain unchanged
- [ ] 4.6 Confirm zero unintended runtime fallback in non-JsonPatch type-reuse fixtures and record result
- [ ] 4.7 Capture baseline and post-change generation/test durations for compatible fixtures and verify <=10% regression threshold
- [ ] 4.8 Capture phase-2 trigger decision owner and review checkpoint in change notes (second unsupported kind or second emitter implementation)
