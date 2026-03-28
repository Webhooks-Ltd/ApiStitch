## Context

ApiStitch already has broad unit coverage and targeted integration coverage for curated specs, but those tests mostly prove known happy paths and hand-picked edge cases. The recurring failures we care about appear when the full CLI/generation pipeline encounters large, vendor-authored documents with deep composition, inline schemas, naming collisions, mixed media types, and parser quirks.

The testing change needs to improve confidence without making the suite flaky. That means the corpus must be deterministic, repo-local, and explicit about what success looks like for each specimen. The goal is not to prove every public API works forever; it is to permanently capture the classes of real-world failures we have already seen and the ones we are most likely to see next.

The initial seed corpus will use pinned snapshots from well-known public OpenAPI sources chosen for size and schema diversity:
- GitHub REST API description
- Stripe OpenAPI
- DigitalOcean public API
- Microsoft Graph v1.0 OpenAPI metadata
- One optional stress specimen reserved for a Kubernetes release snapshot if the repo and CI footprint remain acceptable

## Goals / Non-Goals

**Goals:**
- Add a deterministic real-world corpus that runs in CI and locally without network access.
- Exercise both the library pipeline and the CLI against the same corpus inputs.
- Allow each corpus entry to declare its expected outcome: success, success-with-warnings, or expected-diagnostic-failure.
- Require crash-safe behavior for every corpus entry: no unhandled exceptions, no stack traces, and stable diagnostics.
- Compile generated output for corpus entries expected to succeed.
- Establish a repeatable path for promoting future field failures into permanent regression coverage.

**Non-Goals:**
- Fetch live public specs during test execution.
- Guarantee that every selected public spec generates with zero warnings.
- Build a fuzzing system or property-based generator in this change.
- Introduce product behavior changes to make specific public specs pass.
- Add new production dependencies or runtime configuration surface.

## Decisions

1. **Use pinned in-repo snapshots, not live URLs, for the corpus**
   - Each real-world test input will be stored in the repository or generated from a pinned upstream commit/release and checked in as test data.
   - Metadata will record the upstream source URL and pinned revision so the corpus is auditable.
   - Rationale: keeps tests deterministic, offline, and reviewable.
   - Alternative considered: download live specs during tests. Rejected because it makes the suite flaky and non-reproducible.

2. **Add a manifest-driven harness in the existing integration test project**
   - Store corpus metadata in a manifest file that describes fixture path, source metadata, expected outcome, and whether compile verification is required.
   - Keep the harness in `ApiStitch.IntegrationTests` so it can reuse existing helpers (`CliTestHelper`, `RoslynCompilationHelper`) instead of creating a separate project prematurely.
   - Rationale: one place to express expectations and lower maintenance cost.
   - Alternative considered: one hand-written `[Fact]` per public spec. Rejected because the corpus will grow and per-spec boilerplate becomes noisy.

3. **Model outcome classes explicitly**
   - Every corpus entry will declare one of:
     - `Success`: generation completes without error diagnostics and emitted code compiles.
     - `SuccessWithWarnings`: generation completes without error diagnostics, warning diagnostics are allowed, and emitted code compiles.
     - `ExpectedDiagnosticFailure`: generation fails with known diagnostic codes/messages, but the process must stay controlled.
   - Rationale: some real-world specs are valuable as negative regression cases; we should preserve those without pretending they are expected to pass today.
   - Alternative considered: require every corpus entry to compile. Rejected because it prevents us from locking in known failures safely.

4. **Run both library and CLI paths, but avoid duplicate deep assertions**
   - The library path will provide the deeper assertions around diagnostics, emitted files, and compile checks.
   - The CLI path will validate process-level behavior: exit code, stderr/stdout shape, and absence of stack traces.
   - Rationale: keeps coverage broad without duplicating every assertion twice.
   - Alternative considered: CLI-only verification. Rejected because it makes failure diagnosis harder and loses direct access to generation results.

5. **Use a two-tier regression strategy: raw corpus plus minimized repro fixtures**
   - Large upstream snapshots catch integration failures that only appear at scale.
   - When a specific failure is understood, we should also add or update a minimized fixture that isolates the root cause.
   - Rationale: raw corpus proves realism; minimized fixtures keep debugging and maintenance tractable.
   - Alternative considered: only keep raw specs. Rejected because enormous fixtures are poor for root-cause-focused regression coverage.

6. **Keep the initial corpus intentionally small and curated**
   - Seed with four mandatory public specs plus one optional stress specimen if repository size and execution time stay acceptable.
   - Add new specimens only when they represent a distinct failure class or ecosystem pattern.
   - Rationale: prevents the suite from becoming slow, noisy, or dominated by redundant documents.
   - Alternative considered: import every large public spec we can find. Rejected because it would bloat the repo and reduce signal.

## Risks / Trade-offs

- **[Risk] Large checked-in fixtures increase repository size and test runtime** -> **Mitigation:** cap the initial corpus, prefer compressed/minimized storage where practical, and keep only representative sources.
- **[Risk] Upstream specs may have license or redistribution constraints** -> **Mitigation:** select openly published sources with compatible redistribution terms or store pinned extracts/minimized snapshots when full redistribution is questionable.
- **[Risk] Expected-failure entries can normalize broken behavior** -> **Mitigation:** require explicit diagnostic expectations and promote fixes by updating entry classification when support improves.
- **[Risk] Real-world specs can fail for multiple reasons over time, making assertions brittle** -> **Mitigation:** assert on stable outcome classes and diagnostic codes first, with narrowly chosen message fragments only when necessary.
- **[Trade-off] Running both CLI and library paths costs more time** -> **Mitigation:** keep deep compile checks on the library path and reserve CLI assertions for process behavior.
- **[Trade-off] Kubernetes may be too large for the default suite** -> **Mitigation:** treat it as an optional stress specimen gated by practical repo/CI limits.

## Migration Plan

No product migration is required. The rollout is purely test-side:
1. Add corpus layout and manifest support.
2. Seed the initial pinned specimens and classify expectations.
3. Add harness coverage to CI.
4. Convert future field failures into corpus entries or minimized repro fixtures.

Rollback is straightforward: remove the new integration tests and fixtures if they prove too expensive, without affecting shipped product behavior.

## Open Questions

- Which public sources have redistribution terms that are acceptable for checked-in snapshots versus requiring minimized extracts?
- Should the optional stress specimen run in the default `dotnet test` path or under a separate category/trait?
- Do we want a small helper script for refreshing pinned corpus metadata later, or is manual curation sufficient for now?
