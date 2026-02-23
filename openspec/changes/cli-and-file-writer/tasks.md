## 1. Core Types and Infrastructure

- [ ] 1.1 Create `FileWriteResult` sealed record in `ApiStitch.IO` with properties: `Written` (`IReadOnlyList<string>`), `Unchanged` (`IReadOnlyList<string>`), `Deleted` (`IReadOnlyList<string>`)
- [ ] 1.2 Create `FileWriteOptions` sealed class in `ApiStitch.IO` with property: `CleanOutput` (`bool`, default `false`)
- [ ] 1.3 Add `DeliveryDto` nested class to `ConfigDto` in `ConfigLoader.cs` with `CleanOutput` property (`bool?`)
- [ ] 1.4 Update `ConfigLoader.Load` and `ConfigLoader.LoadFromYaml` return type to `(ApiStitchConfig? Config, FileWriteOptions Delivery, IReadOnlyList<Diagnostic> Diagnostics)`; parse `delivery.cleanOutput` from YAML, default to `new FileWriteOptions()` when absent
- [ ] 1.5 Update all callers of `ConfigLoader.Load` and `ConfigLoader.LoadFromYaml` (pipeline, tests) to destructure the new return type
- [ ] 1.6 Add unit tests for ConfigLoader delivery section: YAML with `delivery.cleanOutput: true`, YAML without delivery section (defaults), unknown delivery properties ignored

## 2. FileWriter — Core Write Logic

- [ ] 2.1 Create static class `FileWriter` in `ApiStitch.IO` with `WriteAsync(IReadOnlyList<GeneratedFile> files, string outputDirectory, FileWriteOptions? options, CancellationToken cancellationToken)` returning `Task<FileWriteResult>`
- [ ] 2.2 Implement path validation: reject `RelativePath` containing `..` segments or rooted/absolute paths with `ArgumentException`
- [ ] 2.3 Implement empty file list guard: return empty `FileWriteResult` immediately (no manifest update, no deletions); also serves as the empty-generation guard when `cleanOutput` is true
- [ ] 2.4 Implement output directory creation and subdirectory creation: `Directory.CreateDirectory` for output dir and for nested `RelativePath` parent directories
- [ ] 2.6 Implement UTF-8 no-BOM encoding: use `new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)` for all reads and writes
- [ ] 2.7 Implement content-comparison skip: read existing file, compare via string equality, skip write if identical; track in `Written` vs `Unchanged` lists
- [ ] 2.8 Pass `CancellationToken` to all async I/O operations (`ReadAllTextAsync`, `WriteAllTextAsync`)

## 3. FileWriter — Manifest and Stale Cleanup

- [ ] 3.1 Implement manifest read: parse `.apistitch.manifest` from output directory (one path per line) on `cleanOutput: true`
- [ ] 3.2 Implement crash-safe ordering: write files first, then write new manifest, then delete stale files
- [ ] 3.3 Implement manifest write: sorted relative paths, one per line, UTF-8 no-BOM
- [ ] 3.4 Implement stale file deletion: compute set difference (old manifest minus current files), delete each file, skip if already missing
- [ ] 3.5 Ensure `.apistitch.manifest` does NOT appear in `FileWriteResult.Written`, `Unchanged`, or `Deleted`
- [ ] 3.6 Verify empty-generation guard is handled by 2.3 early return (no manifest update, no deletions when file list is empty)

## 4. FileWriter — Tests

- [ ] 4.1 Add test: write files to empty (non-existent) output directory — all appear in `Written`
- [ ] 4.2 Add test: write files to existing directory — unchanged files in `Unchanged`, changed files in `Written`
- [ ] 4.3 Add test: nested `RelativePath` creates subdirectories
- [ ] 4.4 Add test: files encoded as UTF-8 without BOM (read bytes, check no BOM preamble)
- [ ] 4.5 Add test: path traversal (`..`) throws `ArgumentException`
- [ ] 4.6 Add test: absolute path throws `ArgumentException`
- [ ] 4.7 Add test: empty file list returns empty result (no files written or deleted)
- [ ] 4.8 Add test: content-comparison skip preserves file timestamp when content unchanged
- [ ] 4.9 Add test: line-ending difference triggers rewrite
- [ ] 4.10 Add test: manifest written on first run with `cleanOutput: true`
- [ ] 4.11 Add test: stale files deleted on subsequent run with `cleanOutput: true`
- [ ] 4.12 Add test: stale file already manually deleted — no error
- [ ] 4.13 Add test: `cleanOutput: false` — no manifest written, no files deleted
- [ ] 4.14 Add test: empty file list with `cleanOutput: true` — no deletions, manifest not updated
- [ ] 4.15 Add test: `.apistitch.manifest` excluded from all result lists
- [ ] 4.16 Add test: CancellationToken cancellation during write throws `OperationCanceledException`, already-written files remain on disk

