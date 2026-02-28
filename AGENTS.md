# ApiStitch Agent Instructions

## Scope

ApiStitch is a .NET OpenAPI client generator focused on:
- Type reuse via `x-apistitch-type`
- Idiomatic C# output
- Typed HttpClient generation
- AOT/trimming-safe serialization

## Tech Stack

- .NET 10, C# 12
- Microsoft.OpenApi v3.x
- Microsoft.OpenApi.YamlReader
- Scriban
- System.Text.Json
- Roslyn (`Microsoft.CodeAnalysis`)

## Reflection Policy

- Avoid reflection in generated output.
- Use System.Text.Json source generation.
- Use Roslyn analysis for type discovery during generation.

## Dependencies

Allowed:
- `Microsoft.OpenApi`, `Microsoft.OpenApi.YamlReader`
- `Scriban`
- `Microsoft.CodeAnalysis`
- `System.Text.Json`
- `System.CommandLine`
- `Microsoft.Extensions.Http` and DI abstractions (generated runtime)

Banned:
- `Newtonsoft.Json`
- `NSwag`
- `Entity Framework Core`
- Java/JVM tooling

## Code Conventions

- Async all the way (`Task`-based I/O).
- XML doc comments on all public types/members/parameters.
- Do not add code comments unless absolutely necessary.
- CancellationToken on generated async methods.
- Generated files use `[GeneratedCode("ApiStitch")]` (no version).
- Deterministic output (stable ordering, no timestamps).
- YAML config file is `openapi-stitch.yaml`.

## Change Process

- Default workflow is OpenSpec-first for non-trivial changes.
- Do not implement non-trivial code changes until the relevant OpenSpec change is created/selected.
- Bugfixes may be implemented without a full OpenSpec cycle only after explicitly stating:
  - the bug (observed behavior and impact), and
  - the proposed solution (smallest safe fix).
- If a request is scoped to samples/docs, do not modify `src/ApiStitch/**` unless explicitly approved by the user.

## Documentation Definition of Done

- For any user-visible behavior/configuration/CLI change, update both `README.md` and `CHANGELOG.md` in the same change.
- In OpenSpec task creation, include explicit checklist items for:
  - updating `README.md` for new/changed behavior, options, or examples, and
  - adding an `Unreleased` entry to `CHANGELOG.md`.
- If docs are intentionally not updated, state why in the change artifacts/tasks.

## Operational Safety

- Never switch Kubernetes context as part of automation.
- For `kubectl`/`helm` commands, always specify the context explicitly.

## Git Commits

- Always create signed commits in this repository.
- Use Git signing (`git commit -S`) and do not bypass signing unless explicitly requested by the user.
- Use Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `test:`).
