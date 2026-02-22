# ApiStitch — Feature Prioritization Matrix

**Artifact**: 3 of 5
**Status**: Draft
**Date**: 2026-02-21
**Author**: Mark (solo developer)
**Depends on**: [01 — Product Brief](./01-product-brief.md)

---

## Prioritization Method

Features are ranked using a weighted combination of three factors:

1. **Demand Signal** — NuGet download proxies, GitHub issue frequency on competing tools, Reddit/forum discussions, real-world team patterns. Hard numbers where available, qualitative assessment where not.
2. **Differentiation Value** — Does this feature directly attack a documented Kiota/NSwag/OpenAPI Generator weakness? Would someone switch for this alone?
3. **Implementation Cost** — Estimated effort at ~20hr/week with heavy AI assistance. Rated as S/M/L/XL.

Features are placed into four tiers. The guiding principle: **nail the type reuse story and one clean output style first, then expand.** One output style + type mapping + MSBuild integration + the common 80% of OpenAPI schemas. Everything else is earned through adoption, not assumed through ambition.

---

## Tier 1: MMVP (Weeks 1-8)

**Goal**: The smallest thing that validates the core hypothesis — "developers will adopt a .NET OpenAPI client generator built around type reuse and clean output." Shippable enough for a blog post and a Reddit drop. Embarrassingly small on purpose.

**The MMVP answers one question**: If I show a .NET developer a NuGet package that takes an OpenAPI spec, produces clean typed HttpClient wrappers with zero third-party runtime deps, maps schemas to their existing C# types, and runs on every build — do they care?