## 5. CLI Project Setup

- [ ] 5.1 Create `src/ApiStitch.Cli/ApiStitch.Cli.csproj` with `OutputType=Exe`, `net8.0`, `PackAsTool=true`, `ToolCommandName=apistitch`, `IsPackable=true`, project reference to `ApiStitch`, `System.CommandLine` 2.0.* package reference
- [ ] 5.2 Add `ApiStitch.Cli` project to `ApiStitch.slnx` under `/src/` folder
- [ ] 5.3 Verify the solution builds (`dotnet build`)

## 6. CLI — Command Structure

- [ ] 6.1 Create `Program.cs` with `async Task<int> Main` entry point using System.CommandLine `RootCommand` with `--version` option
- [ ] 6.2 Create `generate` subcommand (`Command`) with options: `--spec` (string?), `--output` (string?), `--namespace` (string?), `--client-name` (string?), `--output-style` (string?), `--config` (string?), `--clean-output` (bool flag)
- [ ] 6.3 Add top-level `try/catch` in command action: map unhandled exceptions to clean error on stderr + exit code 1

## 7. CLI — Config Resolution

- [ ] 7.1 Implement YAML discovery: if `--config` provided, use that path; otherwise look for `openapi-stitch.yaml` in CWD
- [ ] 7.2 Implement fail-fast: if no YAML found and no `--spec` CLI arg, print error message and return exit code 2
- [ ] 7.3 Load YAML via `ConfigLoader.Load`, destructure `(config, delivery, diagnostics)`
- [ ] 7.4 Implement YAML path resolution: resolve `spec` and `outputDir` from YAML relative to the YAML file's directory (not CWD)
- [ ] 7.5 Implement CLI override layering: overlay non-null CLI option values onto the YAML-loaded `ApiStitchConfig` (create new config instance with overrides)
- [ ] 7.6 Implement `--clean-output` merging: CLI flag `true` overrides YAML `delivery.cleanOutput`; when CLI flag absent, use YAML value
- [ ] 7.7 Implement CLI-only config (no YAML): build `ApiStitchConfig` from CLI args with defaults, resolve `--spec` and `--output` relative to CWD

## 8. CLI — Generation and Output

- [ ] 8.1 Call `GenerationPipeline.Generate(config)` to produce `GenerationResult`
- [ ] 8.2 Call `FileWriter.WriteAsync(result.Files, outputDir, fileWriteOptions, cancellationToken)` to write files to disk
- [ ] 8.3 Implement diagnostic formatting: write each diagnostic to stderr as `{severity} {code}: {message} [{specPath}]` (lowercase severity); omit the `[{specPath}]` suffix entirely when `SpecPath` is null
- [ ] 8.4 Implement summary line to stdout: `Generated N files in {outputDir}` with optional parenthetical for non-zero unchanged/deleted counts (zero-count stats omitted)
- [ ] 8.5 Implement exit code logic: 0 on success (even with warnings), 1 on error-severity diagnostics or unhandled exceptions, 2 on bad args/missing config
- [ ] 8.6 Wire up `CancellationToken` from `Console.CancelKeyPress` / `CancellationTokenSource` through to `FileWriter.WriteAsync`

## 9. CLI — Integration Tests

- [ ] 9.1 Add test: generate from YAML config file — verify files written to output dir, exit code 0
- [ ] 9.2 Add test: generate with `--spec` and `--output` CLI args (no YAML) — verify files written, exit code 0
- [ ] 9.3 Add test: no config and no `--spec` — verify error message on stderr, exit code 2
- [ ] 9.4 Add test: CLI args override YAML values (`--namespace` overrides YAML namespace)
- [ ] 9.5 Add test: YAML spec path resolves relative to YAML file directory
- [ ] 9.6 Add test: `--clean-output` deletes stale files from previous run
- [ ] 9.7 Add test: diagnostics written to stderr in MSBuild format
- [ ] 9.8 Add test: summary line written to stdout with correct counts
- [ ] 9.9 Add test: unhandled exception produces clean error (no stack trace), exit code 1
- [ ] 9.10 Add test: `--version` flag outputs assembly version and exits with code 0
- [ ] 9.11 Add test: `--output-style TypedClient` and `--client-name MyApi` override YAML/defaults
- [ ] 9.12 Add test: defaults when only `--spec` provided — namespace defaults to `ApiStitch.Generated`, output to `./Generated`, style to `TypedClient`
- [ ] 9.13 Add test: warnings in diagnostics still produce exit code 0 (not 1)
