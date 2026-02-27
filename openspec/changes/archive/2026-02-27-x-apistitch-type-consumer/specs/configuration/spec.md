## ADDED Requirements

### Requirement: TypeReuse configuration section in apistitch.yaml
`ApiStitchConfig` SHALL include a `TypeReuse` property of type `TypeReuseConfig` that defaults to an empty configuration (no exclusions). The YAML config file SHALL support a `typeReuse` section with `excludeNamespaces` and `excludeTypes` list properties.

#### Scenario: Config with typeReuse section
- **WHEN** `apistitch.yaml` contains:
  ```yaml
  spec: openapi.json
  typeReuse:
    excludeNamespaces:
      - "Microsoft.AspNetCore.*"
    excludeTypes:
      - "System.String"
  ```
- **THEN** `ApiStitchConfig.TypeReuse.ExcludeNamespaces` contains `["Microsoft.AspNetCore.*"]`
- **THEN** `ApiStitchConfig.TypeReuse.ExcludeTypes` contains `["System.String"]`

#### Scenario: Config without typeReuse section
- **WHEN** `apistitch.yaml` does not contain a `typeReuse` section
- **THEN** `ApiStitchConfig.TypeReuse` is a `TypeReuseConfig` with empty `ExcludeNamespaces` and `ExcludeTypes` lists

#### Scenario: Config with empty typeReuse section
- **WHEN** `apistitch.yaml` contains `typeReuse:` with no sub-properties
- **THEN** `ApiStitchConfig.TypeReuse` is a `TypeReuseConfig` with empty lists

#### Scenario: Config with only excludeNamespaces
- **WHEN** `apistitch.yaml` contains `typeReuse:` with only `excludeNamespaces`
- **THEN** `ExcludeNamespaces` is populated and `ExcludeTypes` is an empty list

### Requirement: Empty entries in exclusion lists are ignored
Empty or whitespace-only entries in `excludeNamespaces` and `excludeTypes` SHALL be ignored during config loading.

#### Scenario: Empty string in excludeNamespaces
- **WHEN** `apistitch.yaml` contains `excludeNamespaces: ["", "System.*"]`
- **THEN** `ExcludeNamespaces` contains only `["System.*"]` (the empty string is filtered out)

#### Scenario: Whitespace string in excludeTypes
- **WHEN** `apistitch.yaml` contains `excludeTypes: ["  ", "SampleApi.Models.Pet"]`
- **THEN** `ExcludeTypes` contains only `["SampleApi.Models.Pet"]`

### Requirement: TypeReuseConfig model
`TypeReuseConfig` SHALL be a class with `List<string> ExcludeNamespaces` (glob patterns for namespaces to exclude from reuse) and `List<string> ExcludeTypes` (exact fully-qualified type names to exclude from reuse). Both SHALL default to empty lists.

#### Scenario: Default TypeReuseConfig
- **WHEN** a `TypeReuseConfig` is created with default values
- **THEN** `ExcludeNamespaces` is an empty list and `ExcludeTypes` is an empty list
