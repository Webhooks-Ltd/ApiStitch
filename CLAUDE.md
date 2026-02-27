# ApiStitch

A .NET OpenAPI client generator with first-class type reuse and clean idiomatic output.

## What This Is

Open-source alternative to Kiota, NSwag, and Refitter. Core differentiators:
- First-class type reuse (map OpenAPI schemas to your existing C# types via `x-apistitch-type` vendor extensions)
- Clean, idiomatic C# output (records, required, nullable reference types, init setters)
- Typed HttpClient wrappers with multi-tag client generation
- System.Text.Json source generation for AOT/trimming compatibility
- Production-ready HTTP patterns (IHttpClientFactory, CancellationToken everywhere)
- Comprehensive content type support (JSON, form-encoded, multipart, octet-stream, text/plain)
- Streaming file downloads via FileResponse, ProblemDetails on ApiException

## Tech Stack

- .NET 10, C# 12
- Microsoft.OpenApi v3.x for OpenAPI spec parsing
- Scriban for code templating
- System.Text.Json (generated output uses STJ source generation)
- Roslyn for type discovery (type reuse via `x-apistitch-type` extensions)

## Project Structure

- `docs/strategy/` — product brief, assumptions, feature prioritization, positioning
- `src/ApiStitch/` — core library (parser, semantic model, emitters, config)
- `src/ApiStitch.OpenApi/` — ASP.NET Core schema transformer (enriches specs with `x-apistitch-type`)
- `src/ApiStitch.Cli/` — dotnet tool CLI (`dotnet tool install ApiStitch.Cli` / `dnx ApiStitch.Cli`)
- `tests/` — unit and integration tests
- `samples/PetStore/` — end-to-end sample (API + shared models + generated client)

## Key Architecture Decisions

- Semantic model approach — parse OpenAPI into ApiSpecification/ApiOperation/ApiSchema, then emit via Scriban templates
- Type reuse via namespace include patterns matching `x-apistitch-type` vendor extensions
- Partial classes on all generated types for extensibility
- Deterministic, diff-friendly output (sorted alphabetically, no timestamps, no version in GeneratedCode attribute)
- ContentKind enum (Json, FormUrlEncoded, MultipartFormData, OctetStream, PlainText) drives template branching
- ParameterStyle/Explode on ApiParameter for inline query serialization (form, deepObject, comma-separated)
- FileResponse for streaming downloads (async factory, IAsyncDisposable)
- ProblemDetails deserialization on ApiException for RFC 9457 error responses

## Reflection Policy

Avoid reflection. The generated output must be AOT/trimming compatible:
- System.Text.Json with source-generated JsonSerializerContext
- No runtime type discovery in generated code
- Roslyn analysis (not reflection) for type discovery during generation

## Dependencies (allowed)

- Microsoft.OpenApi, Microsoft.OpenApi.YamlReader
- Scriban (templating engine)
- Microsoft.CodeAnalysis (Roslyn, for type discovery)
- System.Text.Json
- System.CommandLine (CLI)
- Microsoft.Extensions.Http, DependencyInjection abstractions (in generated output runtime)

## Dependencies (explicitly banned)

- Newtonsoft.Json
- NSwag (no dependency on NSwag for model generation)
- Entity Framework Core
- Any Java/JVM tooling

## Release Process

- Tag `v*` triggers `.github/workflows/release.yml`
- Stable: `v1.0.0` → full release on GitHub + NuGet
- Prerelease: `v1.0.0-alpha.1` → prerelease on both (detected from SemVer suffix)
- Version extracted from tag, passed to `dotnet pack /p:Version=...`
- Requires `NUGET_API_KEY` secret

## Commit Convention

Use [Conventional Commits](https://www.conventionalcommits.org/):

| Prefix | When |
|---|---|
| `feat:` | New capability / public API |
| `fix:` | Bug fix |
| `docs:` | README, XML docs |
| `chore:` | CI, build, housekeeping |
| `refactor:` | Internal restructuring |
| `test:` | Tests only |
| `feat!:` / `fix!:` | Breaking change |

Lowercase after prefix, imperative mood, under 72 chars. Optional scope: `feat(generator): add inheritance support`.

## Conventions

- Async all the way — every I/O method returns Task
- XML doc comments on all public types, members, and parameters
- No inline comments unless absolutely necessary
- CancellationToken on every async method in generated output
- Generated files include [GeneratedCode("ApiStitch")] attribute (no version number to avoid diff noise)
- YAML for configuration (openapi-stitch.yaml)
- kebab-case for CLI commands, PascalCase for generated C# code
- Integration tests against real OpenAPI specs
- Warnings as errors in CI
