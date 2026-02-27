## 1. Project File Updates

- [x] 1.1 Update `src/ApiStitch/ApiStitch.csproj`: change `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>` to `<TargetFramework>net10.0</TargetFramework>`, replace conditional `ItemGroup` blocks with single unconditional `Microsoft.OpenApi` 3.x (+ `Microsoft.OpenApi.YamlReader` 3.x), remove `Microsoft.OpenApi.Readers` reference
- [x] 1.2 Update `src/ApiStitch.Cli/ApiStitch.Cli.csproj`: change `<TargetFramework>net8.0</TargetFramework>` to `<TargetFramework>net10.0</TargetFramework>`
- [x] 1.3 Update `src/ApiStitch.OpenApi/ApiStitch.OpenApi.csproj`: change `<TargetFrameworks>net9.0;net10.0</TargetFrameworks>` to `<TargetFramework>net10.0</TargetFramework>`, replace conditional `ItemGroup` blocks with single unconditional references to `Microsoft.AspNetCore.OpenApi` 10.x and `Microsoft.Extensions.ApiDescription.Server` 10.x
- [x] 1.4 Update `tests/ApiStitch.Tests/ApiStitch.Tests.csproj`: change `<TargetFramework>net8.0</TargetFramework>` to `<TargetFramework>net10.0</TargetFramework>`
- [x] 1.5 Update `tests/ApiStitch.IntegrationTests/ApiStitch.IntegrationTests.csproj`: change `<TargetFramework>net8.0</TargetFramework>` to `<TargetFramework>net10.0</TargetFramework>`
- [x] 1.6 Update `tests/ApiStitch.OpenApi.Tests/ApiStitch.OpenApi.Tests.csproj`: change `<TargetFramework>net9.0</TargetFramework>` to `<TargetFramework>net10.0</TargetFramework>`, update `Microsoft.AspNetCore.Mvc.Testing` and `Microsoft.AspNetCore.OpenApi` to 10.x

## 2. OpenApiSpecLoader — Remove v1 Code Path

- [x] 2.1 Remove `#if NET9_0_OR_GREATER` / `#else` / `#endif` blocks from using directives — keep only `using Microsoft.OpenApi;`
- [x] 2.2 Remove `#if` dispatch in `Load()` — call the v3 parser path directly
- [x] 2.3 Delete `LoadV1()` method entirely (lines 28–81)
- [x] 2.4 Inline the v3 parser path as the body of `Load()` — remove the method wrapper and `#if NET9_0_OR_GREATER` / `#endif` guards

## 3. OperationTransformer — Remove v1 Code Path

- [x] 3.1 Remove `#if NET9_0_OR_GREATER` / `#else` / `#endif` from using directives — keep only `using Microsoft.OpenApi;`
- [x] 3.2 Remove `#if` dispatch in `TransformAll` iteration — use `operationType.Method` directly to call string-based `MapHttpMethod`
- [x] 3.3 Delete the `OperationType` enum-based `MapHttpMethod(OperationType)` overload (lines 601–616)
- [x] 3.4 Remove `#if NET9_0_OR_GREATER` / `#endif` guards around the string-based `MapHttpMethod(string)` method — keep the method, remove the guards

## 4. SchemaTransformer — Remove v1 Code Path and Fix Extension Reading

- [x] 4.1 Remove `#if NET9_0_OR_GREATER` / `#else` / `#endif` from using directives — keep only `using Microsoft.OpenApi;`, add `using System.Text.Json.Nodes;`
- [x] 4.2 Update vendor extension reading in `TransformSchema` — replace `OpenApiString` cast with v3 `JsonNodeExtension` / `JsonValue` extraction
- [x] 4.3 Update enum member value extraction in `TransformEnum` — replace `OpenApiString` cast with v3 extraction (`JsonValue.ToString()` or equivalent)

## 5. ApiStitchTypeInfoSchemaTransformer — Remove Conditional Compilation

- [x] 5.1 Remove `#if NET10_0_OR_GREATER` / `#else` / `#endif` from using directives — keep `using Microsoft.OpenApi;`, remove `using Microsoft.OpenApi.Any;`
- [x] 5.2 Remove `#if NET10_0_OR_GREATER` / `#else` / `#endif` from `TransformAsync` body — keep only the `JsonNodeExtension` code path

## 6. Test Helper Migration

- [x] 6.1 Update `SchemaTransformerTests.ParseYaml` — replace `OpenApiStringReader().Read(yaml, out diagnostic)` with `OpenApiDocument.Parse(yaml).Document`, remove `using Microsoft.OpenApi.Readers;` and `using Microsoft.OpenApi.Models;`, add `using Microsoft.OpenApi;`
- [x] 6.2 Update `OperationTransformerTests.ParseYaml` — same migration as 6.1
- [x] 6.3 Update `InheritanceDetectorTests.ParseYaml` — same migration as 6.1, also remove fully-qualified `Microsoft.OpenApi.Models.OpenApiDocument` return type

## 7. Verify

- [x] 7.1 Build the full solution — `dotnet build` succeeds with no warnings from conditional compilation
- [x] 7.2 Run all tests — `dotnet test` passes
- [x] 7.3 Verify no remaining `#if NET` directives in `src/` or `tests/` — grep confirms zero matches
- [x] 7.4 Verify no remaining references to `Microsoft.OpenApi.Readers`, `OpenApiStreamReader`, `OpenApiStringReader`, `OpenApiString`, or `Microsoft.OpenApi.Any` — grep confirms zero matches
