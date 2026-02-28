## MODIFIED Requirements

### Requirement: CLI generate subcommand

The system SHALL provide an `apistitch generate` subcommand that loads configuration, runs the generation pipeline, writes files to disk, and reports results.

The `--spec` option SHALL accept either a local file path or a full HTTP(S) URL.

#### Scenario: Generate from YAML config
- **WHEN** `apistitch generate` is run in a directory containing `openapi-stitch.yaml`
- **THEN** the config is loaded from that file
- **THEN** the generation pipeline runs
- **THEN** generated files are written to the configured output directory
- **THEN** a summary line is printed to stdout
- **THEN** the process exits with code 0

#### Scenario: Generate with explicit config path
- **WHEN** `apistitch generate --config /path/to/custom.yaml` is run
- **THEN** the specified YAML file is loaded instead of discovering `openapi-stitch.yaml` in CWD

#### Scenario: Generate with HTTPS spec URL from CLI
- **WHEN** `apistitch generate --spec https://example.test/openapi.yaml` is run
- **THEN** the generation pipeline fetches and parses the remote spec
- **THEN** generated files are written to the configured output directory
- **THEN** the process exits with code 0 when no error diagnostics are emitted

#### Scenario: CLI --spec URL overrides YAML local spec
- **WHEN** `openapi-stitch.yaml` contains a local `spec` path and CLI passes `--spec https://example.test/openapi.yaml`
- **THEN** the CLI `--spec` value is used
- **THEN** the local YAML `spec` value is ignored for that run

#### Scenario: CLI --spec local path overrides YAML URL spec
- **WHEN** `openapi-stitch.yaml` contains `spec: https://example.test/openapi.yaml` and CLI passes `--spec ./openapi-local.yaml`
- **THEN** the CLI `--spec` local path is used
- **THEN** YAML URL `spec` is ignored for that run
