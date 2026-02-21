# ApiStitch — .NET OpenAPI Client Generator

## Strategic Context

See [Product Brief](../docs/strategy/01-product-brief.md) for full context.

**One-liner**: A .NET OpenAPI client generator with first-class type reuse, clean idiomatic output, and zero-config MSBuild integration.

**Tagline**: API clients that use your types, not their own.

**License**: MIT

**MMVP target**: See [Feature Prioritization](../docs/strategy/03-feature-prioritization.md)

## Tech Stack

- .NET 8+ (no .NET Framework, no .NET Standard)
- C# 12
- Microsoft.OpenApi / Microsoft.OpenApi.Readers for spec parsing
- Scriban for code templating
- Microsoft.Build.Utilities.Core for MSBuild task
- Microsoft.CodeAnalysis (Roslyn) for attribute-based type discovery
- System.Text.Json (generated output uses source-generated JsonSerializerContext)

## Architecture

- **Pipeline**: OpenAPI spec → Microsoft.OpenApi parse → Semantic model → Emitter → Generated C# source
- **Semantic model**: Language-agnostic representation of the API surface (endpoints, schemas, parameters, responses). Decoupled from OpenAPI DOM and from output format.
- **Emitters**: Per-output-style code generators that consume the semantic model. MMVP: Refit interface emitter. MVP: Typed HttpClient emitter. v1: Extension method emitter.
- **Type resolver**: Resolves OpenAPI schemas to C# types. Checks explicit YAML mappings first, then namespace exclusions, then attribute-based discovery (Roslyn), then falls back to generating the type.
- **MSBuild task**: Primary delivery mechanism. NuGet package ships .props/.targets. Incremental builds via Inputs/Outputs. Generated code goes to obj/.
- **CLI tool**: Secondary delivery. Same generation engine, invoked via `dotnet apistitch generate`.
- **Configuration**: YAML file (openapi-stitch.yaml) with MSBuild property overrides for simple cases.

## Reflection Policy

Avoid reflection in both the generator and the generated output:
- Generated models use System.Text.Json with source-generated JsonSerializerContext
- Type discovery during generation uses Roslyn semantic analysis, not System.Reflection
- AOT/trimming compatibility is a design constraint for generated output
- Ask for explicit permission before introducing any reflection code

## Project Structure

```
src/
  ApiStitch/                   # Core library (parser, semantic model, emitters, config, type resolver)
  ApiStitch.MSBuild/           # MSBuild task (.props, .targets, custom task)
  ApiStitch.Cli/               # dotnet tool CLI
  ApiStitch.Runtime/           # Shared runtime library for generated code (if needed)
tests/
  ApiStitch.Tests/             # Unit tests
  ApiStitch.IntegrationTests/  # End-to-end generation tests against real specs
samples/
  Sample.RefitOutput/          # Refit interface output demo
  Sample.TypedClient/          # Typed HttpClient output demo
  Sample.SharedModels/         # Type reuse demo (shared DTO project)
```

## Key Dependencies (allowed)

- Microsoft.OpenApi, Microsoft.OpenApi.Readers
- Scriban
- Microsoft.Build.Utilities.Core (MSBuild task project only)
- Microsoft.CodeAnalysis.CSharp (type discovery only)
- YamlDotNet (configuration parsing)

## Key Dependencies (explicitly excluded)

- Newtonsoft.Json
- NSwag / NJsonSchema (no dependency on NSwag for anything)
- Entity Framework Core
- Any Java/JVM tooling

## Generated Output Dependencies (what consumers install)

- Refit (when using Refit output style)
- Microsoft.Extensions.Http (for IHttpClientFactory / DI extensions)
- Microsoft.Extensions.DependencyInjection.Abstractions
- System.Text.Json (already part of .NET 8+)

## Conventions

- Async all the way — every I/O method returns Task and is awaited
- No reflection — AOT-safe by design
- CancellationToken on every async method in generated output
- [GeneratedCode("ApiStitch")] attribute on generated types (no version number — avoids diff noise)
- Deterministic output — sorted alphabetically, no timestamps, reproducible across runs
- Partial classes on all generated types for extensibility
- YAML for user-facing configuration
- Integration tests generate code from real OpenAPI specs and compile the output
- Warnings as errors in CI
