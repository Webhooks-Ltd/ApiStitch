## Why

ApiStitch currently needs type-specific exceptions for some framework types (for example JSON Patch) to keep generated clients buildable with source generation. We need a principled, language-neutral way to model external type capabilities so future emitters (TypeScript, Flutter) can reuse the same semantics without C#-specific heuristics.

## What Changes

- Introduce semantic classification for reused external types in the schema model via `ExternalTypeKind` (instead of emitted-language type string parsing).
- Define generator policy for when schema graphs are compatible with generated serialization metadata versus runtime serialization fallbacks.
- Apply the same compatibility policy consistently across request bodies, response bodies, multipart JSON parts, and generated JSON context emission.
- Preserve existing behavior for current C# output while removing coupling to C# type-name pattern checks.
- Add regression coverage for direct, wrapped, wrapper-object, and multipart JSON Patch scenarios.

## Capabilities

### New Capabilities
- `external-type-capabilities`: Classify reused external types with emitter-agnostic semantic type kinds and use those semantics to drive serialization/metadata decisions.

### Modified Capabilities
- `type-reuse-consumer`: Reused external types now carry semantic capability metadata in addition to CLR type mapping.
- `model-emission`: JsonSerializerContext inclusion/exclusion now follows schema capability policy, not language type-string checks.
- `client-emission`: Request/response/multipart JSON serialization path selection now follows schema capability policy consistently.

## Impact

- Affected code: schema model, external type resolver, emission compatibility policy, C# client/model emitters, integration tests.
- APIs: no user-facing config changes required; behavior remains backward compatible.
- Dependencies: none new.
- Future systems: enables adding additional emitters without duplicating C#-specific exception logic.

## Success Criteria

- Zero C# type-name pattern matching remains in compatibility decision paths.
- All `TypeReuse_` integration tests pass, including direct, wrapped, wrapper-object, and multipart JSON Patch scenarios.
- Generated output remains deterministic with no unintended diffs outside this change scope.
- No user-facing configuration or API surface changes are introduced.
- Compatible (non-fallback) type-reuse fixtures show no meaningful performance regression in generation and test execution for this change.

## Delivery Phases

- Phase 1 (this change): implement `ExternalTypeKind` + unified schema compatibility policy for current C# emitters.
- Phase 2 (deferred): consider richer capability flags/registry only when a second incompatible external kind or a second emitter implementation requires it.
