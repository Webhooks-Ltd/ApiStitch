## Purpose

Define producer-side OpenAPI schema enrichment behavior that writes `x-apistitch-type` CLR type hints used by consumer-side type reuse.

## Requirements

### Requirement: Schema transformer writes x-apistitch-type extension
The `ApiStitch.OpenApi` package SHALL provide an `IOpenApiSchemaTransformer` implementation that writes an `x-apistitch-type` vendor extension onto OpenAPI schemas. The extension value SHALL be the CLR `Type.FullName` (namespace-qualified, no assembly name).

#### Scenario: User-defined class gets annotated
- **WHEN** the API exposes a `SampleApi.Models.Pet` class as a schema
- **THEN** the generated OpenAPI spec contains `"x-apistitch-type": "SampleApi.Models.Pet"` on the Pet schema

#### Scenario: User-defined enum gets annotated
- **WHEN** the API exposes a `SampleApi.Models.PetStatus` enum as a schema
- **THEN** the generated OpenAPI spec contains `"x-apistitch-type": "SampleApi.Models.PetStatus"` on the PetStatus schema

#### Scenario: Framework domain type gets annotated
- **WHEN** the API exposes `Microsoft.AspNetCore.Mvc.ProblemDetails` as a response schema
- **THEN** the generated OpenAPI spec contains `"x-apistitch-type": "Microsoft.AspNetCore.Mvc.ProblemDetails"` on the ProblemDetails schema

#### Scenario: Primitive types are skipped
- **WHEN** the API exposes a property of type `string`, `int`, `bool`, or other CLR primitive
- **THEN** no `x-apistitch-type` extension is written for that schema

#### Scenario: Well-known value types are skipped
- **WHEN** the API exposes a property of type `DateTime`, `DateTimeOffset`, `Guid`, `TimeSpan`, `DateOnly`, `TimeOnly`, `Uri`, or `decimal`
- **THEN** no `x-apistitch-type` extension is written for that schema

#### Scenario: Collections are skipped
- **WHEN** the API exposes a `List<Pet>`, `IReadOnlyList<Pet>`, or `Pet[]` type
- **THEN** no `x-apistitch-type` extension is written for the collection schema (the element type `Pet` gets its own annotation)

#### Scenario: User-defined closed generic type gets annotated
- **WHEN** the API exposes a `MyNamespace.PagedResult<MyNamespace.Pet>` closed generic type as a schema
- **THEN** the generated OpenAPI spec contains `"x-apistitch-type": "MyNamespace.PagedResult<MyNamespace.Pet>"` on the PagedResult schema

#### Scenario: User-defined struct gets annotated
- **WHEN** the API exposes a `SampleApi.Models.Money` struct as a schema
- **THEN** the generated OpenAPI spec contains `"x-apistitch-type": "SampleApi.Models.Money"` on the Money schema

#### Scenario: Nullable wrappers are unwrapped
- **WHEN** the API exposes a `Nullable<PetStatus>` (`PetStatus?`) type
- **THEN** the extension is evaluated against the underlying type `PetStatus`, not `Nullable<PetStatus>`

#### Scenario: Type.FullName is null
- **WHEN** the CLR type has a null `FullName` (open generic parameters, compiler-generated types)
- **THEN** no `x-apistitch-type` extension is written for that schema

### Requirement: Configurable emission with build-time always-on
The transformer SHALL always emit `x-apistitch-type` during build-time spec generation (when the entry assembly is `GetDocument.Insider`). At runtime, emission SHALL be controlled by the `AlwaysEmit` option, which defaults to `false`.

#### Scenario: Build-time generation always emits
- **WHEN** the spec is generated at build time via `Microsoft.Extensions.ApiDescription.Server`
- **THEN** `x-apistitch-type` extensions are written regardless of the `AlwaysEmit` setting

#### Scenario: Runtime emission defaults to off
- **WHEN** the spec is served at runtime (e.g., `/openapi/v1.json`) and `AlwaysEmit` is not configured
- **THEN** no `x-apistitch-type` extensions are written

#### Scenario: Runtime emission opt-in
- **WHEN** the spec is served at runtime and `AlwaysEmit` is set to `true`
- **THEN** `x-apistitch-type` extensions are written

### Requirement: Registration via AddApiStitchTypeInfo extension method
The package SHALL provide `AddApiStitchTypeInfo()` as an extension method on `OpenApiOptions` for registering the schema transformer. An overload accepting `Action<ApiStitchTypeInfoOptions>` SHALL be provided for configuration.

#### Scenario: Default registration
- **WHEN** the developer calls `options.AddApiStitchTypeInfo()`
- **THEN** the schema transformer is registered with default options (`AlwaysEmit = false`)

#### Scenario: Configured registration
- **WHEN** the developer calls `options.AddApiStitchTypeInfo(o => o.AlwaysEmit = true)`
- **THEN** the schema transformer is registered with `AlwaysEmit = true`

### Requirement: IsOpenApiGenerationOnly static detection helper
The package SHALL provide `ApiStitchDetection.IsOpenApiGenerationOnly` as a static boolean property that returns `true` when the process is running under `GetDocument.Insider` (build-time spec generation) and `false` otherwise.

#### Scenario: Build-time detection
- **WHEN** the API project is built with `Microsoft.Extensions.ApiDescription.Server` triggering spec generation
- **THEN** `ApiStitchDetection.IsOpenApiGenerationOnly` returns `true`

#### Scenario: Normal runtime detection
- **WHEN** the API project runs normally (e.g., `dotnet run`)
- **THEN** `ApiStitchDetection.IsOpenApiGenerationOnly` returns `false`

### Requirement: Multi-target net9.0 and net10.0
The package SHALL target both `net9.0` and `net10.0`. The implementation SHALL use conditional compilation (`#if NET10_0_OR_GREATER`) to handle the Microsoft.OpenApi v1 (net9.0) vs v2 (net10.0) API differences for writing schema extensions.

#### Scenario: Package builds for net9.0
- **WHEN** the API project targets `net9.0`
- **THEN** the package uses `Microsoft.OpenApi` 1.x types (`OpenApiString`) to write the extension

#### Scenario: Package builds for net10.0
- **WHEN** the API project targets `net10.0`
- **THEN** the package uses `Microsoft.OpenApi` 2.x types (`JsonNode`) to write the extension

### Requirement: Sample API demonstrates usage
The `samples/SampleApi/` project SHALL reference `ApiStitch.OpenApi` and demonstrate the `AddApiStitchTypeInfo()` registration. The sample SHALL include the `IsOpenApiGenerationOnly` guard pattern for heavy dependencies.

#### Scenario: Sample builds and generates enriched spec
- **WHEN** `dotnet build samples/SampleApi/SampleApi.csproj` is run
- **THEN** the generated OpenAPI spec contains `x-apistitch-type` extensions on `Pet`, `PetStatus`, and `CreatePetRequest` schemas
