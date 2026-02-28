## MODIFIED Requirements

### Requirement: CLI argument precedence

CLI arguments SHALL take precedence over configuration file values.

Supported CLI options include:
- `--spec`
- `--namespace`
- `--output`
- `--output-style`
- `--client-name`

When both CLI and config provide values for the same setting, CLI value SHALL win.

#### Scenario: CLI --namespace overrides config
- **WHEN** config has `namespace: Config.Namespace` and CLI has `--namespace Cli.Namespace`
- **THEN** generated code uses `Cli.Namespace`

#### Scenario: CLI --output overrides config
- **WHEN** config has `outputDir: ./generated` and CLI has `--output ./out`
- **THEN** files are written to `./out`

#### Scenario: CLI --output-style option
- **WHEN** CLI has `--output-style TypedClientStructured`
- **THEN** `ApiStitchConfig.OutputStyle` is `OutputStyle.TypedClientStructured`

#### Scenario: CLI --output-style flat option
- **WHEN** CLI has `--output-style TypedClientFlat`
- **THEN** `ApiStitchConfig.OutputStyle` is `OutputStyle.TypedClientFlat`

#### Scenario: CLI --output-style invalid value
- **WHEN** CLI has `--output-style InvalidStyle`
- **THEN** command exits with code 2
- **THEN** stderr includes message listing valid options (`TypedClientStructured`, `TypedClientFlat`)

#### Scenario: output style default when omitted
- **WHEN** neither config nor CLI specify output style
- **THEN** output style defaults to `OutputStyle.TypedClientStructured`
