## Why

The generation pipeline produces correct, compilable C# code in memory (`GenerationResult` with `GeneratedFile` objects), but nothing writes those files to disk. There is no CLI tool and no file-writing infrastructure. A developer cannot point ApiStitch at an OpenAPI spec and get `.cs` files out. This is the gap between "library that generates code" and "tool that developers can use."

## What Changes

- **New `ApiStitch.Cli` project**: A `dotnet tool` that accepts command-line arguments (spec path, output dir, namespace, client name, output style), loads config from YAML or CLI args, runs the generation pipeline, writes files to disk, and reports diagnostics to stderr. Uses `System.CommandLine` (2.0.3, now stable) for argument parsing — proper help text, tab completion, and future subcommand support without rework. Entry point is `async Task<int>` from day one for future async pipeline support.
- **New `FileWriter` utility**: A static class in the core library (`ApiStitch/IO/FileWriter.cs`) that takes `IReadOnlyList<GeneratedFile>` and an output directory. Creates directories as needed, writes UTF-8 files (no BOM, explicit `new UTF8Encoding(false)`). Compares content before writing to preserve file timestamps for incremental MSBuild builds. Returns a `FileWriteResult` reporting files written, unchanged, and deleted.
- **Manifest-based stale file cleanup**: The file writer maintains a `.apistitch.manifest` file listing all generated files. On subsequent runs, files in the old manifest but not in the new output set are deleted. Off by default (`cleanOutput: false` in config), opt-in to avoid accidentally deleting user files (partial classes, extensions).
- **Config resolution**: The CLI discovers `openapi-stitch.yaml` in the current directory by convention, or accepts an explicit `--config` path. CLI args override YAML values. Precedence: defaults < YAML file < CLI args. Fails fast with clear error when no config and no `--spec` arg.
- **New solution entry**: `ApiStitch.Cli` project added to `ApiStitch.slnx`.

### Out of Scope
- MSBuild task integration (separate change)
- NuGet packaging / `dotnet tool install` publishing
- Watch mode / file system monitoring
- Multi-spec generation (single spec per invocation)
- Dry-run mode (deferred; `FileWriteResult` enables it later without refactoring)
- Remote spec URLs (requires async pipeline — future change)

## Capabilities

### New Capabilities
- `cli-and-file-output`: The full delivery layer — CLI entry point (argument parsing, config resolution, diagnostic formatting, exit codes) and file writing (directory creation, content-comparison skip, manifest-based stale cleanup, UTF-8 no-BOM encoding, `FileWriteResult` reporting).

### Modified Capabilities
None. The generation pipeline, config loading, and emission are unchanged. The CLI and file writer are pure consumers of existing APIs.

## Impact

- **New source files**: `src/ApiStitch.Cli/Program.cs`, `src/ApiStitch.Cli/ApiStitch.Cli.csproj`, `src/ApiStitch/IO/FileWriter.cs`
- **Solution file**: `ApiStitch.slnx` gains the CLI project entry
- **Dependencies**: `System.CommandLine` 2.0.3 (stable) in CLI project only. Core library gains no new dependencies.
- **Generated output**: No change to generated code content or structure. New `.apistitch.manifest` file written alongside output (when `cleanOutput` is enabled).
- **Exit codes**: 0 = success, 1 = failure (spec parse errors, config errors, I/O errors)
- **Console output**: Diagnostics to stderr. Summary line to stdout (e.g., "Generated 12 files in ./Generated"). Verbose output deferred.
- **Existing tests**: Unaffected. FileWriter tested against temp directories. CLI gets integration tests.
