# ApiStitch — Competitive Positioning & Messaging

**Artifact**: 5 of 5
**Status**: Draft
**Date**: 2026-02-21
**Author**: Mark (solo developer)
**Depends on**: [01 — Product Brief](./01-product-brief.md), [03 — Feature Prioritization](./03-feature-prioritization.md)

---

## 1. Positioning Statement

> **For .NET developers** who have given up on OpenAPI client generation because every tool either duplicates their existing types, produces unreadable code, or requires a 30-minute setup ritual, **ApiStitch is an OpenAPI client generator** that maps schemas to your existing C# types, outputs clean modern code, and runs on every build with zero configuration beyond a spec path.
>
> **Unlike Kiota**, which closed type reuse as "Not Planned," uses a custom serialization model incompatible with System.Text.Json, and officially states that "readability is not a goal," **ApiStitch starts from the types you already have and generates only the code you're missing.**

This is the internal positioning. It is not copy-paste marketing text. Every external message should be a contextual translation of this statement — adapted for audience, channel, and stage of the project's maturity.

---

## 2. One-Liner Variants

### GitHub Repository Description (under 100 characters)
```
.NET OpenAPI client generator with first-class type reuse. Clean C#. MSBuild integration. MIT.
```

Backup option (emphasizes the pain):
```
OpenAPI client generator for .NET that doesn't duplicate your existing types. MIT licensed.
```

### NuGet Package Description (1-2 sentences)
```
ApiStitch generates clean, idiomatic C# clients from OpenAPI specs with first-class type reuse.
Map schemas to your existing types, choose Refit or typed HttpClient output, and get
System.Text.Json source generation — all via MSBuild with zero manual steps. .NET 8+.
```

### Conference Talk Subtitle
```
"Stop Generating Duplicate Types: Building an OpenAPI Client Generator That Understands Your Codebase"
```

Backup (less product-specific, better for broad audiences):
```
"Type Reuse, Clean Output, and MSBuild: What .NET OpenAPI Tooling Should Look Like in 2026"
```

### Twitter / Social Media Pitch
```
I built a .NET OpenAPI client generator because I was tired of:
- Every tool duplicating types I already have
- Kiota closing type reuse as "Not Planned"
- NSwag's 1,900 open issues and uncertain future
- Generated code I'd be embarrassed to show in a code review

It's called ApiStitch. It maps schemas to your existing C# types. Here's the repo: [link]
```

---

## 3. README Comparison Table

This table goes in the GitHub README. It is deliberately honest about where ApiStitch is immature. Developers smell dishonesty instantly and a single misleading cell destroys trust for the entire table.

### Design Principles for the Table
- Include a "Maturity" row. ApiStitch loses this row. Own it.
- Include rows where ApiStitch wins clearly (type reuse, serializer, code quality, MSBuild integration, output styles).
- Do not include rows that are subjective or unverifiable ("performance", "developer experience").
- Link to evidence (GitHub issues, NuGet pages) where possible so readers can verify claims.
- Update this table with every release tier. The MMVP version will have fewer checkmarks than the v1 version.

### MMVP Version of the Table

