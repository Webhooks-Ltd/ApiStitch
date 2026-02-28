# cli-and-file-output Specification

## Purpose
TBD - created by archiving change cli-and-file-writer. Update Purpose after archive.
## Requirements
### Requirement: Write generated files to disk

The system SHALL write `GeneratedFile` objects to the specified output directory, creating subdirectories as needed. Files SHALL be encoded as UTF-8 without BOM (using `new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)`). File writes SHALL be sequential (not parallelised). The system SHALL reject `RelativePath` values that contain `..` segments or are absolute paths, throwing `ArgumentException`.

#### Scenario: Write files to empty output directory
- **WHEN** `FileWriter.WriteAsync` is called with 5 generated files and an output directory that does not exist
- **THEN** the output directory is created
- **THEN** all 5 files are written to disk at their `RelativePath` within the output directory
- **THEN** `FileWriteResult.Written` contains all 5 file paths
- **THEN** `FileWriteResult.Unchanged` is empty
- **THEN** `FileWriteResult.Deleted` is empty

#### Scenario: Write files to existing output directory
- **WHEN** `FileWriter.WriteAsync` is called and the output directory already exists
- **THEN** files are written without error, overwriting any existing files with different content

#### Scenario: Nested relative paths create subdirectories
- **WHEN** a `GeneratedFile` has `RelativePath = "Models/Pet.cs"`
- **THEN** the `Models` subdirectory is created within the output directory if it does not exist
- **THEN** the file is written at `{outputDirectory}/Models/Pet.cs`

#### Scenario: Files are encoded as UTF-8 without BOM
- **WHEN** any file is written to disk
- **THEN** the file uses UTF-8 encoding with no byte-order mark

#### Scenario: Path traversal rejected
- **WHEN** a `GeneratedFile` has `RelativePath` containing `..` segments (e.g., `../../etc/passwd`)
- **THEN** `FileWriter.WriteAsync` throws `ArgumentException`

#### Scenario: Absolute path rejected
- **WHEN** a `GeneratedFile` has an absolute `RelativePath` (e.g., `/etc/passwd` or `C:\Windows\file.cs`)
- **THEN** `FileWriter.WriteAsync` throws `ArgumentException`

#### Scenario: Empty file list
- **WHEN** `FileWriter.WriteAsync` is called with an empty `IReadOnlyList<GeneratedFile>`
- **THEN** no files are written
- **THEN** `FileWriteResult.Written`, `Unchanged`, and `Deleted` are all empty
- **THEN** if `cleanOutput` is true, no files are deleted (empty output is treated as a no-op, not a delete-all)

### Requirement: Skip writing unchanged files

The system SHALL compare new content against existing files before writing. If the content is identical, the write SHALL be skipped to preserve the file's modification timestamp.

#### Scenario: File content unchanged
- **WHEN** `FileWriter.WriteAsync` is called and a file already exists on disk with identical content
- **THEN** the file is NOT rewritten (modification timestamp preserved)
- **THEN** the file path appears in `FileWriteResult.Unchanged`, not `Written`

#### Scenario: File content changed
- **WHEN** `FileWriter.WriteAsync` is called and a file already exists on disk with different content
- **THEN** the file IS rewritten with the new content
- **THEN** the file path appears in `FileWriteResult.Written`

#### Scenario: File does not exist yet
- **WHEN** `FileWriter.WriteAsync` is called and a file does not exist on disk
- **THEN** the file is written
- **THEN** the file path appears in `FileWriteResult.Written`

#### Scenario: Line ending change triggers rewrite
- **WHEN** a file exists on disk with `\r\n` line endings but the new content has `\n` line endings
- **THEN** the file IS rewritten with canonical content (self-healing)

### Requirement: Return FileWriteResult with write summary

`FileWriter.WriteAsync` SHALL return a `FileWriteResult` reporting which files were written, which were unchanged, and which were deleted. The `.apistitch.manifest` file SHALL NOT appear in any of the result lists (it is bookkeeping, not generated code).

