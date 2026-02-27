# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0-alpha.2] — 2026-02-27

### Fixed

- Source Link enabled for debugger source stepping
- Spec extraction error message now includes stdout fallback and exit code

## [0.1.0-alpha.1] — 2026-02-27

### Added

- **Core generation pipeline** — parse OpenAPI 3.x specs into a semantic model (ApiSpecification, ApiOperation, ApiSchema) and emit typed HttpClient wrappers via Scriban templates
- **Type reuse** — reuse existing C# types from shared libraries via `x-apistitch-type` vendor extensions and namespace include patterns, avoiding duplicate model generation
- **Multi-tag clients** — separate interface + implementation per API tag, registered via a single `AddXxxApi()` DI extension
- **Clean C# 12 output** — records with `required`/`init`, nullable reference types, `partial` classes, `[GeneratedCode]` attribute
- **AOT/trimming compatible** — System.Text.Json source-generated `JsonSerializerContext` for all models
- **Production HTTP patterns** — `IHttpClientFactory`, `CancellationToken` on every method, configurable base address/timeout/headers
- **JSON request/response bodies** — `JsonContent.Create` for requests, `ReadFromJsonAsync` for responses
- **Form-encoded request bodies** — `FormUrlEncodedContent` with schema property flattening
- **Multipart/form-data request bodies** — `MultipartFormDataContent` with `StreamContent` for binary parts, `StringContent`/`JsonContent` for non-binary parts, per-property encoding support
- **Octet-stream request bodies** — `StreamContent` with Content-Type header
- **Plain text request/response bodies** — `StringContent` / `ReadAsStringAsync`
- **Streaming file downloads** — `FileResponse` wrapper with async factory, `IAsyncDisposable` + `IDisposable`, Content-Disposition filename, `HttpCompletionOption.ResponseHeadersRead`
- **Content negotiation** — automatic selection of best content type when multiple are offered, with AS409 diagnostic for skipped alternatives
- **Accept header generation** — set on every request with a response body
- **Content-Type response validation** — guard against non-JSON responses before `ReadFromJsonAsync`, with body preview in exception
- **ProblemDetails deserialization** — RFC 9457 structured error details on `ApiException` for `application/problem+json` responses
- **Parameter style support** — `style`/`explode` parsing with OpenAPI spec defaults per location (query: form/true, path: simple/false, header: simple/false)
- **DeepObject query parameters** — bracket-notation serialization for object-typed query params
- **Comma-separated arrays** — `form` + `explode: false` via `string.Join`
- **Enum query parameters** — wire-value serialization via generated extension methods
- **Inheritance support** — `allOf` composition with base record types
- **CLI tool** — `dotnet tool install ApiStitch.Cli` / `dnx ApiStitch.Cli`, with `generate` command supporting spec file, project reference, config file, and output directory
- **Project-based spec extraction** — point at a `.csproj` and ApiStitch builds it and extracts the OpenAPI spec automatically
- **Diagnostics** — AS400–AS409 warnings for unsupported features (missing operationId, inline schemas, cookie params, unsupported content types, parameter styles, content negotiation)
- **PetStore sample** — end-to-end demo with JSON CRUD, multipart upload (multiple binary + text parts), file download, text/plain health check, array/enum query params, byte[] avatar, type reuse, error handling
- **CI pipeline** — GitHub Actions for build + test on push/PR
- **Release pipeline** — tag-triggered GitHub release + NuGet publish with prerelease support via SemVer suffix

[Unreleased]: https://github.com/Webhooks-Ltd/ApiStitch/compare/v0.1.0-alpha.2...HEAD
[0.1.0-alpha.2]: https://github.com/Webhooks-Ltd/ApiStitch/compare/v0.1.0-alpha.1...v0.1.0-alpha.2
[0.1.0-alpha.1]: https://github.com/Webhooks-Ltd/ApiStitch/releases/tag/v0.1.0-alpha.1
