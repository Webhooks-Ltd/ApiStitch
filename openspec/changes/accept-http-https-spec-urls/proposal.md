## Why

ApiStitch currently expects local spec file paths, which makes quick evaluation of public OpenAPI documents and remote CI-driven generation flows awkward. Supporting direct HTTP(S) spec URLs removes a common setup barrier and aligns CLI/config usage with how teams often share specs.

## What Changes

- Add HTTP(S) support for `spec` input in both CLI `--spec` and `openapi-stitch.yaml` configuration.
- Update spec loading to fetch and parse remote OpenAPI documents over HTTP(S), including JSON and YAML payloads.
- Preserve existing local-file behavior and diagnostics while adding clear diagnostics for URL fetch failures and unsupported URL schemes.
- Keep deterministic generation behavior unchanged after document loading.

## Capabilities

### New Capabilities
- `remote-spec-loading`: Load OpenAPI specs directly from HTTP(S) URLs with consistent diagnostics and format handling.

### Modified Capabilities
- `spec-parsing`: Extend input source handling from local files to local files plus HTTP(S) URLs.
- `configuration`: Allow `spec` in YAML configuration to be either a local path or full HTTP(S) URL.
- `cli-and-file-output`: Ensure CLI `--spec` accepts HTTP(S) URLs and documents/diagnoses URL handling behavior.

## Impact

- Affected code: config/path resolution flow, OpenAPI spec loader, CLI validation and diagnostics, parsing tests.
- APIs: no breaking public API changes; existing path-based usage remains valid.
- Dependencies: likely uses existing BCL `HttpClient`/`HttpClientHandler` patterns; no new third-party package expected.
- Systems: improves usability for remote-spec workflows in local dev and CI.
