## 1. Project Setup

- [x] 1.1 Create `src/ApiStitch.OpenApi/ApiStitch.OpenApi.csproj` targeting `net9.0;net10.0` with `FrameworkReference Include="Microsoft.AspNetCore.App"`, version-conditioned `PackageReference` for `Microsoft.AspNetCore.OpenApi` (9.0.* for net9.0, 10.0.* for net10.0), `InternalsVisibleTo Include="ApiStitch.OpenApi.Tests"`, and NuGet package metadata (`PackageId`, `Description`, `Authors`, `PackageTags`, `PackageLicenseExpression`)
- [x] 1.2 Add `src/ApiStitch.OpenApi/` project to the solution file
- [x] 1.3 Verify `dotnet build src/ApiStitch.OpenApi/ApiStitch.OpenApi.csproj` succeeds for both target frameworks

## 2. Options and Detection

- [x] 2.1 Create `ApiStitchTypeInfoOptions` class with `bool AlwaysEmit { get; set; } = false`
- [x] 2.2 Create `ApiStitchDetection` static class with `IsOpenApiGenerationOnly` static property that checks `Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider"`

## 3. Schema Transformer

- [x] 3.1 Create `ApiStitchTypeInfoSchemaTransformer` implementing `IOpenApiSchemaTransformer` with constructor accepting `ApiStitchTypeInfoOptions`
- [x] 3.2 Implement `IsUserDefinedType` internal static method: unwrap `Nullable<T>`, skip arrays, skip primitives (`IsPrimitive`), skip well-known types (`string`, `decimal`, `object`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid`, `Uri`, `Half`), skip collection generic types via `CollectionDefinitions` (`List<>`, `IList<>`, `ICollection<>`, `IEnumerable<>`, `IReadOnlyList<>`, `IReadOnlyCollection<>`, `HashSet<>`, `ISet<>`, `IReadOnlySet<>`, `Dictionary<,>`, `IDictionary<,>`, `IReadOnlyDictionary<,>`), skip null `FullName`; user-defined closed generics (e.g., `PagedResult<Pet>`) pass the filter
- [x] 3.3 Implement `GetCleanFullName` internal static method: for non-generic types return `Type.FullName`; for closed generic types strip the arity suffix and assembly-qualified type args, producing clean C#-style format (e.g., `MyNamespace.PagedResult<MyNamespace.Pet>`); return null if any component FullName is null
- [x] 3.4 Implement `TransformAsync`: check `IsUserDefinedType`, check emission rules (always emit if `GetDocument.Insider`, else only if `AlwaysEmit`), get type name via `GetCleanFullName`, write `x-apistitch-type` extension with conditional compilation (`OpenApiString` for net9.0, `JsonNode.Parse` for net10.0)

## 4. Registration Extension Method

- [x] 4.1 Create `OpenApiOptionsExtensions` static class with `AddApiStitchTypeInfo(this OpenApiOptions options)` overload (default options)
- [x] 4.2 Add `AddApiStitchTypeInfo(this OpenApiOptions options, Action<ApiStitchTypeInfoOptions> configure)` overload that applies configuration before registering the transformer

## 5. Unit Tests

- [x] 5.1 Create `tests/ApiStitch.OpenApi.Tests/ApiStitch.OpenApi.Tests.csproj` targeting `net9.0` with references to `ApiStitch.OpenApi`, xUnit, and FluentAssertions
- [x] 5.2 Add unit tests for `IsUserDefinedType`: user-defined class returns true, user-defined enum returns true, user-defined struct returns true, closed generic type (`PagedResult<Pet>`) returns true, primitive types return false, well-known types (including `Half`) return false, nullable wrappers are unwrapped, collection generic types (`List<Pet>`, `Dictionary<string, Pet>`) return false, non-collection closed generics are not skipped, arrays return false, null FullName returns false
- [x] 5.3 Add unit tests for `GetCleanFullName`: non-generic type returns `Type.FullName`, closed generic returns clean format without assembly info (e.g., `Namespace.PagedResult<Namespace.Pet>`), nested generic type arguments are resolved recursively, null FullName returns null
- [x] 5.4 Add unit tests for `ApiStitchDetection.IsOpenApiGenerationOnly`: verify returns false under normal test execution
- [x] 5.5 Add unit tests for `ApiStitchTypeInfoOptions`: verify `AlwaysEmit` defaults to false

## 6. Integration Tests

- [x] 6.1 Add `Microsoft.AspNetCore.TestHost` package reference to the test project; create an integration test that boots a minimal API with `AddApiStitchTypeInfo(o => o.AlwaysEmit = true)` and sample endpoints, fetches the OpenAPI spec, and verifies `x-apistitch-type` extensions appear on user-defined schemas (`Pet`, `PetStatus`) but not on primitive/collection schemas
- [x] 6.2 Add integration test verifying that with default options (no `AlwaysEmit`), runtime-served spec does NOT contain `x-apistitch-type` extensions

## 7. Sample API Project

- [x] 7.1 Create `samples/SampleApi/SampleApi.csproj` targeting `net9.0` with project reference to `ApiStitch.OpenApi` and `Microsoft.Extensions.ApiDescription.Server` package reference
- [x] 7.2 Create sample model types: `Pet` class with `Id`, `Name`, `Status` properties; `PetStatus` enum; `CreatePetRequest` class
- [x] 7.3 Create minimal API `Program.cs` with `AddOpenApi` + `AddApiStitchTypeInfo()` registration, `IsOpenApiGenerationOnly` guard pattern for a fake heavy dependency, and a few sample endpoints returning `Pet`/`PetStatus`/`CreatePetRequest`
- [x] 7.4 Add `samples/SampleApi/` project to the solution file
- [x] 7.5 Verify `dotnet build samples/SampleApi/SampleApi.csproj` succeeds and generates an OpenAPI spec containing `x-apistitch-type` extensions on `Pet`, `PetStatus`, and `CreatePetRequest` schemas
