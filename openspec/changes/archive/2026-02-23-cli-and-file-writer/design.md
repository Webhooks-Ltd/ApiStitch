## Context

The generation pipeline (`GenerationPipeline.Generate`) returns `GenerationResult(IReadOnlyList<GeneratedFile> Files, IReadOnlyList<Diagnostic> Diagnostics)` where each `GeneratedFile` is `record GeneratedFile(string RelativePath, string Content)`. Config is loaded via `ConfigLoader.Load(string filePath)` which reads YAML and returns `(ApiStitchConfig?, IReadOnlyList<Diagnostic>)`. The pipeline and config loader are fully functional and tested — what's missing is the delivery layer: writing files to disk and a CLI entry point.

The core library targets `net8.0`, uses `slnx` solution format, and has no CLI or MSBuild project yet. `ApiStitchConfig` has properties: `Spec` (required), `Namespace` (default `"ApiStitch.Generated"`), `OutputDir` (default `"./Generated"`), `OutputStyle` (enum, default `TypedClient`), `ClientName` (string?). The `ConfigDto` internal class has matching nullable string properties for YAML deserialization.

## Goals / Non-Goals

**Goals:**
- Write generated files to disk with content-comparison skip and manifest-based stale cleanup
- Provide a CLI entry point using System.CommandLine with config resolution (YAML discovery + CLI overrides)
- Report diagnostics to stderr with severity indicators
- Return meaningful exit codes
- Enable future MSBuild task reuse of the file writer

**Non-Goals:**
- MSBuild task integration (separate change)
- NuGet tool packaging / publishing
- Watch mode, multi-spec, remote URLs
- Dry-run mode (deferred; `FileWriteResult` enables it later)
- Verbose/quiet output levels

## Decisions

### D1: FileWriter as static class in core library

**Decision**: Place `FileWriter` in `ApiStitch.IO` namespace within the core `ApiStitch` project as a static class.

**Rationale**: The MSBuild task (future change) will also need to write files to disk. Placing the file writer in the core library avoids duplication. The class is purely I/O — takes `IReadOnlyList<GeneratedFile>`, an output directory, and options; returns a result. No state to manage, so static is appropriate. File writes are sequential (not parallelised) — the file count is small (~100 files) and parallel writes to the same directory cause NTFS journal contention on Windows.

**Signature**:

```csharp
public static class FileWriter
{
    public static async Task<FileWriteResult> WriteAsync(
        IReadOnlyList<GeneratedFile> files,
        string outputDirectory,
        FileWriteOptions? options = null,
        CancellationToken cancellationToken = default)
}

public sealed record FileWriteResult(
    IReadOnlyList<string> Written,
    IReadOnlyList<string> Unchanged,
    IReadOnlyList<string> Deleted);

public sealed class FileWriteOptions
{
    public bool CleanOutput { get; init; }
}
```

**Alternative considered**: Non-static class with `IFileSystem` abstraction for testability. Rejected — file writing is the entire purpose of this class; testing against temp directories is more valuable than mocking and avoids over-abstraction.

### D2: Content-comparison skip

**Decision**: Before writing each file, read the existing file (if any) and compare content via string equality. If identical, skip the write to preserve the file's modification timestamp.

**Rationale**: MSBuild uses file timestamps to determine whether to recompile. Rewriting unchanged files forces unnecessary recompilation. This is cheap (string equality comparison, short-circuits on length mismatch) and critical for the future MSBuild integration.

**Implementation**: `File.Exists` + `File.ReadAllTextAsync` (using same UTF-8 no-BOM encoding) + string equality. If content matches, add to `Unchanged` list. If different or file doesn't exist, write and add to `Written` list.

**Line-ending note**: If a user's editor changes `\n` to `\r\n` in a generated file, the comparison sees a diff and rewrites the canonical output. This is correct self-healing behaviour.

### D3: Manifest-based stale file cleanup

