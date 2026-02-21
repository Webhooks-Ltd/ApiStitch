# ApiStitch

A .NET OpenAPI client generator with first-class type reuse, clean idiomatic output, and zero-config MSBuild integration.

## What This Is

Open-source alternative to Kiota, NSwag, and Refitter. Core differentiators:
- First-class type reuse (map OpenAPI schemas to your existing C# types)
- Clean, idiomatic C# output (records, required, nullable reference types, init setters)
- Multiple output styles (Refit interfaces, typed HttpClient wrappers, extension methods)
- Zero-config MSBuild integration (add NuGet package, point to spec, build)
- System.Text.Json source generation for AOT/trimming compatibility
- Production-ready HTTP patterns (IHttpClientFactory, IHttpClientBuilder, CancellationToken everywhere)

## Tech Stack

- .NET 8+, C# 12
- Microsoft.OpenApi for OpenAPI spec parsing
- Scriban for code templating
- System.Text.Json (generated output uses STJ source generation)
- MSBuild custom task for build integration
- Roslyn for type discovery (attribute-based type reuse)

## Project Structure

- `docs/strategy/` — product brief, assumptions, feature prioritization, positioning
- `src/ApiStitch/` — core library (parser, semantic model, emitters, config)
- `src/ApiStitch.MSBuild/` — MSBuild task integration (.props/.targets)
- `src/ApiStitch.Cli/` — dotnet tool CLI
- `tests/` — unit and integration tests
- `samples/` — sample projects demonstrating each output style

## Key Architecture Decisions

- Semantic model approach — parse OpenAPI into a language-agnostic API surface model, then emit per-style
- MSBuild task as primary delivery — generated code in obj/, incremental builds via Inputs/Outputs
- Type reuse via three strategies: explicit YAML mapping, namespace exclusion, [OpenApiSchema] attribute discovery
- Thin generated code + runtime library — generated code is declarations, HTTP mechanics in shared package
- Partial classes on all generated types for extensibility
- Deterministic, diff-friendly output (sorted alphabetically, no timestamps, no version in GeneratedCode attribute)

## Reflection Policy

Avoid reflection. The generated output must be AOT/trimming compatible:
- System.Text.Json with source-generated JsonSerializerContext
- No runtime type discovery in generated code
- Roslyn analysis (not reflection) for attribute-based type discovery during generation

## Dependencies (allowed)

- Microsoft.OpenApi, Microsoft.OpenApi.Readers
- Scriban (templating engine)
- Microsoft.Build.Utilities.Core (MSBuild task)
- Microsoft.CodeAnalysis (Roslyn, for type discovery)
- System.Text.Json
- Microsoft.Extensions.Http, DependencyInjection, Options Abstractions (in generated output runtime)

## Dependencies (explicitly banned)

- Newtonsoft.Json
- NSwag (no dependency on NSwag for model generation)
- Entity Framework Core
- Any Java/JVM tooling

## Conventions

- Async all the way — every I/O method returns Task
- No comments unless absolutely necessary
- CancellationToken on every async method in generated output
- Generated files include [GeneratedCode("ApiStitch")] attribute (no version number to avoid diff noise)
- YAML for configuration (openapi-stitch.yaml)
- kebab-case for CLI commands, PascalCase for generated C# code
- Integration tests against real OpenAPI specs
- Warnings as errors in CI
