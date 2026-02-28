## Context

ApiStitch currently supports type reuse through `x-apistitch-type`, with external type mapping resolved in parsing and consumed by C# emitters. Recent JSON Patch support exposed a cross-cutting issue: some reused framework types are not compatible with STJ source-generated metadata paths, so emission needed exceptions in model and client code.

The immediate fix works, but part of the detection currently depends on emitted C# type string inspection. That creates language coupling and makes future non-C# emitters harder to implement cleanly.

## Goals / Non-Goals

**Goals:**
- Represent reused external type semantics in the schema model (not by emitted language string patterns).
- Define one compatibility policy that model and client emitters consume consistently.
- Keep current runtime behavior for request, response, multipart, and JsonSerializerContext generation.
- Preserve deterministic output and existing user-facing configuration.

**Non-Goals:**
- Introduce new user configuration knobs for compatibility behavior.
- Add support for additional output languages in this change.
- Redesign ProblemDetails handling in this change.

## Decisions

1. **Add semantic external type classification to `ApiSchema`**
   - Add `ExternalTypeKind` (e.g., `None`, `JsonPatchDocument`) to the model.
   - Classification is set in `ExternalTypeResolver` when `ExternalClrTypeName` is resolved.
   - Rationale: parsing/model owns type identity; emitters consume facts.
   - Alternative considered: keep emitter-side string matching. Rejected as language-coupled and brittle.

2. **Keep compatibility policy centralized in emission, but schema-driven**
   - `JsonSerializationCompatibility` recursively inspects `ApiSchema` graphs (properties, arrays, additionalProperties, inheritance/allOf).
   - For this change, if schema composition with `oneOf`/`anyOf`/`not` cannot be represented in the semantic graph, compatibility SHALL conservatively fall back to runtime JSON APIs.
   - Unsupported source-gen types are identified via `ExternalTypeKind`, not C# type text.
   - Rationale: one policy source for request/response/multipart/context behavior.
   - Alternative considered: duplicate checks in each emitter path. Rejected due to drift risk.

3. **Define explicit compatibility defaults**
   - `ExternalTypeKind.JsonPatchDocument` is unsupported for generated metadata in this change.
   - `ExternalTypeKind.None` is compatible by default.
   - New kinds default to compatible unless explicitly marked unsupported by policy.
   - Rationale: deterministic behavior and clear extension path.

4. **Use capability policy at all JSON serialization call sites**
   - Request JSON body: select `_jsonOptions` vs runtime defaults.
   - Response JSON body: select `_jsonOptions` vs runtime defaults.
   - Multipart JSON-encoded parts: same selection per-part.
   - JsonSerializerContext: exclude schema roots/collections whose graphs contain unsupported source-gen types.
   - Rationale: consistent behavior and predictable AOT/source-gen safety.

5. **Preserve ProblemDetails strategy as-is**
   - Continue existing generated-vs-external ProblemDetails behavior.
   - Rationale: orthogonal concern; avoid scope creep.

## Risks / Trade-offs

- **[Risk] Incomplete classification for future framework types** -> **Mitigation:** keep `ExternalTypeKind` extensible and add targeted tests when adding new kinds.
- **[Risk] Recursive graph policy misses schema links** -> **Mitigation:** retain cycle-safe traversal and cover wrapper/collection/multipart/response scenarios in integration tests.
- **[Trade-off] C# emitters still consume compatibility policy today** -> **Mitigation:** policy input is now language-agnostic model metadata, enabling future emitters to reuse the same semantic contract.

## Migration Plan

1. Add model-level `ExternalTypeKind` and resolver classification.
2. Refactor compatibility policy to use schema semantic flags.
3. Update emitters to consume schema policy everywhere JSON serialization decisions are made.
4. Extend integration/unit tests for direct, wrapped, wrapper-object, and multipart scenarios.
5. Validate deterministic output and existing type-reuse behavior.

Rollback: revert change set; previous behavior is restored without data migration.

## Verification Criteria

- `TypeReuse_` integration suite passes with added cases for direct/wrapped/wrapper-object/multipart.
- No emitter compatibility decisions rely on C# type-name substring matching.
- JsonSerializerContext includes compatible graphs and excludes unsupported graphs consistently.
- Full solution build/tests pass with deterministic output expectations unchanged.

## Phase Gate

- The design intentionally uses `ExternalTypeKind` in Phase 1.
- Promotion to richer capability flags/registry is deferred until either:
  - a second unsupported external type kind is introduced, or
  - non-C# emitter implementation requires finer-grained semantics.
- Phase-2 trigger owner: ApiStitch maintainers.
- Phase-2 checkpoint: next emitter design kickoff or quarterly planning (whichever occurs first after trigger conditions are met).