| # | Feature | Description | Rationale |
|---|---------|-------------|-----------|
| 1.1 | **Typed HttpClient Wrappers** | Generate interface + concrete class pairs with `HttpClient` injection. Full IHttpClientFactory integration. The typed client pattern Microsoft recommends for production HTTP clients. | Typed HttpClient wrappers are the Microsoft-recommended pattern, require zero third-party runtime dependencies, and are fully AOT-compatible. Starting with typed HttpClient output means the MMVP has no disqualifying runtime dependency. The generated code uses IHttpClientFactory directly — no Castle.DynamicProxy, no Refit runtime. |
| 1.2 | **Model Generation (Records, Modern C#)** | Generate C# records with `required` properties, `init` setters, nullable reference types, `System.Text.Json` attributes. File-scoped namespaces. No preprocessor directives. | The opposite of Kiota's output. Every generated type should read like code a developer would write in a .NET 8+ project. Records over classes (immutable by default for API responses). `required` over `[Required]` attributes. Native nullable (`string?`) over `#if NETSTANDARD2_1_OR_GREATER`. This is the "clean output" differentiator made concrete. |
| 1.3 | **Type Reuse — Explicit YAML Mapping** | Configure `typeMappings: { Address: MyApp.Shared.Address }` in YAML. The generator emits a `using` directive and references the existing type instead of generating a duplicate. | The #1 differentiator. The feature that no other .NET OpenAPI generator provides properly. YAML mapping is the simplest implementation: pure string substitution at the code generation level. No Roslyn needed, no compilation dependency, no circular build issues. Start here, expand to namespace exclusion and attribute discovery later. |
| 1.4 | **Type Reuse — Namespace Exclusion** | Configure `excludeNamespaces: [MyApp.SharedModels]`. All schemas whose mapped type falls in that namespace are skipped; the generator adds a `using` directive. | The complement to explicit mapping for teams with many shared types. Instead of mapping 50 types individually, exclude the entire shared namespace. Implementation is the same mechanism as 1.3 (skip generation, add using) but triggered by namespace match instead of per-type config. |
| 1.5 | **YAML Configuration** | A YAML config file (`apistitch.yaml`) with: spec path/URL, output namespace, output style, type mappings, namespace exclusions. Sensible defaults for everything. | YAML over JSON for readability and comments. The config is the user interface — it must be simple enough that the default (no config, just a spec path) works, and powerful enough that type mapping and namespace exclusion are straightforward. Inspired by graphql-codegen's config design. |
| 1.6 | **MSBuild Task Integration** | NuGet package with `.props`/`.targets`. Add a `<ApiStitchSpec>` item to the csproj, build, get generated code in `obj/`. Incremental builds — only regenerate when the spec or config changes. | The gRPC Grpc.Tools model. This is how code generation should work in .NET: invisible until you need to change it. Generated code in `obj/` means it's never committed, never reviewed, never diffed. The MSBuild task is the primary delivery mechanism — the CLI and source generator are future additions. |
| 1.7 | **System.Text.Json Source Generation** | Generate a `JsonSerializerContext` for all generated models. Models decorated with `[JsonPropertyName]`. Compatible with AOT and trimming. | The only serialization path that works everywhere .NET 8+ runs. Kiota's IParsable is incompatible with System.Text.Json. NSwag maps to Newtonsoft. This is not optional — it's the foundation of the "production-ready output" story. Source gen from day one means the AOT story is true from day one. |
| 1.8 | **OpenAPI 3.0 Core Schema Support** | Handle the common 80%: objects, arrays, primitives, enums, `$ref`, `allOf` (for inheritance/composition), nullable, required/optional. Produce compilable output for real-world specs like Petstore, Stripe (subset), and internal microservice specs. | The OpenAPI spec is enormous. The MMVP does not handle every corner case. It handles what developers actually encounter: objects with properties, references to other schemas, basic inheritance via allOf, arrays, enums, and nullable types. Exotic patterns (oneOf discriminators, recursive schemas, XML) are deferred. Unsupported patterns produce clear comments in the output, not silent failures. |
| 1.9 | **DI Registration Extensions** | Generate `IServiceCollection` extension methods: `services.AddPetStoreClient(Action<HttpClient>)` that return `IHttpClientBuilder` for resilience policy chaining. | Returning `IHttpClientBuilder` (not `void`) is the key detail. It enables `services.AddPetStoreClient(...).AddPolicyHandler(retryPolicy)` — Polly integration without wrapper code. No existing generator does this. The DI extension is the developer's first interaction with the generated code, so it must be clean and discoverable. |
| 1.10 | **CancellationToken on Every Method** | Every generated HTTP method accepts a `CancellationToken` parameter (with default `default`). | Table stakes for production .NET code. This is a design constraint, not a feature to build — but it must be enforced from day one. Kiota is inconsistent about this. NSwag makes it optional via configuration. ApiStitch makes it non-negotiable. |

**MMVP scope boundary**: 10 features. One output style (typed HttpClient). Two type reuse mechanisms (YAML mapping + namespace exclusion). One spec version (OpenAPI 3.0). Core schemas only. No extension method output. No Refit output. No CLI tool. No attribute-based type discovery. No schema-level overrides beyond skip. No pagination/IAsyncEnumerable. The MMVP is: parse a spec, map types, generate typed HttpClient wrappers + models, run on build.

**Estimated effort**: ~120-160 hours (6-8 weeks at 20hr/week). The code generation engine is the hard part (~50%): translating OpenAPI schemas to clean C# with type mapping, nullable handling, and proper formatting. MSBuild integration (~20%). Config parsing and validation (~15%). Testing against real specs (~15%).

---

## Tier 2: MVP (Weeks 9-15)

**Goal**: The smallest complete product a developer would trust for real projects. Adds the second output style, the features that handle production edge cases, and the configuration depth that teams need. The MVP is what earns GitHub stars from people who actually used ApiStitch in a real codebase.

| # | Feature | Description | Rationale |
|---|---------|-------------|-----------|
| 2.1 | **Extension Method Output Style** | Generate `public static async Task<Pet> GetPetAsync(this HttpClient client, int id, CancellationToken ct = default)` extension methods. The lightest-weight output: no interfaces, no DI registration, just methods on HttpClient. | The second output style. For internal service clients where typed client ceremony is overkill. Extension methods are the most discoverable pattern — `httpClient.GetPetAsync()` appears in IntelliSense automatically. Implementation reuses the same operation parsing as typed HttpClient output — only the code emitter differs. |
| 2.2 | **Schema-Level Overrides** | Per-schema configuration in YAML: `schemas: { Pet: { partial: true }, LegacyError: { skip: true }, Status: { enumType: string } }`. Control generation granularity at the individual type level. | Real-world specs have schemas that need special handling. Some types should be partial (so the developer can extend them). Some should be skipped entirely (legacy types that aren't used). Some enums should be strings instead of C# enums (open enums from third-party APIs). This is where ApiStitch's configuration depth differentiates from Refitter's all-or-nothing approach. |
| 2.3 | **Filtering by Tags and Paths** | Configure `filtering: { includeTags: [pets, users] }` or `filtering: { excludePaths: [/internal/*] }` to generate clients for a subset of the API. | Large OpenAPI specs (Stripe has 400+ operations) shouldn't force generation of everything. Teams consuming a partner API may only use 3 of 20 endpoint groups. Tag and path filtering reduces the generated surface area and build time. NSwag has this. Kiota has it. ApiStitch must have it. |
| 2.4 | **Open Enums** | Generate enums that don't throw on unknown values. Either a string-backed enum pattern or a custom struct with predefined constants. | Third-party APIs add enum values without bumping API versions. A generated client that throws `JsonException` on an unknown enum value is a production incident waiting to happen. Kiota handles this with IParsable but the developer experience is poor. NSwag doesn't handle it at all. Open enums are a production-readiness requirement. |
| 2.5 | **Partial Classes on Generated Models** | All generated models are `partial` by default (configurable). Enables developers to add methods, computed properties, and interface implementations to generated types without touching generated files. | The gRPC pattern. Grpc.Tools generates partial classes so developers extend them in separate files. This is especially important for ApiStitch because some developers will want to add `IEquatable<T>`, validation logic, or mapping methods to generated models — without using type reuse for those specific types. |
| 2.6 | **Post-Generation Hooks** | Configure `hooks: { afterGenerate: "dotnet csharpier {outputDir}" }` to run a command after code generation completes. | Borrowed from graphql-codegen and Orval. The most common use case is formatting (CSharpier, dotnet format). Some teams will want to run analyzers or custom transformations. Hooks keep the generator focused on generation while enabling arbitrary post-processing. |
| 2.7 | **OpenAPI 3.1 Support** | Extend the parser to handle OpenAPI 3.1 differences: JSON Schema draft 2020-12 alignment, `type` as array, `null` as a type. | OpenAPI 3.1 adoption is growing. Many newer API specs use 3.1 features. Microsoft.OpenApi has added 3.1 support. Deferring beyond MVP would exclude a growing segment of specs. |
| 2.8 | **CLI Tool (dotnet tool)** | `dotnet tool install ApiStitch.Cli` — generate code from the command line. Same engine as the MSBuild task. Useful for CI/CD debugging, one-off generation, and spec exploration. | The CLI is the escape hatch when MSBuild isn't the right integration point. CI pipelines that pre-generate code. Developers debugging generation issues. Teams evaluating ApiStitch before committing to the NuGet package. Implementation cost is low because the generation engine exists — the CLI is just a different entry point. |
| 2.9 | **IAsyncEnumerable for Paginated Endpoints** | Detect paginated response patterns (Link headers, cursor parameters, offset/limit) and generate `IAsyncEnumerable<T>` wrappers that handle pagination transparently. | The DX leap from "call 5 endpoints to iterate through pages" to `await foreach (var item in client.GetAllPetsAsync())`. No existing .NET OpenAPI generator produces IAsyncEnumerable for pagination. This is a differentiation feature that demos well and solves a real repetitive coding pattern. |
| 2.10 | **Spec URL Download with Caching** | Support `spec: https://api.example.com/openapi.json` in config. Download and cache the spec locally. Regenerate only when the remote spec changes (ETag/Last-Modified). | Teams consuming third-party APIs don't want to manually download spec files. URL support with caching means the build always uses the latest spec without re-downloading on every build. NSwag supports this. Kiota supports this. Table stakes for the "point at a spec and build" story. |

**MVP scope boundary**: 20 total features (10 MMVP + 10 MVP). Two output styles (typed HttpClient + extension methods). Schema-level overrides. Tag/path filtering. Open enums. Partial classes. CLI tool. OpenAPI 3.1. The MVP is what you recommend to a team and say "this handles real-world specs."

**Estimated effort**: ~100-140 additional hours (5-7 weeks at 20hr/week). Extension method output (~25%) shares parsing logic with typed HttpClient but needs a new emitter. Schema overrides and filtering (~20%). OpenAPI 3.1 (~15%). CLI (~10%). Everything else is incremental (~30%).

---

## Tier 3: v1.0 Public Release (Weeks 16-28)

**Goal**: The version you announce at conferences, write migration guides for, and promote as "the .NET OpenAPI client generator." Feature-complete enough that teams adopting ApiStitch don't hit walls for common scenarios. This is when ApiStitch earns "credible alternative to Kiota and NSwag" status.

| # | Feature | Description | Rationale |
|---|---------|-------------|-----------|
| 3.1 | **Refit Interface Generation (Optional)** | Generate Refit interface definitions from OpenAPI operation paths. One interface per tag (or configurable grouping). `[Get("/pets/{id}")]` with typed request/response. Adds Refit as a runtime dependency. | The third output style, optional. Refit has 153.3M NuGet downloads but adds a runtime dependency (including Castle.DynamicProxy, which is incompatible with AOT). Deferred to v1 because typed HttpClient + extension methods cover the vast majority of use cases without third-party runtime deps. Build only if demand materialises from teams already using Refit who want type reuse on top. |
| 3.2 | **Type Reuse — Attribute-Based Discovery** | `[OpenApiSchema("Address")]` attribute on existing C# types. The generator scans the compilation via Roslyn and automatically maps matching schemas. Zero YAML configuration for type reuse in projects that reference the shared library. | The "tRPC experience" — types just exist without explicit configuration. This is the most ergonomic type reuse mechanism but the hardest to implement: it requires either a Roslyn source generator or a pre-build Roslyn workspace scan. Deferred to v1 because YAML mapping (MMVP) and namespace exclusion (MMVP) cover the use case adequately if less elegantly, and the Roslyn integration is complex. |
| 3.3 | **oneOf / anyOf Discriminated Unions** | Handle `oneOf` and `anyOf` with discriminator properties. Generate a discriminated union type (abstract record + derived types with `[JsonDerivedType]`) or a wrapper type with `TryGet<T>()` methods. | The most common "unsupported pattern" that MMVP/MVP users will hit. Many third-party APIs (Stripe, GitHub) use oneOf for polymorphic responses. The code generation is non-trivial: discriminator mapping, fallback handling, serialization. Deferred to v1 to ship a correct implementation, not a rushed one. |
| 3.4 | **OpenAPI 2.0 (Swagger) Support** | Accept OpenAPI 2.0 (Swagger) specs. Internally convert to OpenAPI 3.0 model before generation. | Many legacy APIs still serve Swagger 2.0 specs. Microsoft.OpenApi can read 2.0 and convert to 3.0 internally. The implementation cost is low (parser handles it), but the edge cases in conversion need testing. Deferred to v1 because the MMVP/MVP audience is likely working with modern specs (3.0+). |
| 3.5 | **Roslyn Source Generator Delivery** | Alternative delivery as a Roslyn incremental source generator. Best IDE experience: instant regeneration in the editor, no explicit build step required. Generated code appears in IntelliSense immediately. | The holy grail of .NET code generation DX. But Roslyn source generators have significant constraints: no file system access, no NuGet references at generation time, size limits on generated output. Many code generators (including Refitter) hit these walls. Deferred to v1 to benefit from lessons learned from the MSBuild task approach, and because source generator limitations may force compromises on feature completeness. |
| 3.6 | **Response Type Variants** | Generate return types that distinguish success from error responses. `ApiResponse<Pet>` with status code, headers, and typed error body. Configurable: return `T` directly (throw on error) or return `ApiResponse<T>` (inspect on error). | Production code needs to handle 400/404/500 responses differently from 200s. Most generators either throw on non-success or return the raw HttpResponseMessage. ApiStitch should generate typed error models from the spec's error responses and let the developer choose the error handling pattern. |
| 3.7 | **Header Parameter Support** | Generate methods that accept header parameters from the OpenAPI spec (API keys, correlation IDs, custom headers). Map them to method parameters or configure them globally via HttpClient defaults. | Deferred from MMVP/MVP because most header-based auth is handled via IHttpClientFactory message handlers, not per-call parameters. But some APIs require per-call headers (idempotency keys, tenant IDs), and the spec declares them. v1 should respect them. |
| 3.8 | **Multipart/Form-Data File Upload** | Generate correct method signatures for `multipart/form-data` operations. Stream-based file parameters, `MultipartFormDataContent` construction handled by the generated code. | File uploads are a common enough pattern that excluding them beyond v1 would be a visible gap. The code generation is straightforward (map binary properties to `Stream` or `StreamContent` parameters) but the testing matrix is wide (mixed form fields + files, multiple files, streaming). |
| 3.9 | **Plugin Architecture for Custom Output** | Allow third-party output plugins. A custom `ICodeEmitter` can produce arbitrary output from the parsed spec model: TypeScript types, documentation, mock servers. | Borrowed from graphql-codegen's plugin model. Future-proofing: if a user needs Flurl output or a custom HttpClient wrapper pattern, they can write a plugin instead of forking the project. Deferred to v1 because the internal code model must stabilize before it becomes a public extension point. |
| 3.10 | **Deprecation Handling** | Generate `[Obsolete("...")]` attributes on deprecated operations and schemas. Optionally skip generating deprecated items entirely. | Clean deprecation handling makes API version migration visible at compile time. If the upstream spec marks an endpoint as deprecated, every caller gets a compiler warning. Low implementation cost but requires the code model to propagate the deprecated flag through the generation pipeline. |
| 3.11 | **XML Documentation Comments** | Generate XML doc comments from OpenAPI `description` and `summary` fields. IntelliSense displays the API documentation inline. | Developers consume API docs through IntelliSense as much as through Swagger UI. Generated XML docs mean `client.GetPetAsync()` shows the operation description on hover. Deferred from MMVP because comment generation adds output complexity and the MMVP must focus on correctness over polish. |
| 3.12 | **Multiple Spec Composition** | Support generating from multiple OpenAPI specs into a single output. Useful for teams consuming APIs from multiple microservices. Shared types across specs resolved via type mapping. | Microservice teams consume 5-15 specs. Today, each spec needs its own config and NuGet package reference. Multi-spec composition generates all clients in one pass, with shared type mapping applied across all specs. This is the microservice team's killer feature — deferred to v1 because the single-spec story must be solid first. |

**v1 scope boundary**: ~32 features total. Three output styles. Three type reuse mechanisms. Full OpenAPI 3.0/3.1/2.0 support. oneOf/anyOf. Roslyn source generator. Plugin architecture. Multi-spec composition. This is the version that competes with Kiota and NSwag on breadth while winning on type reuse, output quality, and DX.

---

## Tier 4: Later / Never

Features explicitly deferred with rationale. "Later" means "if the community asks for it." "Never" means "this is out of scope for ApiStitch's identity."

### Later (Community-Driven)

| # | Feature | Rationale for Deferral |
|---|---------|----------------------|
| 4.1 | **Mock Server Generation** | Generate a lightweight in-memory mock server from the spec for testing. Useful for integration testing without a live API. Build it when a production user contributes requirements, not before — the testing story is adequately served by WireMock and Testcontainers. |
| 4.2 | **F# Output** | Generate F# types and modules. The F# community has its own OpenAPI pain points (SwaggerProvider is the main tool, with limitations). Build it as a community-contributed output plugin if demand materializes. |
| 4.3 | **API Diff / Breaking Change Detection** | Compare two spec versions and report breaking changes in the generated client. Useful for CI pipelines that gate on API compatibility. Adjacent to but not part of code generation. Better suited as a separate tool or plugin. |
| 4.4 | **GraphQL Schema Support** | Accept GraphQL schemas as input alongside OpenAPI. The code generation engine is operation-based; GraphQL operations could theoretically use the same pipeline. But the GraphQL .NET ecosystem (StrawberryShake, Hot Chocolate) already has excellent tooling. Don't compete where the competition is strong. |
| 4.5 | **Custom Template Overrides** | Allow users to provide custom Scriban/Liquid templates to override the default code generation output. Useful for teams with strict coding standards. Build it when template customization requests exceed 5% of GitHub issues. |
| 4.6 | **Visual Studio Extension** | A VS extension with a UI for configuring ApiStitch: spec browser, type mapping visual editor, preview of generated output. High development cost, low maintainability as VS versions change. The YAML config is sufficient for most users. |
| 4.7 | **Swagger UI / ReDoc Integration** | Bundle a generated Swagger UI or ReDoc page alongside the client. This is server-side functionality and belongs in a different tool (Swashbuckle, NSwag's own docs hosting). |
| 4.8 | **Blazor-Specific Generated Components** | Generate Blazor components or service wrappers that integrate generated clients with Blazor's component lifecycle. Interesting but too framework-coupled. Generated clients work with Blazor's DI already; framework-specific wrappers are a thin layer developers can write themselves. |

### Never (Out of Scope)

| # | Feature | Rationale |
|---|---------|-----------|
| 4.9 | **Multi-Language Support** | Generating Java, Python, TypeScript, Go, or any non-C# output. This is OpenAPI Generator's territory and it's a trap: supporting multiple languages means no language gets first-class treatment. ApiStitch's value is being the best .NET generator, not a mediocre polyglot one. |
| 4.10 | **Server Stub Generation** | Generating ASP.NET Core controllers, Minimal API endpoints, or server-side models from an OpenAPI spec. Server generation is a different problem with different constraints. NSwag does both and does neither well. ApiStitch does one thing. |
| 4.11 | **API Gateway / Proxy Generation** | Generating YARP configuration, Ocelot routes, or API gateway definitions from OpenAPI specs. This is infrastructure tooling, not client generation. |
| 4.12 | **Managed SaaS / Cloud Service** | A hosted version of ApiStitch that generates code in the cloud. This changes the product from a developer tool to a service business. Not compatible with the "NuGet package + MSBuild" identity. |
| 4.13 | **.NET Framework / .NET Standard Support** | Backward compatibility with pre-.NET 8 runtimes. The entire value proposition depends on modern C# features (records, required, init, file-scoped namespaces) and System.Text.Json source generation. Supporting legacy runtimes would compromise every design decision. |
| 4.14 | **Protocol Buffers / gRPC Generation** | Grpc.Tools exists and is excellent. Don't compete where the competition has Google's backing and decade-long maturity. ApiStitch is for REST/OpenAPI. |

---

## Decision Log

Key trade-offs and the reasoning behind them, for future reference.

### Why typed HttpClient output first, not Refit?

Typed HttpClient wrappers are the Microsoft-recommended pattern for IHttpClientFactory. They require zero third-party runtime dependencies, are fully AOT-compatible, and work directly with IHttpClientBuilder for Polly resilience chaining. Starting with typed HttpClient means the MMVP has no disqualifying runtime dependency — no team evaluating ApiStitch has to accept a Refit dependency they don't want. Refit has 153.3M downloads, but it adds Castle.DynamicProxy (incompatible with AOT) and creates a runtime dependency that some teams won't accept. Refit output is deferred to v1 as an optional style for teams already using Refit.

### Why YAML mapping for type reuse before attribute discovery?

YAML mapping (`typeMappings: { Address: MyApp.Shared.Address }`) is string substitution. The generator doesn't need to compile the user's project, scan assemblies, or integrate with Roslyn. It just reads a config value and emits a `using` directive instead of a class definition. Attribute-based discovery (`[OpenApiSchema("Address")]`) is the better DX but requires Roslyn workspace scanning or a source generator that reads the user's compilation — both are complex and fragile. Ship the simple mechanism first, earn adoption, then invest in the elegant mechanism.

### Why MSBuild task before Roslyn source generator?

Roslyn source generators have constraints that make OpenAPI code generation difficult: no file system access (can't read spec files from disk easily), no NuGet package references at generation time, output size limits for large specs, and the two-phase problem (generating types that need to be visible to other generators). MSBuild tasks have none of these constraints. Grpc.Tools uses an MSBuild task for exactly this reason. The Roslyn source generator (v1) benefits from lessons learned and can make informed trade-offs about which constraints to work around.

### Why CancellationToken as non-negotiable from MMVP?

Every production .NET HTTP call needs cancellation support. Making CancellationToken optional (as NSwag does) means the default output is not production-ready. Making it always-present means the generated code works correctly in ASP.NET Core (request cancellation), hosted services (shutdown cancellation), and timeout scenarios without the developer remembering to toggle a config option. This is a design principle, not a feature.

### Why no oneOf/anyOf in MMVP or MVP?

oneOf and anyOf with discriminators require generating discriminated union types in C# — a language that doesn't have native discriminated unions. Every approach (abstract records + derived types, wrapper types with TryGet, JsonConverter-based dispatch) has trade-offs. Getting this wrong produces confusing, unusable types. Getting it right requires careful API design and community feedback. The MMVP and MVP handle allOf (the simple composition case). oneOf/anyOf ships in v1 after the base type system has been validated by real users.

### Why three output styles instead of one opinionated choice?

Because the .NET ecosystem doesn't have a single dominant HTTP client pattern. Typed HttpClient (Microsoft-recommended, zero deps, AOT-compatible), extension methods (lightweight pattern), and Refit (153.3M downloads, popular but adds runtime deps) each serve different team preferences and technical constraints (AOT rules out Refit's Castle.DynamicProxy). Offering one style forces developers to adapt their architecture to the generator. Offering three means the generator adapts to the codebase. The cost is a larger testing matrix — mitigated by sharing the parsing and type resolution pipeline across all three emitters.

### Why IHttpClientBuilder return types on DI extensions?

`services.AddPetStoreClient(...)` returning `void` is a dead end — the developer can't chain Polly resilience policies, configure the underlying HttpClientHandler, or add delegating handlers. Returning `IHttpClientBuilder` enables `.AddPolicyHandler(retryPolicy).ConfigurePrimaryHttpMessageHandler(...)` — the standard IHttpClientFactory fluent API. No existing OpenAPI generator does this. It's a one-line difference in the generated code that unlocks the entire Microsoft.Extensions.Http resilience ecosystem.

---

## Summary Table

| Tier | Features | Timeline | Success Gate |
|------|----------|----------|--------------|
| **MMVP** | 10 features: typed HttpClient output, modern C# models, explicit type mapping, namespace exclusion, YAML config, MSBuild task, System.Text.Json source gen, OpenAPI 3.0 core schemas, DI extensions returning IHttpClientBuilder, CancellationToken everywhere | Weeks 1-8 | Publishable blog post. Positive signal on r/dotnet (>50 upvotes, >20 comments). At least 3 developers try it with their own specs and report results. |
| **MVP** | +10 features: extension method output, schema overrides, tag/path filtering, open enums, partial classes, post-gen hooks, OpenAPI 3.1, CLI tool, IAsyncEnumerable pagination, spec URL with caching | Weeks 9-15 | 100+ GitHub stars. 1,000+ NuGet downloads. At least 1 community PR. At least 1 team using it in a real project (not just evaluation). |
| **v1.0** | +12 features: Refit output (optional), attribute-based type discovery, oneOf/anyOf, OpenAPI 2.0, Roslyn source generator, response type variants, header parameters, multipart upload, plugin architecture, deprecation handling, XML docs, multi-spec composition | Weeks 16-28 | 500+ stars. 5,000+ downloads. 5+ community PRs. Migration guide from NSwag published. Conference talk submitted. |
| **Later** | 8 features: mock server, F# output, API diff, GraphQL, custom templates, VS extension, Swagger UI, Blazor components | Community-driven | Build when requested by production users with concrete use cases. |
| **Never** | 6 anti-features: multi-language, server stubs, API gateway, managed SaaS, .NET Framework, gRPC | Permanent | These define what ApiStitch is NOT. Revisit only if the entire product thesis changes. |

---

*Previous artifact: [01 — Product Brief](./01-product-brief.md)*
*Next artifact: 04 — Technical Architecture*
