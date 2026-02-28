## Why

Structured output currently organizes files into role-based folders, but generated namespaces still use a single root namespace. This creates a mismatch between folder structure and code organization that hurts discoverability and increases friction when navigating generated clients.

## What Changes

- In `TypedClientStructured` mode, generate namespaces that align with folder segments (`Contracts`, `Clients`, `Models`, `Infrastructure`, `Configuration`).
- Keep `TypedClientFlat` behavior unchanged (single root namespace).
- Ensure all generated references (interfaces, implementations, DI extensions, JsonSerializerContext, model usages) remain valid across segmented namespaces.
- Keep deterministic output behavior and file routing unchanged; this change only affects namespace assignment for structured mode.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `output-layouts`: define namespace-per-folder behavior for structured layout.
- `client-emission`: emit client/interface/config/runtime files with folder-aligned namespaces in structured mode.
- `model-emission`: emit models/JsonSerializerContext with folder-aligned namespaces in structured mode.

## Impact

- Affected code: emitter namespace construction, template model inputs, and tests asserting generated namespaces.
- APIs: generated type namespaces change for `TypedClientStructured` output.
- Dependencies: none.
- User impact: structured output becomes internally coherent (paths and namespaces match), improving ergonomics.
