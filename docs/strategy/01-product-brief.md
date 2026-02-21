# ApiStitch — Product Brief

**Artifact**: 1 of 5
**Status**: Draft
**Date**: 2026-02-21
**Author**: Mark (solo developer)

---

## 1. Problem Statement

.NET developers who consume OpenAPI specs face a broken toolchain. Kiota, Microsoft's official generator, produces unreadable code by policy ("readability is not a goal"), requires 6-7 NuGet packages for setup, forces a custom IParsable serialization model incompatible with System.Text.Json, and closed the most-requested feature — type reuse — as "Not Planned" (issue #3912). NSwag, with 1,900+ open issues, has the community asking "is NSwag dead?" (issue #4884), still maps to Newtonsoft.Json internally, and offers only fragile name-based type exclusion. OpenAPI Generator requires a JVM dependency and produces non-idiomatic .NET code. Refitter generates clean Refit interfaces but relies on NSwag internally for models and has no true type mapping. Microsoft.Extensions.ApiDescription.Client — the only official MSBuild integration point — is deprecated in .NET 10. The result is that fullstack .NET teams sharing DTOs between Blazor WASM clients and ASP.NET Core servers abandon code generation entirely, hand-rolling HTTP clients and maintaining duplicate types across project boundaries. ApiStitch exists to give .NET developers an OpenAPI client generator that produces clean, idiomatic C# with first-class type reuse — the feature every team needs and no tool provides.

---

## 2. Target Users

### The Fullstack .NET Developer
Builds applications with Blazor WASM or MAUI on the frontend and ASP.NET Core on the backend. Shares DTOs via a class library referenced by both projects. Tried NSwag once, got a 4,000-line generated file that duplicated every model from the shared library, spent an afternoon configuring ExcludedTypeNames, and gave up. Now hand-writes HttpClient calls and manually keeps client-side types in sync with the API. Evaluates tools by asking: "Will this understand that I already have these types?"

### The Microservice Architect
Manages 5-15 services that communicate over HTTP APIs. Ships shared contract types in internal NuGet packages. Every service consumes 2-4 other services' OpenAPI specs. Tried Kiota, got a different serialization model (IParsable) that couldn't round-trip with the System.Text.Json contracts the team already uses. Now maintains hand-written clients per service and dreads API version bumps. Needs a generator that maps schema X to existing type Y and generates only the HTTP plumbing.

### The Frustrated Pragmatist
Consumes third-party OpenAPI specs (Stripe, Twilio, internal partner APIs). Tried OpenAPI Generator, got a Java dependency and code that looked like it was ported from a 2015 Swagger Codegen template. Tried Kiota, needed AnonymousAuthenticationProvider for an unauthenticated API and six NuGet packages before writing a single request. Wants to add a NuGet package, point it at a spec, and get a typed client that works with IHttpClientFactory. Will evaluate any tool that takes fewer than 5 minutes to set up.

### The AOT-Conscious Developer
Building for Native AOT deployment — cloud functions, trimmed containers, high-throughput services. Needs generated code that uses System.Text.Json source generation, avoids reflection, and works with IHttpClientFactory. Kiota's IParsable model is reflection-heavy. NSwag's Newtonsoft dependency is incompatible with trimming. Currently writes manual JsonSerializerContext classes for every API type. Wants a generator that outputs AOT-compatible code by default.

---

## 3. Jobs-to-be-Done

### Functional Jobs
- **When** I consume an OpenAPI spec from a service that shares DTOs with my project, **I want to** map schema types to my existing C# classes, **so that** I don't maintain duplicate type hierarchies across client and server.
- **When** I add a new API dependency to my project, **I want to** install a NuGet package, point it at a spec file or URL, and get typed clients on the next build, **so that** I don't hand-write boilerplate HTTP calls.
- **When** the upstream API spec changes, **I want to** rebuild and see compile errors for breaking changes, **so that** I catch integration issues at build time rather than runtime.
- **When** I review generated code (or diff it across versions), **I want to** read clean, modern C# that uses records, nullable reference types, and init setters, **so that** the generated code doesn't feel like a foreign body in my codebase.
- **When** I deploy to Native AOT or trimmed environments, **I want to** trust that the generated client uses source-generated serialization, **so that** I don't hit runtime reflection failures in production.

