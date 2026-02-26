## ADDED Requirements

### Requirement: Glob pattern matching for namespace exclusion
The `ExternalTypeResolver` SHALL support glob patterns in `excludeNamespaces` where `*` matches any sequence of characters. Patterns SHALL be anchored — they must match the full type name from start to end. The `.` character SHALL be treated as a literal character, not a wildcard.

#### Scenario: Pattern matches full namespace prefix
- **WHEN** `excludeNamespaces` contains `"Microsoft.AspNetCore.*"` and the vendor type hint is `"Microsoft.AspNetCore.Mvc.ProblemDetails"`
- **THEN** the type is excluded from reuse

#### Scenario: Pattern does not match partial namespace
- **WHEN** `excludeNamespaces` contains `"System.*"` and the vendor type hint is `"SystemMonitor.Types.Foo"`
- **THEN** the type is NOT excluded (because `System.` requires a literal dot after `System`)

#### Scenario: Pattern with wildcard at start
- **WHEN** `excludeNamespaces` contains `"*.Internal.*"` and the vendor type hint is `"MyApp.Internal.Secret"`
- **THEN** the type is excluded

#### Scenario: Pattern must match entire type name
- **WHEN** `excludeNamespaces` contains `"Microsoft.*"` and the vendor type hint is `"Microsoft.AspNetCore.Mvc.ProblemDetails"`
- **THEN** the type IS excluded (because `*` matches `AspNetCore.Mvc.ProblemDetails`)

#### Scenario: Multiple patterns — any match excludes
- **WHEN** `excludeNamespaces` contains `["System.*", "Microsoft.*"]` and the vendor type hint is `"System.Collections.Generic.List"`
- **THEN** the type is excluded (first pattern matches)

#### Scenario: Generic type matched by pattern
- **WHEN** `excludeNamespaces` contains `"SampleApi.*"` and the vendor type hint is `"SampleApi.Models.PagedResult<OtherNamespace.Pet>"`
- **THEN** the type IS excluded (the full string starts with `SampleApi.` and `*` matches the rest including angle brackets)

### Requirement: Glob pattern matching is case-sensitive
`excludeNamespaces` pattern matching SHALL use ordinal (case-sensitive) comparison, consistent with CLR type name case sensitivity.

#### Scenario: Case-sensitive namespace pattern
- **WHEN** `excludeNamespaces` contains `"system.*"` and the vendor type hint is `"System.String"`
- **THEN** the type is NOT excluded (lowercase `system` does not match `System`)

### Requirement: Exact type name exclusion
The `ExternalTypeResolver` SHALL support exact type name matching in `excludeTypes`. Matching SHALL use ordinal string comparison against the full vendor type hint value.

#### Scenario: Exact match excludes
- **WHEN** `excludeTypes` contains `"Microsoft.AspNetCore.Mvc.ProblemDetails"` and the vendor type hint is `"Microsoft.AspNetCore.Mvc.ProblemDetails"`
- **THEN** the type is excluded from reuse

#### Scenario: Partial match does not exclude
- **WHEN** `excludeTypes` contains `"Microsoft.AspNetCore.Mvc.Problem"` and the vendor type hint is `"Microsoft.AspNetCore.Mvc.ProblemDetails"`
- **THEN** the type is NOT excluded

#### Scenario: Case-sensitive matching
- **WHEN** `excludeTypes` contains `"sampleapi.models.pet"` and the vendor type hint is `"SampleApi.Models.Pet"`
- **THEN** the type is NOT excluded (ordinal comparison is case-sensitive)

### Requirement: Empty exclusion configuration means all vendor hints are honoured
When no `excludeNamespaces` or `excludeTypes` are configured, the `ExternalTypeResolver` SHALL honour all valid vendor type hints and mark those schemas as external.

#### Scenario: No exclusions configured
- **WHEN** `excludeNamespaces` is empty, `excludeTypes` is empty, and a schema has `VendorTypeHint = "SampleApi.Models.Pet"`
- **THEN** the schema is marked as external with `ExternalClrTypeName = "SampleApi.Models.Pet"`

### Requirement: Diagnostic emitted for excluded types
When a vendor type hint is present but excluded by configuration, the `ExternalTypeResolver` SHALL emit a diagnostic `AS500` at info severity indicating the type was excluded and code will be generated instead.

#### Scenario: Type excluded by namespace pattern
- **WHEN** `excludeNamespaces` contains `"Microsoft.*"` and a schema has `VendorTypeHint = "Microsoft.AspNetCore.Mvc.ProblemDetails"`
- **THEN** a diagnostic `AS500` (info) is emitted with message containing the type name and noting exclusion by configuration

#### Scenario: Type excluded by exact name
- **WHEN** `excludeTypes` contains `"SampleApi.Models.Pet"` and a schema has `VendorTypeHint = "SampleApi.Models.Pet"`
- **THEN** a diagnostic `AS500` (info) is emitted
