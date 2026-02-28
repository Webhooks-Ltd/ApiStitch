## MODIFIED Requirements

### Requirement: ExternalTypeResolver resolves vendor type hints to external CLR type names
The `ExternalTypeResolver` pipeline step SHALL iterate all schemas in the specification, evaluate each `VendorTypeHint` against the exclusion configuration, and set `ExternalClrTypeName` on schemas that qualify for reuse. The step SHALL run after `InheritanceDetector` and before `CSharpTypeMapper`.

For each schema resolved as external, the resolver SHALL also assign semantic `ExternalTypeKind` metadata on `ApiSchema` based on the resolved external root type (for example `JsonPatchDocument`). This metadata SHALL be language-agnostic and SHALL NOT depend on emitted C# type-name string matching in emitters.

#### Scenario: Schema with vendor type hint and no exclusions
- **WHEN** a schema has `VendorTypeHint = "SampleApi.Models.Pet"` and no exclusion patterns match
- **THEN** `ExternalClrTypeName` is set to `"SampleApi.Models.Pet"` and `IsExternal` is `true`
- **THEN** `ExternalTypeKind` is `None`

#### Scenario: Schema with vendor type hint excluded by namespace pattern
- **WHEN** a schema has `VendorTypeHint = "Microsoft.AspNetCore.Mvc.ProblemDetails"` and `excludeNamespaces` contains `"Microsoft.AspNetCore.*"`
- **THEN** `ExternalClrTypeName` remains `null` and `IsExternal` is `false`
- **THEN** a diagnostic `AS500` (info) is emitted: "Type 'Microsoft.AspNetCore.Mvc.ProblemDetails' excluded from reuse by configuration. Code will be generated."

#### Scenario: Schema with vendor type hint excluded by exact type name
- **WHEN** a schema has `VendorTypeHint = "SampleApi.Models.Pet"` and `excludeTypes` contains `"SampleApi.Models.Pet"`
- **THEN** `ExternalClrTypeName` remains `null` and `IsExternal` is `false`
- **THEN** a diagnostic `AS500` (info) is emitted

#### Scenario: Schema with no vendor type hint
- **WHEN** a schema has `VendorTypeHint = null`
- **THEN** `ExternalClrTypeName` remains `null` and `IsExternal` is `false`
- **THEN** no diagnostic is emitted

#### Scenario: JsonPatch external type classification
- **WHEN** a schema resolves to `ExternalClrTypeName = "Microsoft.AspNetCore.JsonPatch.SystemTextJson.JsonPatchDocument<SampleApi.Models.Pet>"`
- **THEN** `ExternalTypeKind` is set to `JsonPatchDocument`
- **THEN** emitters can consume this semantic metadata without C# type-string parsing