### Emotional Jobs
- **Confidence**: I want to point at the generated code and say "I could have written this by hand" — not apologize for it in code reviews.
- **Control**: I want to choose the output style (Refit interfaces, typed clients, extension methods) that fits my team's conventions, not adapt my architecture to the generator's opinion.
- **Relief**: I want to stop maintaining hand-written HttpClient wrappers because I finally have a generator I trust.

---

## 4. Competitive Positioning

| Capability | Kiota | NSwag | OpenAPI Generator | Refitter | **ApiStitch** |
|---|---|---|---|---|---|
| **Type reuse** | No (closed as Not Planned, #3912) | Name-based exclusion (fragile) | No | Type exclusion (no mapping) | **Yes, first-class mapping** |
| **Serializer** | Custom IParsable (not System.Text.Json) | Newtonsoft.Json internally | Varies by template | Via NSwag (Newtonsoft) | **System.Text.Json with source gen** |
| **Code readability** | "Not a goal" (official policy) | Medium (partial classes, boilerplate) | Low (template-dependent) | High (Refit interfaces) | **High (modern C# idioms)** |
| **MSBuild integration** | No native .targets | Exists (VS/CLI inconsistencies) | No (JVM-based) | Source generator (writes to disk) | **Yes, gRPC-style obj/ output** |
| **Output styles** | One (Kiota clients) | One (partial classes) | One (per template) | One (Refit interfaces) | **Three (Refit, typed client, extensions)** |
| **Setup complexity** | 6-7 NuGet packages + auth provider | JSON config with hundreds of options | JVM + CLI + templates | NuGet + attribute | **Single NuGet + YAML** |
| **IHttpClientFactory** | No native integration | Yes | Varies | Via Refit | **Yes, returns IHttpClientBuilder** |
| **CancellationToken** | Inconsistent | Optional | Varies | Via Refit | **Every method, always** |
| **AOT / trimming** | No (reflection-heavy IParsable) | No (Newtonsoft) | No | Partial (model layer blocked) | **Yes (source gen serialization)** |
| **Nullable reference types** | Preprocessor directives on every property | Partial | No | Via NSwag | **Native, clean** |
| **NuGet downloads** | 92.4M (inflated by MS Graph SDK) | 33.8M | N/A (JVM) | 1.2M | **New** |
| **Maintenance status** | Active (Microsoft) | Uncertain (1,900+ issues, #4884) | Active (community) | Active (single maintainer) | **Active (single maintainer)** |

**Positioning statement**: ApiStitch is the .NET OpenAPI client generator that does what developers have been asking every other tool to do: map API schemas to your existing types and output code that looks hand-written. First-class type reuse, System.Text.Json source generation, multiple output styles, zero-config MSBuild integration — for the teams who gave up on code generation because no generator understood their codebase.

---

## 5. Core Differentiators

These are the six capabilities that define ApiStitch's identity. Each one directly addresses a documented failure in the existing toolchain.

### 5.1 First-Class Type Reuse

**What**: Map OpenAPI schema types to existing C# classes via explicit YAML mappings, namespace exclusion, or attribute-based discovery. The generator emits `using` directives instead of duplicate types.
**Why**: This is the #1 unmet need in .NET OpenAPI tooling. Kiota closed it as "Not Planned" (issue #3912). NSwag offers ExcludedTypeNames, which is fragile name-based string matching that breaks when types move namespaces. Refitter has type exclusion but no mapping — it can skip generating a type, but it can't tell the generated client to use your existing one. Every fullstack .NET team (Blazor WASM, MAUI) and every microservice team with shared DTO packages hits this wall. It is the single most common reason developers abandon OpenAPI code generation in .NET.

### 5.2 Clean, Idiomatic C# Output

**What**: Generated code uses modern C# features: records, required properties, nullable reference types (without preprocessor directives), init setters, file-scoped namespaces. The output reads like code a senior developer wrote by hand.
**Why**: Kiota officially states that "readability is not a goal." The result is code littered with `#if NETSTANDARD2_1_OR_GREATER` preprocessor directives on every nullable property, path segment noise (`client.Api.V1.Talks` instead of `client.Talks`), and version bump diffs that touch every generated file. Developers on Hacker News describe Kiota as "needlessly overcomplicated" and hand-roll HTTP clients instead. NSwag generates partial classes with Newtonsoft attributes. OpenAPI Generator outputs code that looks like it was machine-translated from Java. Clean output is not cosmetic — it determines whether developers trust the generator or bypass it.

### 5.3 Multiple Output Styles

**What**: Choose between Refit interfaces, typed HttpClient wrappers (interface + implementation + DI extensions), or extension methods on HttpClient. Same engine, same spec, different output shapes.
**Why**: Refit has 153.3M NuGet downloads — it is the most popular REST client pattern in .NET. But not every team uses Refit. Some want concrete HttpClient wrappers for maximum control and AOT compatibility. Some want lightweight extension methods for internal services. Every existing generator forces one style. ApiStitch lets the team choose what fits their codebase, not what fits the generator's architecture.

### 5.4 Zero-Config MSBuild Integration

**What**: Install the NuGet package, add an MSBuild property pointing to the spec, build. Generated code goes to `obj/`, never committed to source control. Incremental builds — regenerate only when the spec changes.
**Why**: This is how `Grpc.Tools` works, and it is the gold standard for code generation DX in .NET. NSwag's MSBuild integration has documented inconsistencies between Visual Studio and `dotnet build`. Kiota has no native MSBuild integration. OpenAPI Generator requires a JVM. Refitter's source generator writes files to disk (a two-stage generation problem). Microsoft.Extensions.ApiDescription.Client — the only official MSBuild hook — is deprecated in .NET 10. The build system is the integration surface. If code generation doesn't happen on build, developers forget to regenerate and ship stale clients.

### 5.5 System.Text.Json Source Generation

**What**: Generated models use `System.Text.Json` with source-generated `JsonSerializerContext` for AOT and trimming compatibility. No Newtonsoft.Json dependency. No runtime reflection.
**Why**: Kiota uses a custom IParsable serialization model that is incompatible with System.Text.Json and reflection-heavy. NSwag maps to Newtonsoft.Json internally, which is incompatible with trimming and has had multiple CVEs. As .NET moves toward Native AOT (cloud functions, trimmed containers, MAUI), generators that depend on reflection or Newtonsoft are dead ends. System.Text.Json source generation is the only serialization path that works everywhere .NET 8+ runs.

### 5.6 Production-Ready HTTP Patterns

**What**: Generated clients integrate with IHttpClientFactory. DI extension methods return `IHttpClientBuilder` so teams can chain Polly resilience policies. CancellationToken on every method. IAsyncEnumerable for paginated endpoints. Open enums that don't break on unknown values.
**Why**: Kiota has no native IHttpClientFactory integration. NSwag generates clients that work with DI but don't return IHttpClientBuilder for resilience chaining. None of the existing generators produce IAsyncEnumerable for pagination or handle open enums cleanly. These are not advanced features — they are the patterns every production .NET HTTP client needs. A generator that doesn't produce them creates work for the developer to add them manually, which defeats the purpose of generation.

---

## 6. What ApiStitch is NOT

Explicit boundaries to prevent scope creep and set expectations.

- **Not a multi-language tool.** C# and .NET only, by design. OpenAPI Generator covers 70+ languages poorly. ApiStitch covers one language well. Narrowing the target means every design decision optimizes for .NET idioms, MSBuild integration, and System.Text.Json — none of which are possible when supporting Java, Python, and TypeScript from the same engine.
- **Not a server stub generator.** ApiStitch generates HTTP clients, not ASP.NET Core controllers. Server-side code generation is a different problem with different constraints (routing, middleware, model binding). Staying client-only keeps the scope tractable and the output focused.
- **Not a replacement for Refit itself.** ApiStitch can generate Refit interfaces as one of its output styles. It doesn't replace Refit's runtime — it generates the interface definitions that Refit consumes. Teams already using Refit gain type reuse and MSBuild integration on top of their existing pattern.
- **Not an API design tool.** ApiStitch consumes OpenAPI specs. It does not help you write, validate, or lint specs. Tools like Spectral, Redocly, and Stoplight own that space. ApiStitch assumes you have a valid spec and generates a client from it.
- **Not trying to support every OpenAPI edge case on day one.** The MMVP targets OpenAPI 3.0 and the common 80% of schema patterns: objects, arrays, enums, $ref, allOf for inheritance, nullable. Exotic features (oneOf discriminators, XML serialization, callbacks, links) are deferred until the core is stable and users report real needs.
- **Not targeting legacy .NET.** .NET 8+ only. No .NET Framework, no .NET Standard, no conditional compilation. This enables records, required properties, init setters, file-scoped namespaces, and System.Text.Json source generators without compromise.

---

## 7. Success Metrics

ApiStitch is a credibility and reputation project — proving expertise in .NET tooling and code generation. Success is measured by adoption, ecosystem influence, and recognition as the go-to .NET OpenAPI client generator.

### 6-Month Targets (Post-MMVP Launch)

| Metric | Target | Signal |
|---|---|---|
| GitHub Stars | 400+ | Community interest and discoverability |
| NuGet Downloads (total) | 5,000+ | Actual trial and adoption |
| Open Issues (non-bug) | 15+ | People using it enough to want more |
| Community PRs Merged | 3+ | External contributors investing effort |
| Blog Posts / Content Pieces | 6+ | Thought leadership in the OpenAPI/.NET space |
| r/dotnet / HN appearances | 2+ front-page posts | Content resonance |

### 12-Month Targets

| Metric | Target | Signal |
|---|---|---|
| GitHub Stars | 1,500+ | Entering "credible tool" territory |
| NuGet Downloads (total) | 25,000+ | Real production adoption |
| Community PRs Merged | 15+ | Healthy contributor ecosystem |
| Conference / Meetup Talks | 2+ | Speaking opportunities generated |
| Production Users (self-reported) | 10+ | Trust signal for others considering adoption |
| Migration Guides Published | 2+ (from Kiota, from NSwag) | Capturing switching intent |
| Contributors (unique) | 8+ | Sustainable open-source project |

### Leading Indicators to Watch Weekly
- GitHub star velocity (accelerating or decelerating?)
- NuGet download trend (weekly, not cumulative)
- Issue response time (target: first response within 24 hours)
- Time from PR submission to merge (target: under 72 hours for community PRs)
- Mentions on r/dotnet, Hacker News, Twitter/X (organic buzz)
- Kiota/NSwag issue tracker (new complaints = new potential users)

### Go/No-Go Signal at 6 Months
**Continue investing** if: 200+ stars AND 2,000+ downloads AND at least 2 community PRs merged. This indicates the project has crossed from "personal tool" to "community tool."

**Pivot approach** if: Under 75 stars after 6 months despite consistent content marketing. This suggests the type reuse message isn't landing — reassess whether the pain is as widespread as assumed, or whether the messaging needs rework.

---

*Next artifact: [02 — Assumption Register & Risk Map](./02-assumptions-and-risks.md)*