**Decision**: When `CleanOutput` is enabled, maintain a `.apistitch.manifest` file in the output directory listing all generated file relative paths (one per line, sorted).

**Execution order** (crash-safe):
1. Read the previous manifest (if it exists)
2. Write new/changed files to disk
3. Write the new manifest
4. Delete stale files (old manifest minus new manifest)

If the process crashes between steps 3 and 4, stale files remain but will be cleaned up on the next run. This is safe — no data loss.

**Rationale**: Directory-scanning deletion (remove all `.cs` files not in output) is dangerous — users may have hand-written partial classes or extensions in the output directory. The manifest approach only deletes files that ApiStitch previously generated.

**Edge cases**:
- If a user manually deletes a generated file, the stale cleanup gracefully handles `FileNotFoundException` (check existence before delete).
- The `.apistitch.manifest` file should be added to `.gitignore` guidance — it's a generated artifact, not source.

**Default**: `CleanOutput = false`. Users opt in via `cleanOutput: true` in YAML or `--clean-output` CLI flag.

**Alternative considered**: Always-on cleanup with a header comment check (only delete files containing `[GeneratedCode("ApiStitch")]`). Rejected — fragile, and users would need to understand the heuristic.

### D4: UTF-8 encoding without BOM

**Decision**: Use `new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)` explicitly for all file reads and writes.

**Rationale**: `Encoding.UTF8` includes the BOM preamble, which causes issues with some tools and is not the C# convention for source files. The .NET SDK and Roslyn generate files without BOM. Using the same encoding for reads ensures the content-comparison in D2 is consistent.

### D5: System.CommandLine for argument parsing

**Decision**: Use `System.CommandLine` 2.0.3 (stable, shipped Feb 2026) in the CLI project.

**Rationale**: The CLI has ~6 options now, but will grow (subcommands for `validate`, `init`, future output styles). System.CommandLine provides help text generation, tab completion, and a clean extensibility model. Now that 2.0 GA is stable with 69M+ downloads, the perpetual-preview concern no longer applies. The dependency is scoped to the CLI project only — the core library is unaffected.

**CLI surface**:

```
apistitch generate [options]

Options:
  --spec <path>          Path to OpenAPI spec file
  --output <path>        Output directory for generated files [default: ./Generated]
  --namespace <name>     C# namespace for generated types [default: ApiStitch.Generated]
  --client-name <name>   Client name override (derived from spec title if omitted)
  --output-style <style> Output style: TypedClient [default: TypedClient]
  --config <path>        Path to openapi-stitch.yaml config file
  --clean-output         Delete stale generated files from previous runs
  --version              Show version information
```

`generate` is a **subcommand**, not the root command. This supports future subcommands (`validate`, `init`) without changing the UX. `--version` is on the root command.

**API note**: System.CommandLine 2.0 GA uses `command.SetAction(async (ParseResult) => { ... })` — not the `SetHandler` extension pattern from beta4. Implementation must reference 2.0 GA docs, not beta blog posts.

**Alternative considered**: Cocona (2.2.0, last release Mar 2023). Rejected — stale, no release in 3 years, smaller community.

### D6: Config resolution and precedence

**Decision**: Three-layer config resolution with strict precedence: defaults < YAML file < CLI args.

**Resolution order**:
1. If `--config` is provided, load that specific YAML file
2. If no `--config`, look for `openapi-stitch.yaml` in the current directory
3. If YAML file found, load it via existing `ConfigLoader.Load`
4. CLI args override any values from YAML
5. If no YAML and no `--spec` CLI arg, fail with clear error: "No openapi-stitch.yaml found in the current directory. Either create one or pass --spec <path>."

**Path resolution**: Paths in YAML (`spec`, `outputDir`) resolve **relative to the YAML file's directory**. CLI `--spec` and `--output` resolve **relative to CWD**. This follows the convention of tsconfig.json, .editorconfig, and similar tools.

