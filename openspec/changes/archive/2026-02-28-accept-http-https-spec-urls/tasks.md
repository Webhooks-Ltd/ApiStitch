## 1. Source detection and loading foundation

- [x] 1.1 Add shared spec-source classification that distinguishes local paths, HTTP(S) URLs, and unsupported URI-like inputs
- [x] 1.2 Extend `OpenApiSpecLoader` with async remote fetch over HTTP(S) and feed existing parser flow
- [x] 1.3 Add diagnostics for unsupported URI schemes, HTTP non-success status codes, request exceptions, timeout, and payload-too-large conditions
- [x] 1.4 Enforce remote fetch safeguards: 30s timeout, 10 MiB payload limit, and max 5 HTTP(S) redirects
- [x] 1.5 Propagate cancellation through remote loading path and keep cancellation handling distinct from generic fetch failures

## 2. Configuration and CLI integration

- [x] 2.1 Ensure YAML config `spec` supports full HTTP(S) URLs without local path rewriting
- [x] 2.2 Ensure CLI `--spec` accepts full HTTP(S) URLs and passes them unchanged to the loader
- [x] 2.3 Preserve existing local-path resolution rules (YAML-relative and CLI CWD-relative) for non-URL inputs, including Windows absolute paths
- [x] 2.4 Ensure CLI `--spec` precedence over YAML `spec` remains unchanged for both local and URL values

## 3. Test coverage and verification

- [x] 3.1 Add unit/integration tests for successful remote YAML and JSON loading paths
- [x] 3.2 Add tests for source classification edge cases (HTTP(S), unsupported URI-like inputs, Windows absolute/local paths)
- [x] 3.3 Add tests for remote failure diagnostics (unsupported scheme, HTTP non-success, request exception, timeout, payload-too-large, redirect-limit)
- [x] 3.4 Add tests for CLI/config precedence and local path resolution regressions
- [x] 3.5 Run targeted parsing/config/CLI tests and fix regressions
- [x] 3.6 Run full solution build/tests and confirm deterministic output behavior remains unchanged
