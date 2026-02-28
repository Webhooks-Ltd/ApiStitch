## Context

ApiStitch currently emits all generated client files into a single flat output folder. This makes small APIs simple but creates noisy directories for larger specs where interfaces, implementations, models, and infrastructure files are intermingled.

The user requirement is to support multiple output layouts while making a structured layout the default (no compatibility constraints to preserve old default behavior).

## Goals / Non-Goals

**Goals:**
- Introduce selectable output layouts (flat and structured) for TypedClient output.
- Make structured layout the default for both config and CLI when not explicitly set.
- Keep deterministic output paths for stable diffs.
- Preserve generated type content and runtime behavior; only file placement should vary by layout mode.

**Non-Goals:**
- Introduce new emission styles beyond TypedClient.
- Change namespaces by folder location (namespace remains config-driven).
- Rework model/client template logic beyond path routing.

## Decisions

1. **Add explicit layout values under `OutputStyle`**
   - Add `TypedClientStructured` and retain `TypedClientFlat`.
   - Set default to `TypedClientStructured`.
   - Rationale: keeps selection in existing config/CLI concept without introducing a separate option.
   - Alternative considered: separate `outputLayout` option. Rejected for now to avoid option proliferation and precedence complexity.

2. **Define deterministic structured folder map**
   - `Clients/`: concrete tag client implementations (`{Api}{Tag}Client.cs`) and `FileResponse.cs`.
   - `Contracts/`: generated interfaces (`I{Api}{Tag}Client.cs`).
   - `Models/`: generated non-infrastructure model records/classes/enums.
   - `Infrastructure/`: `ApiException.cs`, `ProblemDetails.cs`, generated JsonSerializerContext.
   - `Configuration/`: `{Api}ClientOptions.cs`, `{Api}JsonOptions.cs`, `{Api}ServiceCollectionExtensions.cs`, enum query extensions.
   - Rationale: predictable discoverability while keeping folder count small.

3. **Centralize path mapping in emitters, not templates**
   - Keep templates unchanged where possible; compute `RelativePath` in emitters via a small layout mapper helper.
   - Rationale: avoids templating complexity and keeps deterministic path logic testable.

4. **CLI/config parsing treats `outputStyle` case-insensitively and validates known values**
   - Unknown values remain an error with valid-options list.
   - Rationale: maintain current UX contract and diagnostics behavior.

## Risks / Trade-offs

- **[Risk] Existing downstream scripts expect flat file paths** → **Mitigation:** keep explicit `TypedClientFlat` opt-in and document migration.
- **[Risk] Misrouted files in structured mode** → **Mitigation:** add inventory tests for both styles with explicit expected paths.
- **[Trade-off] Overloading `outputStyle` for layout concerns** → **Mitigation:** acceptable for current single emission style; revisit if additional output styles ship.
