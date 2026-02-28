## Context

`TypedClientStructured` currently places generated files into role-based folders (`Contracts`, `Clients`, `Models`, `Infrastructure`, `Configuration`) but still emits a single root namespace for most files. This creates mismatched mental models and awkward symbol discovery because folder hierarchy and namespace hierarchy diverge.

The requested behavior is explicit: when output style is structured, folder-to-namespace alignment should be the only behavior. Flat output remains root-namespace behavior.

## Goals / Non-Goals

**Goals:**
- In `TypedClientStructured`, emit namespaces that match folder role suffixes.
- Keep `TypedClientFlat` namespace behavior unchanged (single configured namespace).
- Ensure all generated cross-file references remain valid after namespace segmentation.
- Keep deterministic output ordering and naming.

**Non-Goals:**
- Introduce additional output styles.
- Add user-configurable namespace templates/patterns.
- Rename generated type identifiers; only namespace placement changes.

## Decisions

1. **Folder role determines namespace suffix in structured mode**
   - `Contracts/*` -> `{Root}.Contracts`
   - `Clients/*` -> `{Root}.Clients`
   - `Models/*` -> `{Root}.Models`
   - `Infrastructure/*` -> `{Root}.Infrastructure`
   - `Configuration/*` -> `{Root}.Configuration`
   - Flat mode remains `{Root}`.

2. **Apply namespace segmentation in emitter model-building, not path rewriting**
   - Keep file routing logic unchanged from configurable-output-layouts.
   - Compute namespace per emitted file in `ScribanClientEmitter`/`ScribanModelEmitter` based on output style + role.
   - Rationale: minimal impact and deterministic behavior.

3. **Cross-role references use explicit namespaces in generated templates/models**
   - Client implementations reference contracts/models/infrastructure/configuration namespaces explicitly.
   - DI extensions reference segmented interface/implementation namespaces.
   - Json context and options references remain valid via explicit namespace mapping.

4. **No compatibility fallback inside structured mode**
   - In structured mode there is no "single-root namespace" branch.
   - This enforces consistency and avoids mixed namespace outputs.

## Risks / Trade-offs

- **[Risk] Missing namespace qualification causes compile failures** -> **Mitigation:** add focused generation tests and full-solution tests.
- **[Risk] Large snapshot churn in tests** -> **Mitigation:** centralize expected namespace helpers and keep deterministic path+namespace assertions.
- **[Trade-off] More explicit namespace imports in generated files** -> **Mitigation:** acceptable for clarity and alignment with folder structure.
