# external-type-capabilities Specification

## Purpose
TBD - created by archiving change language-neutral-external-type-capabilities. Update Purpose after archive.
## Requirements
### Requirement: External schemas expose semantic type capabilities
The semantic model SHALL expose capability metadata for reused external schemas so emitter behavior can be based on schema semantics rather than emitted-language type strings.

`ApiSchema` SHALL include `ExternalTypeKind` semantic classification that is assigned during external type resolution for schemas selected by type reuse.

#### Scenario: Non-special external type
- **WHEN** a schema is reused as `SampleApi.Models.Pet`
- **THEN** the schema is marked external
- **THEN** `ExternalTypeKind` is `None`

#### Scenario: JsonPatch external type
- **WHEN** a schema is reused as `Microsoft.AspNetCore.JsonPatch.SystemTextJson.JsonPatchDocument<SampleApi.Models.Pet>`
- **THEN** the schema is marked external
- **THEN** `ExternalTypeKind` is `JsonPatchDocument`

### Requirement: JSON compatibility policy is schema-graph based
The system SHALL determine generated-metadata compatibility by traversing schema graphs and evaluating semantic external type capabilities on all reachable nodes.

Traversal SHALL include schema properties, arrays, additionalProperties, and inheritance/allOf links, and SHALL avoid infinite loops for cyclic graphs.

For this change, `ExternalTypeKind.JsonPatchDocument` SHALL be treated as unsupported for generated metadata. `ExternalTypeKind.None` and any unlisted kinds SHALL be treated as compatible by default unless explicitly marked unsupported in policy.

When composition constructs (`oneOf`/`anyOf`/`not`) are present in the source schema and equivalent semantic links are not available on `ApiSchema`, the compatibility decision SHALL conservatively fall back to runtime JSON APIs.

#### Scenario: Wrapper object containing unsupported external kind
- **WHEN** schema `PatchWrapper` has property `patch` referencing a `JsonPatchDocument` schema
- **THEN** `PatchWrapper` is treated as unsupported for generated metadata
- **THEN** emitters route JSON serialization/deserialization through runtime APIs for `PatchWrapper`

#### Scenario: Collection containing unsupported external kind
- **WHEN** a request or response body schema is `IReadOnlyList<JsonPatchDocument<Pet>>`
- **THEN** collection metadata is excluded from generated JsonSerializerContext
- **THEN** emitters use runtime JSON serialization/deserialization path for that collection

#### Scenario: Compatible schema graph
- **WHEN** a schema graph contains only supported primitive/object/enum/generated and external kinds
- **THEN** emitters use generated metadata path with `_jsonOptions`

#### Scenario: Unknown external type kind default behavior
- **WHEN** an external schema has a non-`JsonPatchDocument` kind not explicitly listed as unsupported
- **THEN** the schema is treated as compatible for generated metadata in this change

#### Scenario: Composition cannot be represented in semantic graph
- **WHEN** a request or response schema includes `oneOf`, `anyOf`, or `not` that is not represented in `ApiSchema` links
- **THEN** compatibility falls back to runtime JSON APIs

### Requirement: Compatibility policy is reused across all JSON call sites
The same compatibility decision SHALL be applied consistently at every JSON serialization/deserialization call site in generated clients.

#### Scenario: JSON request body
- **WHEN** an operation request body schema is incompatible with generated metadata
- **THEN** generated code uses `JsonContent.Create(body)`

#### Scenario: JSON response body
- **WHEN** an operation response body schema is incompatible with generated metadata
- **THEN** generated code uses `ReadFromJsonAsync<T>(cancellationToken)`

#### Scenario: Multipart JSON part
- **WHEN** a multipart part uses JSON encoding and its schema is incompatible with generated metadata
- **THEN** generated code uses `JsonContent.Create(part)` for that part