#### Scenario: Mixed result
- **WHEN** 3 files are new, 2 files are unchanged, and 1 stale file is deleted
- **THEN** `FileWriteResult.Written` has 3 entries, `Unchanged` has 2 entries, `Deleted` has 1 entry

#### Scenario: All files new
- **WHEN** all files are written to an empty directory
- **THEN** `FileWriteResult.Written` has all file paths, `Unchanged` and `Deleted` are empty

#### Scenario: Manifest excluded from result
- **WHEN** `cleanOutput` is enabled and the `.apistitch.manifest` is written
- **THEN** it does NOT appear in `FileWriteResult.Written`, `Unchanged`, or `Deleted`

### Requirement: Support CancellationToken

`FileWriter.WriteAsync` SHALL accept a `CancellationToken` parameter and pass it to all async I/O operations. The file writer SHALL NOT attempt rollback on cancellation.

#### Scenario: Cancellation during write
- **WHEN** cancellation is requested during file writing
- **THEN** the operation throws `OperationCanceledException`
- **THEN** files already written remain on disk (no rollback attempted)

### Requirement: Delete stale files via manifest when cleanOutput is enabled

When `FileWriteOptions.CleanOutput` is `true`, the system SHALL maintain a `.apistitch.manifest` file in the output directory and delete files from previous runs that are no longer in the output set. The manifest SHALL list one relative file path per line, sorted alphabetically.

#### Scenario: First run with cleanOutput enabled
- **WHEN** `cleanOutput` is `true` and no `.apistitch.manifest` exists
- **THEN** all files are written
- **THEN** a `.apistitch.manifest` is created listing all written file paths (sorted, one per line)
- **THEN** `FileWriteResult.Deleted` is empty

#### Scenario: Subsequent run deletes stale files
- **WHEN** `cleanOutput` is `true` and a previous `.apistitch.manifest` lists `["Pet.cs", "Category.cs", "Dog.cs"]`
- **THEN** the current run generates `["Pet.cs", "Animal.cs"]`
- **THEN** `Category.cs` and `Dog.cs` are deleted from disk
- **THEN** `FileWriteResult.Deleted` contains `["Category.cs", "Dog.cs"]`
- **THEN** the new `.apistitch.manifest` lists `["Animal.cs", "Pet.cs"]`

#### Scenario: Stale file already manually deleted
- **WHEN** a file listed in the old manifest has already been deleted by the user
- **THEN** the system does NOT throw an error
- **THEN** the file does NOT appear in `FileWriteResult.Deleted`

#### Scenario: cleanOutput disabled (default)
- **WHEN** `cleanOutput` is `false` or `FileWriteOptions` is null
- **THEN** no `.apistitch.manifest` is written
- **THEN** no files are deleted
- **THEN** `FileWriteResult.Deleted` is empty

#### Scenario: Crash-safe ordering
- **WHEN** files are written and the manifest is updated
- **THEN** the execution order SHALL be: write files, write new manifest, delete stale files
- **THEN** if the process crashes after writing but before deleting, stale files remain (cleaned up on next run)

#### Scenario: Empty generation with cleanOutput enabled
- **WHEN** `cleanOutput` is `true` and `FileWriter.WriteAsync` receives an empty file list
- **THEN** no files are deleted (empty output is a no-op, not a delete-all)
- **THEN** the manifest is NOT updated

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

### Requirement: YAML paths resolve relative to YAML file location

Paths specified in the YAML config file (`spec`, `outputDir`) SHALL resolve relative to the directory containing the YAML file, not the current working directory. CLI `--spec` and `--output` paths SHALL resolve relative to CWD.

#### Scenario: Spec path relative to YAML file
- **WHEN** `--config /projects/myapi/openapi-stitch.yaml` is used and the YAML contains `spec: ../specs/petstore.yaml`
- **THEN** the spec is loaded from `/projects/specs/petstore.yaml`

#### Scenario: CLI paths relative to CWD
- **WHEN** `--spec ../specs/petstore.yaml` is passed as a CLI argument from CWD `/home/user`
- **THEN** the spec path resolves to `/home/specs/petstore.yaml`

