## 1. Output style and configuration surface

- [x] 1.1 Extend `OutputStyle` enum to include `TypedClientStructured` and `TypedClientFlat`
- [x] 1.2 Set default output style to `TypedClientStructured` in config defaults and CLI fallback paths
- [x] 1.3 Update config loader validation/errors for new style values (case-insensitive parsing retained)
- [x] 1.4 Update CLI `--output-style` validation/help text to include new values

## 2. Structured/flat relative path mapping

- [x] 2.1 Introduce deterministic file-path routing for client emitter outputs by selected layout mode
- [x] 2.2 Route client interfaces to `Contracts/` and implementations to `Clients/` in structured mode
- [x] 2.3 Route shared files (`ApiException`, `ProblemDetails`, JsonContext, options, json options, service extensions, enum extensions) to `Infrastructure/` and `Configuration/` in structured mode
- [x] 2.4 Keep `TypedClientFlat` behavior as root-level output for all generated files
- [x] 2.5 Ensure emitted `RelativePath` ordering remains deterministic for both layout modes

## 3. Tests and regression coverage

- [x] 3.1 Add/adjust configuration tests for output style parsing and structured default
- [x] 3.2 Add/adjust CLI tests for `--output-style` values, invalid value diagnostics, and default selection
- [x] 3.3 Add/adjust emitter tests for structured layout file inventory and folder placement
- [x] 3.4 Add/adjust emitter tests for flat layout file inventory parity
- [x] 3.5 Run full solution tests and resolve regressions

## 4. Documentation and rollout notes

- [x] 4.1 Add `CHANGELOG.md` `Unreleased` entry for configurable layouts and new default
- [x] 4.2 Update `README.md` output-style guidance and examples for structured/flat layout selection
