## Why

ApiStitch consumes OpenAPI spec files that have already lost CLR type information. To enable type reuse — our core differentiator — consumers need to know the original .NET type names behind each schema. Today there is no way to carry that information through a spec file. A lightweight producer-side package that enriches the spec with `x-apistitch-type` extensions solves this, and pairs naturally with `Microsoft.Extensions.ApiDescription.Server` for zero-config build-time spec generation.

## What Changes

- New NuGet package `ApiStitch.OpenApi` targeting ASP.NET Core OpenAPI (`Microsoft.AspNetCore.OpenApi`)
- Provides an `IOpenApiSchemaTransformer` that writes `x-apistitch-type` with `Type.FullName` onto component schemas
- Provides `ApiStitchDetection.IsOpenApiGenerationOnly` static property to detect build-time spec generation (via `GetDocument.Insider` entry assembly check)
- Configurable: extension emission can be toggled on/off, but always emits when running under `GetDocument.Insider` (build-time generation)
- Registration via `options.AddApiStitchTypeInfo()` extension method on `OpenApiOptions`

## Capabilities

### New Capabilities
- `openapi-type-enrichment`: Schema transformer that writes `x-apistitch-type` CLR type names into OpenAPI specs, plus the `IsOpenApiGenerationOnly()` startup guard helper

### Modified Capabilities

## Impact

- New project `src/ApiStitch.OpenApi/` added to solution
- New dependency on `Microsoft.AspNetCore.OpenApi` (peer dependency — the API project already has this)
- No changes to the core `ApiStitch` library (consumer-side `x-apistitch-type` reading will be a separate change)
- Sample API project (`samples/SampleApi/`) updated to demonstrate usage