| Capability | Kiota | NSwag | OpenAPI Generator | Refitter | **ApiStitch** |
|---|---|---|---|---|---|
| **Type reuse (schema-to-type mapping)** | No ([#3912](https://github.com/microsoft/kiota/issues/3912) — Not Planned) | Name-based exclusion only | No | Type exclusion only | **Yes, first-class** |
| **Serializer** | Custom IParsable | Newtonsoft.Json | Varies | Via NSwag (Newtonsoft) | **System.Text.Json source gen** |
| **Code readability** | ["Not a goal"](https://github.com/microsoft/kiota/issues/4330) | Medium | Low | High (Refit interfaces) | **High (records, required, nullable)** |
| **Output styles** | 1 (Kiota clients) | 1 (partial classes) | 1 (per template) | 1 (Refit) | **1 (Refit); 2 more planned** |
| **MSBuild integration** | No native .targets | Yes (VS/CLI inconsistencies) | No (JVM) | Source generator | **Yes, gRPC-style obj/ output** |
| **Setup complexity** | 6-7 NuGet pkgs + auth provider | JSON config (hundreds of options) | JVM + CLI + templates | NuGet + attribute | **Single NuGet + YAML** |
| **IHttpClientFactory + IHttpClientBuilder** | No native integration | Partial (no IHttpClientBuilder) | Varies | Via Refit | **Yes, returns IHttpClientBuilder** |
| **CancellationToken** | Inconsistent | Optional (configurable) | Varies | Via Refit | **Every method, always** |
| **AOT / trimming** | No (reflection-heavy) | No (Newtonsoft) | No | Partial | **Yes (source gen)** |
| **Nullable reference types** | Preprocessor directives (#if) | Partial | No | Via NSwag | **Native, clean** |
| **NuGet downloads** | 92.4M (inflated by MS Graph) | 33.8M | N/A (JVM) | 1.2M | **New** |
| **Production maturity** | Active (Microsoft-backed) | Uncertain ([1,900+ issues](https://github.com/RicoSuter/NSwag/issues)) | Active (community, [4,500+ issues](https://github.com/OpenAPITools/openapi-generator/issues)) | Active (single maintainer) | **Pre-production** |

**Note on including all competitors**: The table includes four competitors because the .NET OpenAPI generator space is fragmented and developers actively compare all of them. Each column earns its place: Kiota (Microsoft's official tool), NSwag (the incumbent), OpenAPI Generator (the polyglot option), Refitter (the rising alternative). Excluding any would leave an obvious gap.

### Post-MVP Table Updates
After each release tier ships, update the table. Change Refit-only to "Refit + typed HttpClient." Add new capability rows as features land. This creates a visible momentum signal. Never add features to the table before they ship.

---

## 4. Key Talking Points by Channel

### 4.1 r/dotnet Post

**Goal**: Generate genuine discussion, not drive downloads. The post that works on r/dotnet is the one that teaches something, not the one that sells something. Self-promotion posts get downvoted. Posts that share a real engineering challenge with a link at the end get engagement.

**Title options** (ordered by preference):
1. "I built an OpenAPI client generator because Kiota closed type reuse as 'Not Planned' — here's what I learned"
2. "After years of hand-writing HttpClient wrappers because NSwag duplicated my shared DTOs, I built my own generator"
3. "The state of .NET OpenAPI client generation is broken. Here's my attempt to fix it."

**Post structure**:
1. Open with the pain. Two paragraphs about the specific problems encountered: shared DTOs duplicated by NSwag, Kiota's IParsable incompatibility with System.Text.Json, the boilerplate of hand-written clients across microservices. Name the GitHub issues. This is not opinion — it is documented, community-verified pain.
2. Acknowledge what existing tools got right. NSwag created the category and served the .NET community for years. Kiota is ambitious in its multi-language scope. Refitter's interface-based approach is elegant. Respect the work. This sentence buys credibility for everything that follows.
3. Explain the approach. Type reuse via YAML mapping and namespace exclusion. Clean C# records with System.Text.Json source gen. MSBuild integration with output in obj/. Technical details, not marketing.
4. Be explicit about what ApiStitch does NOT do yet. No typed HttpClient output (coming in MVP). No oneOf/anyOf (coming in v1). No OpenAPI 2.0. This honesty is the differentiator between a credible project and vaporware.
5. Show the generated code. Paste a real before/after: the YAML config with type mappings and the resulting Refit interface. Let the code speak for itself.
6. Ask for feedback. "What schemas or patterns do your specs use that I should test against? What would you need to see before trying this?" This transforms promotion into conversation.
7. Link to the repo at the end, not the beginning.

**What to avoid**:
- "I'm excited to announce" (corporate voice)
- "Kiota killer" or "NSwag replacement" (antagonistic, invites backlash)
- Any comparison that misrepresents Kiota's or NSwag's capabilities
- Responding defensively to criticism — every critical comment is free product feedback

### 4.2 Blog Post Angles

Four posts, each with a different strategic purpose. These are not published simultaneously — see section 6 (Launch Sequence) for timing.

**Post 1: "The State of .NET OpenAPI Client Generation in 2026: A Honest Assessment"**
- Type: Market analysis, not product promotion
- Purpose: Establish authority in the problem space before launching anything
- Content: Honest assessment of Kiota (92.4M downloads, IParsable lock-in, "readability is not a goal"), NSwag (33.8M downloads, 1,900+ issues, Newtonsoft dependency), OpenAPI Generator (23K+ stars, JVM dependency, non-idiomatic .NET output), Refitter (1.2M downloads, fast growth, NSwag dependency). Data-driven with NuGet numbers and GitHub issue citations.
- ApiStitch mention: None, or a single line at the end: "This analysis informed the design of a tool I'm working on — more soon."
- Target: r/dotnet, Dev.to, Hacker News
- Success metric: 100+ r/dotnet upvotes, 50+ HN points, 20+ substantive comments

**Post 2: "Why I Stopped Using OpenAPI Generators and Started Hand-Writing HTTP Clients (And Why I Regret It)"**
- Type: Personal engineering narrative with immediate practical relevance
- Purpose: Capture the audience that has already given up on generators — they are ApiStitch's largest addressable market
- Content: Walk through the real progression: tried NSwag (duplicate types), tried Kiota (IParsable confusion, 6 NuGet packages), tried hand-writing (worked until the API had 40 endpoints and 3 version bumps). The cost of maintaining hand-written clients at scale. Why generation is worth the effort if the generator respects your codebase.
- ApiStitch mention: Final section — "I built ApiStitch to make generation viable again for teams with shared types. Here's how it works."
- Target: Dev.to (SEO), r/dotnet
- Success metric: Captures search traffic for "NSwag vs Kiota" and related comparison queries

**Post 3: "Type Reuse in OpenAPI Code Generation: Three Approaches for .NET"**
- Type: Technical deep-dive with architecture discussion
- Purpose: Build trust through transparency. Position ApiStitch's type reuse as a thought-through design, not a hack.
- Content: Explain the three approaches (YAML mapping, namespace exclusion, attribute discovery) with trade-offs for each. Show how the code generation pipeline handles type resolution. Discuss the build ordering challenges and how MSBuild task vs Roslyn source generator affects type reuse implementation. Each approach framed as an engineering trade-off with explicit downsides.
- ApiStitch mention: Central subject. No need to hide it.
- Target: Hacker News, r/dotnet, personal blog
- Success metric: 50+ HN points, 3+ technical discussions in comments

**Post 4: "From OpenAPI Spec to Production Client in 60 Seconds: Building a Grpc.Tools-Style MSBuild Integration"**
- Type: Practical tutorial with DX showcase
- Purpose: Demonstrate the "zero-config" promise with a real walkthrough
- Content: Start with an empty .NET project. Install the NuGet package. Add a spec. Build. Show the generated code. Add type mappings. Rebuild. Show the mapped types. Chain a Polly policy on the IHttpClientBuilder. Deploy. Total time: 60 seconds of configuration, the rest is build time.
- Target: Dev.to, r/dotnet, Twitter (thread format)
- Success metric: Shared as a reference by at least 3 people in "which OpenAPI generator" discussions

### 4.3 Conference Talk Pitch

**Title**: "Stop Generating Duplicate Types: What .NET OpenAPI Tooling Should Look Like"

**Abstract** (for submission to .NET Conf, NDC, Update Conference, local meetups):

> Every .NET team consuming OpenAPI specs faces the same problem: the code generator doesn't know about your existing types. You get a generated `Address` class alongside the `Address` class you already share between client and server. You try the exclusion list. It breaks when you rename a type. You give up and hand-write HTTP clients.
>
> In this talk, I'll walk through why type reuse is the #1 unmet need in .NET OpenAPI tooling — backed by GitHub issues, NuGet data, and community frustration. We'll look at how ApiStitch solves it with three approaches (YAML mapping, namespace exclusion, and Roslyn-based attribute discovery), why System.Text.Json source generation matters for AOT, and what it takes to build gRPC-style MSBuild integration that "just works."
>
> Whether you use ApiStitch, Kiota, NSwag, or hand-write your clients, you'll leave with a clear framework for evaluating OpenAPI generators and patterns for type reuse you can apply in any codebase.

**Duration**: 30-45 minutes
**Target events**: .NET Conf 2026 (virtual, CFP opens mid-year), NDC Oslo/London, Update Conference, local .NET user groups (start here for practice runs)

### 4.4 Hacker News (Show HN)

**Title**:
```
Show HN: ApiStitch – .NET OpenAPI client generator with first-class type reuse
```

Backup titles:
```
Show HN: ApiStitch – OpenAPI generator that maps schemas to your existing C# types
Show HN: ApiStitch – Clean C# clients from OpenAPI specs, with type reuse and MSBuild integration
```

**Link to**: The GitHub repository. HN rewards repos over landing pages for dev tools. The README is the landing page.

**First comment** (posted immediately by the author):

> Hi HN, I'm Mark. I built ApiStitch because every .NET OpenAPI client generator I tried either duplicated my existing types or produced code I couldn't read.
>
> The specific problems:
> - Kiota (Microsoft's official generator, 92.4M NuGet downloads) closed type reuse as "Not Planned" (#3912), uses a custom serialization model incompatible with System.Text.Json, and officially states "readability is not a goal"
> - NSwag (33.8M downloads) has 1,900+ open issues, still depends on Newtonsoft.Json, and offers only fragile name-based type exclusion
> - OpenAPI Generator requires a JVM and produces non-idiomatic .NET code
> - Refitter (1.2M downloads, growing fast) generates clean Refit interfaces but relies on NSwag internally for models
>
> ApiStitch takes a different approach: you tell it which OpenAPI schemas map to your existing C# types (via YAML config or namespace exclusion), and it generates only the HTTP client code — clean Refit interfaces or typed HttpClient wrappers with System.Text.Json source generation.
>
> What it does today: Refit interface generation, type mapping, MSBuild integration (output in obj/, like Grpc.Tools), System.Text.Json source gen, OpenAPI 3.0 core schemas.
>
> What it doesn't do yet: typed HttpClient output, oneOf/anyOf, OpenAPI 2.0, CLI tool. These are planned but not shipped. The README comparison table shows "Planned" honestly.
>
> The code is MIT-licensed and I'm the sole maintainer. I'm especially interested in feedback from anyone who has given up on code generation and hand-writes HTTP clients — that's the audience I'm trying to reach.

**Tactical notes for the HN post**:
- Post between 8-10am US Eastern, Tuesday through Thursday
- Do not post on the same day as a major tech announcement
- Respond to every comment within 2 hours for the first 6 hours
- When criticized, agree with the valid part before explaining
- Do not have friends or colleagues upvote — HN detection is aggressive
- If the post doesn't gain traction in 2 hours (fewer than 5 points), wait at least a week before trying a different title
- The .NET audience on HN is smaller than the web/systems audience — set expectations accordingly. 30+ points is a good result for a .NET tool.

---

## 5. What NOT to Say

Messaging anti-patterns that will undermine credibility. Each one is a pattern observed in failed OSS launches or competitor missteps.

### 5.1 Do Not Bash Kiota's Team or NSwag's Maintainer

Kiota is built by a Microsoft team with legitimate multi-language ambitions. NSwag was built and maintained by Rico Suter for years, serving millions of developers. Criticize the architectural decisions, the technical trade-offs, the feature gaps — never the people. The .NET community is small enough that personal attacks travel fast and permanently.

**Wrong**: "The Kiota team refuses to support type reuse."
**Right**: "Kiota's architecture is built around IParsable, a serialization model that makes type reuse structurally difficult. The team closed the type reuse request as 'Not Planned,' likely because it would require a fundamental redesign."

**Wrong**: "NSwag is abandoned."
**Right**: "NSwag has 1,900+ open issues and the community is uncertain about its maintenance trajectory (issue #4884). For teams starting new projects, this uncertainty is a risk factor."

### 5.2 Do Not Claim Production-Readiness Before It Is True

The MMVP handles a subset of OpenAPI schemas. The first real-world spec a user throws at it will expose gaps. Calling it production-ready and having someone's build break on an unhandled schema pattern will destroy trust faster than any competitor could.

**Wrong**: "ApiStitch is a production-ready OpenAPI client generator."
**Right**: "ApiStitch generates clean Refit clients from OpenAPI 3.0 specs with type reuse. It handles the common 80% of schema patterns. It's alpha software — I'm looking for early adopters who will test it against their real specs and report what breaks."

Label the maturity clearly:
- MMVP: "Alpha — handles core OpenAPI 3.0 patterns. Try it on your spec and tell me what's missing."
- MVP: "Beta — handles most real-world specs. Suitable for non-critical projects with monitoring."
- v1.0: "Stable — production-ready for the documented feature set."

### 5.3 Do Not Position as a "Kiota Killer" or "NSwag Replacement"

"Killer" framing sets expectations ApiStitch can't meet at launch, antagonizes existing communities, and frames success as dependent on competitors' failure.

**Wrong**: "The NSwag replacement the .NET world has been waiting for."
**Right**: "A modern alternative for teams whose needs aren't served by existing generators — particularly teams sharing types between client and server."

### 5.4 Do Not Sell Unreleased Features

The comparison table has "Planned" tags. Blog posts can mention the roadmap. But do not cite typed HttpClient output or oneOf/anyOf support as reasons to adopt today. Users who adopt on a promise and hit the gap will be more frustrated than users who knew the limitation upfront.

**Wrong**: "ApiStitch supports Refit, typed HttpClient, and extension method output styles."
**Right**: "ApiStitch ships with Refit interface generation today. Typed HttpClient output is next in the MVP milestone."

### 5.5 Do Not Use Corporate Marketing Language

The audience is developers evaluating a code generator. They will reject polish and respond to substance.

**Wrong**: "ApiStitch empowers development teams to seamlessly integrate OpenAPI specifications into their .NET solutions with next-generation type-safe code generation."
**Right**: "ApiStitch reads your OpenAPI spec, maps schemas to types you already have, and generates Refit interfaces. The generated code goes in obj/ and you never think about it again."

### 5.6 Do Not Dismiss the "Just Hand-Write It" Crowd

Many experienced .NET developers have concluded that code generation isn't worth the hassle. They're right — given the current tooling. Dismissing their position alienates the exact audience ApiStitch should convert.

**Wrong**: "Hand-writing HTTP clients is a waste of time. Use a generator."
**Right**: "If you've given up on generators because they all duplicate your types or produce unreadable code, I built ApiStitch to address exactly those problems. The generated code should be indistinguishable from what you'd write by hand."

### 5.7 Do Not Overstate the Kiota Download Numbers

Kiota's 92.4M NuGet downloads are inflated by the Microsoft Graph SDK, which bundles Kiota packages as transitive dependencies. Most of those downloads are not developers choosing Kiota for their own OpenAPI specs. Cite the number but always note the inflation.

**Wrong**: "Kiota has 92.4M downloads, proving it's the market leader developers choose."
**Right**: "Kiota has 92.4M NuGet downloads, though this is heavily inflated by the Microsoft Graph SDK bundling Kiota packages as transitive dependencies. Actual opt-in adoption for custom OpenAPI specs is significantly lower."

---

## 6. Launch Sequence

Ordered list of content and marketing actions. Core principle: **build authority in the problem space before announcing the solution.** The worst launch strategy is publishing the repo on day one with no audience and no context.

### Phase 0: Pre-Launch (Weeks -4 to -1, Before MMVP Ships)

The goal of Phase 0 is to have an audience that already cares about the problem before the solution exists publicly.

| # | Action | Channel | Success Metric | Notes |
|---|--------|---------|----------------|-------|
| 0.1 | Publish **Blog Post 1** ("The State of .NET OpenAPI Client Generation in 2026") | Dev.to, personal blog, cross-post to r/dotnet | 100+ r/dotnet upvotes, 50+ HN points | Do NOT mention ApiStitch by name. Establish authority in the problem space first. |
| 0.2 | Start answering OpenAPI/HttpClient questions on Stack Overflow and r/dotnet | Stack Overflow, Reddit | 5+ substantive answers | Become a recognizable name in "which OpenAPI generator should I use?" threads. |
| 0.3 | Post a discussion: "What's your biggest pain point with .NET OpenAPI generators?" | r/dotnet | 50+ upvotes, 30+ comments | Validates demand (Assumption A1) and collects messaging language from real developers. |
| 0.4 | Publish **Blog Post 2** ("Why I Stopped Using OpenAPI Generators and Started Hand-Writing") | Dev.to, personal blog | 50+ upvotes, search traffic for "NSwag vs Kiota" | Captures the "gave up on generators" audience. |

### Phase 1: Soft Launch (Week 0, MMVP Ships)

The goal of Phase 1 is to get the repo in front of early adopters primed from Phase 0.

| # | Action | Channel | Success Metric | Notes |
|---|--------|---------|----------------|-------|
| 1.1 | Push MMVP to GitHub with complete README (comparison table, quickstart, generated code samples, type mapping example) | GitHub | N/A — prerequisite for everything else | The README is the landing page. Include a real before/after showing type reuse. Spend as much time on it as on a blog post. |
| 1.2 | Publish NuGet package (pre-release: `0.1.0-alpha`) | NuGet | Package live, metadata optimized | Pre-release tag sets honest expectations. Tags: openapi, swagger, code-generator, refit, type-reuse. |
| 1.3 | Publish **Blog Post 3** ("Type Reuse in OpenAPI Code Generation: Three Approaches") | Personal blog, Dev.to | 50+ HN points, 20+ r/dotnet comments | The launch announcement disguised as a technical article. |
| 1.4 | Post to **r/dotnet** | Reddit | 100+ upvotes, 30+ comments, 50+ GitHub stars within 72 hours | Follow the structure in section 4.1 exactly. |
| 1.5 | Recruit 3-5 beta testers from the r/dotnet thread | Reddit DMs, GitHub Discussions | 3+ people testing ApiStitch with their own specs within 2 weeks | The real validation: does the generator handle real-world specs? Every failure is a high-priority bug report. |

### Phase 2: Amplification (Weeks 1-3, Post-Launch)

The goal of Phase 2 is to create a second wave of attention after the launch spike.

| # | Action | Channel | Success Metric | Notes |
|---|--------|---------|----------------|-------|
| 2.1 | Post **Show HN** | Hacker News | 30+ points | See section 4.4. Post 3-5 days after the r/dotnet post. Set expectations lower for a .NET-specific tool on HN. |
| 2.2 | Submit to **awesome-dotnet** | GitHub PR | PR accepted | Passive discovery. Wait until 50+ stars for credibility. |
| 2.3 | Tweet/post the comparison table as a standalone image | Twitter/X, Mastodon, LinkedIn | 30+ retweets/shares | The table is the most shareable artifact. |
| 2.4 | Publish **Blog Post 4** ("From OpenAPI Spec to Production Client in 60 Seconds") | Personal blog, Dev.to | Shared in 3+ "which generator" threads | Practical tutorial format. The demo video equivalent in text form. |
| 2.5 | Comment with ApiStitch as an option in active "NSwag vs Kiota" threads | r/dotnet, Stack Overflow | 3+ relevant threads | Not spamming — only comment where type reuse is specifically discussed as a need. Always disclose that you're the author. |

### Phase 3: Sustained Growth (Weeks 4-12)

The goal of Phase 3 is to convert launch attention into sustained organic growth.

| # | Action | Channel | Success Metric | Notes |
|---|--------|---------|----------------|-------|
| 3.1 | Publish a monthly "ApiStitch Dev Log" blog post | Personal blog, Dev.to | Consistent 20+ upvotes per post | Shows momentum. Each post highlights new schema support, output improvements, or community contributions. |
| 3.2 | Submit conference talk to .NET Conf 2026, NDC, or regional conferences | Conference CFPs | 1+ talk accepted | Use the abstract from section 4.3. |
| 3.3 | Publish beta tester case study | Personal blog | 1 case study: "How [team] uses ApiStitch with their Blazor WASM app" | Social proof. A real team with a real codebase using type reuse in production. |
| 3.4 | Ship MVP milestone and announce | r/dotnet, HN, Twitter | 50+ upvotes, 100+ new stars | The MVP announcement is a second launch moment. Now with typed HttpClient output and schema overrides. |
| 3.5 | Write a "Migrating from NSwag to ApiStitch" guide | Repo docs, personal blog | Appears in search for "NSwag alternative" | Directly captures switching intent. Provide a concrete migration path: map NSwag config to ApiStitch YAML, show equivalent output. |

### Metrics Dashboard

Track these across all channels, reviewed weekly:

| Metric | Source | Phase 0 Target | Phase 1 Target | Phase 2 Target |
|---|---|---|---|---|
| GitHub stars | GitHub | 0 | 75 | 200 |
| NuGet downloads | NuGet | 0 | 300 | 1,000 |
| Blog post views (total) | Dev.to / analytics | 2,000 | 5,000 | 10,000 |
| r/dotnet upvotes (total) | Reddit | 150 | 250 | 350 |
| HN points (best post) | Hacker News | 50 | 30 | 50 |
| Beta testers recruited | GitHub Discussions | 0 | 3 | 5 |
| Specs tested by users | GitHub Issues | 0 | 5 | 15 |
| Community PRs | GitHub | 0 | 0 | 1 |

---

## 7. Messaging Evolution by Stage

The messaging should mature as the product matures. Do not use v1 messaging at MMVP stage.

### MMVP Messaging (Alpha)

**Tone**: Humble, curious, builder-oriented.
**Core message**: "I built this because my shared DTOs kept getting duplicated by every generator I tried. Here's my approach. Try it on your spec and tell me what breaks."
**What to emphasize**: Type reuse, clean output, the problems being solved, honesty about what's missing.
**What to avoid**: Any claim of completeness, production-readiness, or superiority to established tools.

Example: "ApiStitch is a new OpenAPI client generator for .NET 8+ that generates Refit interfaces with first-class type reuse. It's alpha software — it handles core OpenAPI 3.0 patterns and maps schemas to your existing C# types via YAML config. I'm looking for developers willing to test it against their real specs."

### MVP Messaging (Beta)

**Tone**: Confident, specific, evidence-backed.
**Core message**: "Here's what we've shipped, here are the specs it handles, here's who's using it."
**What to emphasize**: Two output styles (Refit + typed HttpClient), schema-level overrides, tag filtering, real-world spec compatibility, beta tester testimonials.
**What to avoid**: "Production-ready" without qualification. Still no "Kiota killer."

Example: "ApiStitch now generates Refit interfaces and typed HttpClient wrappers from OpenAPI 3.0 and 3.1 specs, with type reuse, schema overrides, and tag filtering. Five teams are testing it against their production specs. The API is stabilizing toward v1."

### v1 Messaging (Stable)

**Tone**: Assertive, professional, category-defining.
**Core message**: "The .NET OpenAPI client generator built around your existing types."
**What to emphasize**: Full feature set, three output styles, three type reuse mechanisms, production case studies, the comparison table with all checkmarks filled.
**What to avoid**: Complacency. v1 is when real competition begins.

Example: "ApiStitch is a production-ready OpenAPI client generator for .NET 8+. First-class type reuse via YAML mapping, namespace exclusion, or attribute discovery. Refit, typed HttpClient, or extension method output. System.Text.Json source generation for AOT. OpenAPI 2.0, 3.0, and 3.1. MSBuild integration that works like Grpc.Tools. MIT licensed."

---

## 8. Tagline Candidates

For use across the repo, NuGet, social media, and presentations. Ranked by preference.

1. **"OpenAPI clients that know your types."** — Captures the core differentiator in six words.
2. **"Generate the client. Keep your types."** — Frames the problem and the solution in one breath.
3. **"The OpenAPI generator that reads your codebase, not just your spec."** — Slightly long but precise. Works for blog posts and talks.
4. **"Clean clients from OpenAPI specs. Your types, not duplicates."** — More literal, less memorable. Good for NuGet descriptions.

Use tagline 1 for the MMVP. It's short, differentiated, and immediately communicates what no other generator does. Evaluate whether tagline 2 resonates better based on community feedback.

---

*Previous artifact: [03 — Feature Prioritization](./03-feature-prioritization.md)*