**Implementation**: The CLI builds an `ApiStitchConfig` by starting from YAML (if found), then overlaying any non-null CLI option values. This reuses the existing `ConfigLoader` without modification.

**Why not walk up directory tree**: Complexity trap. Multi-project repos would get confusing behaviour. CWD-only discovery is explicit and predictable.

### D7: Diagnostic output formatting

**Decision**: Diagnostics go to stderr, formatted as:

```
warning AS400: Operation 'GET /pets' has no operationId. Consider adding one. [paths./pets.get]
error AS302: Configuration property 'spec' is required and must not be empty
```

Format: `{severity} {code}: {message}[ {specPath}]` — mirrors MSBuild diagnostic format for familiarity.

**Summary line** goes to stdout: `Generated 12 files in ./Generated (3 unchanged, 2 deleted)`.

**Rationale**: stderr for diagnostics allows piping stdout to other tools. The MSBuild-style format is recognizable to .NET developers and parseable by IDEs.

### D8: CLI project structure

**Decision**: `src/ApiStitch.Cli/` as a console app with `<PackAsTool>true</PackAsTool>` (enables `dotnet tool install` later, though publishing is out of scope for this change).

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>apistitch</ToolCommandName>
    <IsPackable>true</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\ApiStitch\ApiStitch.csproj" />
    <PackageReference Include="System.CommandLine" Version="2.0.*" />
  </ItemGroup>
</Project>
```

**Solution update**: `ApiStitch.slnx` gains the CLI project under `/src/`.

**Entry point**: `async Task<int> Main`. Top-level `try/catch` maps unhandled exceptions to a clean error message + exit code 1 (no stack traces exposed to users).

**Exit codes**:
- `0`: success (with or without warnings)
- `1`: generation failed (errors in diagnostics, I/O errors, unhandled exceptions)
- `2`: bad arguments / config not found

### D9: Config model — CleanOutput property

**Decision**: Keep `CleanOutput` on `FileWriteOptions` only, not on `ApiStitchConfig`. Add a `delivery` section to the YAML config for file-writing concerns.

```yaml
spec: petstore.yaml
namespace: PetStore.Client
delivery:
  cleanOutput: true
```

**Rationale**: `ApiStitchConfig` should remain focused on "what to generate." Delivery concerns (clean output, future dry-run, force-overwrite) belong in `FileWriteOptions`. The `delivery` YAML section maps to `FileWriteOptions` in `ConfigLoader`, keeping the boundary clean. The CLI's `--clean-output` flag also maps to `FileWriteOptions`.

**Implementation**: Add a `DeliveryDto` nested class in `ConfigDto`, and a `FileWriteOptions? Delivery` property on the `ConfigLoader` return type (or a new `DeliveryConfig` alongside `ApiStitchConfig`).

## Risks / Trade-offs

**[System.CommandLine API surface]** → System.CommandLine 2.0 GA has a different API from the beta series. Documentation from older blog posts may be misleading. Mitigated by referencing the 2.0.3 API directly and keeping the CLI surface minimal.

**[Manifest file in output directory]** → The `.apistitch.manifest` file is a hidden implementation detail that could confuse users if they inspect the output directory. Mitigated by: using a dotfile name, only writing it when `cleanOutput` is enabled, documenting it, and recommending `.gitignore` inclusion.

**[Content comparison on large files]** → Reading existing files before writing adds I/O overhead. For typical generated code (~100 files, ~2KB each), this is negligible. If perf becomes a concern, a hash-based approach (store hashes in manifest) can be added later.

**[Synchronous pipeline, async file writer]** → The generation pipeline is synchronous (`GenerationResult Generate(ApiStitchConfig config)`), while the file writer is async. The CLI bridges this cleanly: call `Generate` synchronously, then `await WriteAsync`. When remote spec URLs are added later, the pipeline itself will need to become async — the CLI's `async Task<int> Main` is ready for that transition.
