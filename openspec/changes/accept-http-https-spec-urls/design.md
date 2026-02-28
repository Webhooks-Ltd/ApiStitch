## Context

ApiStitch currently loads specs from local filesystem paths. Users evaluating generator behavior against public API specs must first download those files, which adds friction and creates extra CI plumbing for ephemeral checks. The change should allow full HTTP(S) spec URLs while preserving current local-path behavior, diagnostics, and deterministic generation output.

## Goals / Non-Goals

**Goals:**
- Accept full HTTP(S) URLs anywhere `spec` is provided (`--spec` and YAML config `spec`).
- Keep a single loading abstraction that handles local files and remote URLs consistently.
- Preserve existing parsing behavior for JSON/YAML content and warning/error diagnostics.
- Add explicit diagnostics for network failures, unsupported URL schemes, and non-success HTTP responses.

**Non-Goals:**
- Add authentication features (Bearer tokens, mTLS, custom headers) for remote fetch in this change.
- Add caching, retries, or offline mirror management.
- Change downstream schema transformation, emission, or file-writing behavior.

## Decisions

1. **Keep one `spec` input field and detect source type by URI semantics**
   - `spec` remains a single string in config/CLI.
   - Loader classifies as remote only when the input is an absolute URI with scheme `http` or `https`; otherwise it is treated as a local path.
   - Rationale: avoids config surface growth and keeps backward compatibility.
   - Alternative considered: separate `specUrl` setting. Rejected due to unnecessary duplication and precedence complexity.

2. **Implement remote fetch in `OpenApiSpecLoader` using BCL HTTP APIs**
   - Use `HttpClient` for GET and read response body as text.
   - Enforce v1 safeguards: request timeout (30s), maximum response payload size (10 MiB), and redirect limit (up to 5 HTTP(S) redirects only).
   - Parse content with existing OpenAPI parser pipeline so warnings/errors remain consistent.
   - Rationale: centralizes source handling and keeps parser behavior unchanged after input acquisition.
   - Alternative considered: pre-download in CLI layer. Rejected because it would duplicate logic for config and non-CLI callers.

3. **Make spec loading async and cancellation-aware end-to-end**
   - Add async loader and generation entry path so HTTP requests can honor cancellation.
   - Treat cancellation distinctly from failures (do not map cancellation to generic fetch diagnostics).
   - Rationale: avoids sync-over-async and preserves expected Ctrl+C behavior in CLI.
   - Alternative considered: keep sync pipeline and block on async fetch. Rejected due to deadlock/perf/cancellation risk.

4. **Define strict source validation and diagnostics**
   - Supported remote schemes: `http`, `https` only.
   - Unsupported absolute URI schemes produce an error diagnostic.
   - HTTP non-success status, request failures, TLS failures, timeout, oversize payload, and unreadable payloads produce deterministic error diagnostics with actionable messages.
   - Rationale: predictable behavior and debuggability without silent fallbacks.

5. **Preserve path resolution rules and precedence for local specs**
   - YAML-relative path resolution stays unchanged for local paths.
   - HTTP(S) spec values bypass local path normalization.
   - CLI `--spec` remains highest precedence over YAML `spec`, for both local and URL values.
   - Rationale: prevents regressions in existing repo layouts and keeps URL handling explicit.

## Risks / Trade-offs

- **[Risk] Network flakiness can break generation for remote specs** -> **Mitigation:** clear diagnostics and deterministic failure behavior; no silent retry loops in this phase.
- **[Risk] Very large remote specs can increase memory/time** -> **Mitigation:** enforce 10 MiB payload cap and add tests around boundary behavior.
- **[Risk] Remote specs are mutable and reduce build reproducibility** -> **Mitigation:** document guidance to pin/version/mirror spec URLs for CI workflows.
- **[Risk] SSRF-like misuse in automation contexts** -> **Mitigation:** explicitly document trust boundary and defer stricter allow/deny network policy to follow-up.
- **[Trade-off] No auth support initially** -> **Mitigation:** document as a follow-up if enterprise/private spec demand appears.
