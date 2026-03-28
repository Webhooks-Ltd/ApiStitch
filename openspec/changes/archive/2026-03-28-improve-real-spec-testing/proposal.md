## Why

ApiStitch's current tests are strong against curated fixtures but weak against the long tail of large, messy public OpenAPI documents. We keep rediscovering failures only when running the CLI against real-world specs, so we need a regression corpus that turns those failures into repeatable tests.

## What Changes

- Add a pinned real-world regression test corpus built from large public OpenAPI descriptions chosen to stress parsing, transformation, emission, and compilation.
- Add a manifest-driven test harness that runs the generator end-to-end for each corpus entry and asserts the expected outcome class.
- Require crash-safety assertions so real-world corpus runs fail with structured diagnostics rather than unhandled exceptions or stack traces.
- Require successful corpus entries to compile generated output, while allowing explicitly classified warning-only and known-diagnostic failure cases.
- Add a workflow for promoting future production failures into either the pinned corpus or minimized repro fixtures.

## Capabilities

### New Capabilities
- `real-spec-regression-testing`: Maintain and execute a curated real-world OpenAPI regression corpus with explicit expectations and crash-safety guarantees.

### Modified Capabilities
- None.

## Impact

- Affected code: integration test infrastructure, spec fixtures, Roslyn compile verification, CLI/library end-to-end test coverage, and contributor workflow for adding regressions.
- APIs: no runtime product API or CLI behavior changes; this is a quality and validation change.
- Dependencies: no new production dependencies expected; test-only support code may be added within existing test projects.
- Systems: improves confidence against public-spec regressions and makes future failures reproducible in CI.
