# ApiStitch

[![CI](https://github.com/Webhooks-Ltd/ApiStitch/actions/workflows/ci.yml/badge.svg)](https://github.com/Webhooks-Ltd/ApiStitch/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-purple)

A .NET OpenAPI client generator with first-class type reuse, clean idiomatic output, and zero-config MSBuild integration.

## Features

- **First-class type reuse** — map OpenAPI schemas to your existing C# types
- **Clean, idiomatic C# output** — records, required properties, nullable reference types, init setters
- **Multiple output styles** — typed HttpClient wrappers, extension methods, Refit interfaces
- **Zero-config MSBuild integration** — add the NuGet package, point to your spec, build
- **AOT/trimming compatible** — System.Text.Json source generation, no reflection
- **Production-ready HTTP patterns** — IHttpClientFactory, IHttpClientBuilder, CancellationToken everywhere

## Quick Start

```bash
# Install the CLI tool
dotnet tool install --global ApiStitch.Cli

# Generate a client from an OpenAPI spec
apistitch generate --spec petstore.yaml --output ./Generated
```

Or add the NuGet package for MSBuild integration:

```bash
dotnet add package ApiStitch.OpenApi
```

## Build from Source

```bash
git clone https://github.com/Webhooks-Ltd/ApiStitch.git
cd ApiStitch
dotnet build
dotnet test
```

## Project Structure

```
src/ApiStitch/          Core library (parser, semantic model, emitters, config)
src/ApiStitch.Cli/      dotnet tool CLI
src/ApiStitch.OpenApi/  MSBuild task integration
tests/                  Unit and integration tests
samples/                Sample projects demonstrating each output style
```

## License

[MIT](LICENSE)
