# ApiStitch

[![CI](https://github.com/Webhooks-Ltd/ApiStitch/actions/workflows/ci.yml/badge.svg)](https://github.com/Webhooks-Ltd/ApiStitch/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/ApiStitch.Cli.svg)](https://www.nuget.org/packages/ApiStitch.Cli)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-purple)

A .NET OpenAPI client generator with first-class type reuse. Generates typed HttpClient wrappers from OpenAPI specs, with the ability to reuse existing C# types from shared libraries instead of generating duplicates.

## Features

- **First-class type reuse** — whitelist namespaces and types from your shared libraries; ApiStitch reuses them in the generated client instead of emitting duplicates
- **Namespace remapping** — remap producer namespaces to consumer namespaces when the two differ
- **Project-based spec extraction** — point at a `.csproj` and ApiStitch builds it and extracts the OpenAPI spec automatically
- **Multi-tag clients** — separate client interface per API tag, backed by a single named `HttpClient`
- **Clean C# 12 output** — records, `required`, `init`, nullable reference types, partial classes
- **AOT/trimming compatible** — System.Text.Json source generation, no reflection
- **Production HTTP patterns** — `IHttpClientFactory`, `CancellationToken` on every method, DI registration via `IServiceCollection` extension

## How It Works

ApiStitch has two halves:

**Producer side** (`ApiStitch.OpenApi`) — an ASP.NET Core schema transformer that enriches your OpenAPI spec with `x-apistitch-type` vendor extensions containing CLR type names.

**Consumer side** (`ApiStitch.Cli`) — a CLI tool that reads the enriched spec, resolves which types to reuse vs. generate, and emits typed HttpClient wrappers.

## Quick Start

### 1. Producer: enrich the OpenAPI spec

In your ASP.NET Core API project, install `ApiStitch.OpenApi` (project reference for now) and register the schema transformer:

```csharp
builder.Services.AddOpenApi(options => options.AddApiStitchTypeInfo());
```

This writes `x-apistitch-type` extensions onto your OpenAPI schemas at document generation time.

### 2. Consumer: configure and generate

Create `openapi-stitch.yaml` in your client project:

```yaml
project: ../MyApi/MyApi.csproj
namespace: MyClient.Generated
clientName: MyApi
typeReuse:
  includeNamespaces:
    - "MyShared.Models.*"
```

Run the CLI:

```bash
dotnet run --project path/to/ApiStitch.Cli -- generate --config openapi-stitch.yaml --output Generated
```

You can also point directly at a spec file instead of a project:

```yaml
spec: path/to/openapi.json
namespace: MyClient.Generated
clientName: MyApi
```

`spec` can also be a full HTTP(S) URL, for example:

```yaml
spec: https://petstore3.swagger.io/api/v3/openapi.json
namespace: MyClient.Generated
clientName: MyApi
```

### 3. Use the generated client

```csharp
services.AddMyApi(options =>
{
    options.BaseAddress = new Uri("https://api.example.com");
});

// Inject per-tag clients
var petsClient = provider.GetRequiredService<IMyApiPetsClient>();
var pets = await petsClient.ListPetsAsync();
```

## Configuration Reference

All options in `openapi-stitch.yaml`:

| Key | Description | Default |
|-----|-------------|---------|
| `spec` | Path or HTTP(S) URL to OpenAPI spec (mutually exclusive with `project`) | |
| `project` | Path to `.csproj` that produces an OpenAPI spec at build time | |
| `namespace` | C# namespace for generated types | `ApiStitch.Generated` |
| `outputDir` | Output directory for generated files | `./Generated` |
| `outputStyle` | Output style (`TypedClient`) | `TypedClient` |
| `clientName` | Client name override (derived from spec title if omitted) | |
| `typeReuse.includeNamespaces` | Glob patterns for namespaces to reuse (e.g., `MyShared.Models.*`) | `[]` |
| `typeReuse.includeTypes` | Exact fully-qualified type names to reuse | `[]` |
| `typeReuse.excludeNamespaces` | Glob patterns for namespaces to exclude (overrides includes) | `[]` |
| `typeReuse.excludeTypes` | Exact type names to exclude (overrides includes) | `[]` |
| `typeReuse.namespaceMap` | Namespace remapping (e.g., `ProducerNs: ConsumerNs`) | `{}` |

CLI flags (`--spec`, `--output`, `--namespace`, `--client-name`, `--output-style`) override the corresponding YAML values.

For remote `spec` URLs, ApiStitch applies a bounded fetch policy (30s timeout, 10 MiB response limit, max 5 redirects) and reports fetch/URL errors via diagnostics.

## Sample

The [`samples/PetStore/`](samples/PetStore/) directory contains an end-to-end example with three projects:

- **PetStore.SharedModels** — shared model types (`Pet`, `Owner`, `PetStatus`) referenced by both API and client
- **PetStore.Api** — ASP.NET Core API using both Minimal APIs (Pets) and MVC controllers (Owners), with `AddApiStitchTypeInfo()` registered
- **PetStore.Client** — generated typed client that reuses `PetStore.SharedModels` types and generates only API-local types (`CreatePetRequest`)

The sample demonstrates partial type reuse: shared models pass through unchanged while request types that only exist in the API are generated fresh.

## Project Structure

```
src/ApiStitch/          Core library (parser, semantic model, emitters, config)
src/ApiStitch.Cli/      CLI tool (apistitch generate)
src/ApiStitch.OpenApi/  Producer-side ASP.NET Core integration (x-apistitch-type enrichment)
tests/                  Unit and integration tests
samples/PetStore/       End-to-end sample (SharedModels + API + Client)
```

## Build from Source

```bash
git clone https://github.com/Webhooks-Ltd/ApiStitch.git
cd ApiStitch
dotnet build
dotnet test
```

Requires the .NET 10 SDK.

## Current Status

ApiStitch is under active development. What works today:

- CLI generation (`apistitch generate`)
- TypedClient output style (interfaces + implementations + DI registration)
- Producer-side schema enrichment (`ApiStitch.OpenApi`)
- Type reuse via include/exclude whitelist
- Namespace remapping
- Project-based spec extraction
- Multi-tag client generation