### Requirement: ConfigLoader exposes delivery config

`ConfigLoader` SHALL parse the optional `delivery` section from YAML and return `FileWriteOptions` alongside `ApiStitchConfig`. The return type SHALL be `(ApiStitchConfig? Config, FileWriteOptions Delivery, IReadOnlyList<Diagnostic> Diagnostics)`.

#### Scenario: YAML with delivery section
- **WHEN** YAML contains `delivery: { cleanOutput: true }`
- **THEN** the returned `FileWriteOptions.CleanOutput` is `true`

#### Scenario: YAML without delivery section
- **WHEN** YAML has no `delivery` section
- **THEN** the returned `FileWriteOptions` has default values (`CleanOutput = false`)

#### Scenario: Unknown delivery properties ignored
- **WHEN** YAML contains `delivery: { cleanOutput: true, futureProperty: foo }`
- **THEN** the unknown property is silently ignored (consistent with existing `IgnoreUnmatchedProperties` behaviour)

### Requirement: Report diagnostics to stderr

The system SHALL write all diagnostics (warnings and errors) to stderr in MSBuild-compatible format: `{severity} {code}: {message}[ {specPath}]`.

#### Scenario: Warning diagnostic
- **WHEN** the generation pipeline emits a warning diagnostic with code AS400 and message "Operation has no operationId" at spec path "paths./pets.get"
- **THEN** stderr receives: `warning AS400: Operation has no operationId [paths./pets.get]`

#### Scenario: Error diagnostic
- **WHEN** the generation pipeline emits an error diagnostic with code AS302 and no spec path
- **THEN** stderr receives: `error AS302: Configuration property 'spec' is required and must not be empty`

#### Scenario: Summary line to stdout
- **WHEN** generation completes successfully with 10 files written, 2 unchanged, and 1 deleted
- **THEN** stdout receives: `Generated 10 files in ./Generated (2 unchanged, 1 deleted)`
- **THEN** the path in the summary is the `outputDir` value as configured (not normalised to absolute)

#### Scenario: Summary line without cleanup stats
- **WHEN** generation completes with 10 files written, 0 unchanged, and cleanOutput disabled
- **THEN** stdout receives: `Generated 10 files in ./Generated`
- **THEN** zero-count stats are omitted from the parenthetical

#### Scenario: Summary line with unchanged only
- **WHEN** generation completes with 10 files written, 2 unchanged, and cleanOutput disabled
- **THEN** stdout receives: `Generated 10 files in ./Generated (2 unchanged)`

### Requirement: Exit codes

The CLI SHALL return exit codes indicating the outcome of the operation.

#### Scenario: Successful generation
- **WHEN** generation completes with no error diagnostics
- **THEN** exit code is 0 (even if warnings were emitted)

#### Scenario: Generation errors
- **WHEN** the generation pipeline returns error-severity diagnostics (spec parse failures, config errors)
- **THEN** exit code is 1

#### Scenario: Bad arguments or missing config
- **WHEN** the CLI receives invalid arguments or cannot find a config file
- **THEN** exit code is 2

#### Scenario: Unhandled exception
- **WHEN** an unexpected exception occurs during generation
- **THEN** a clean error message is printed to stderr (no stack trace)
- **THEN** exit code is 1

### Requirement: CLI --version flag

The root command SHALL support a `--version` flag that displays the tool version.

#### Scenario: Version output
- **WHEN** `apistitch --version` is run
- **THEN** the assembly version is printed to stdout
- **THEN** exit code is 0

### Requirement: CLI project packaged as dotnet tool

The CLI project SHALL be configured as a .NET tool with `<PackAsTool>true</PackAsTool>` and `<ToolCommandName>apistitch</ToolCommandName>`.

#### Scenario: Tool command name
- **WHEN** the CLI project is built and packed
- **THEN** the tool is invocable as `apistitch`

#### Scenario: Project references core library
- **WHEN** the CLI project is compiled
- **THEN** it references the `ApiStitch` core library project

