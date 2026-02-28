## ADDED Requirements

### Requirement: Structured client namespaces align with folder roles

When output style is `TypedClientStructured`, client-side generated files SHALL use role-aligned namespaces.

Required mapping:
- Interfaces in `Contracts/` use `{RootNamespace}.Contracts`
- Client implementations in `Clients/` use `{RootNamespace}.Clients`
- Runtime helpers in `Infrastructure/` use `{RootNamespace}.Infrastructure`
- DI/options/enum extensions in `Configuration/` use `{RootNamespace}.Configuration`

#### Scenario: Structured interface and implementation namespaces
- **WHEN** a spec generates client interfaces and implementations in structured mode
- **THEN** interfaces are emitted with `{RootNamespace}.Contracts`
- **THEN** implementations are emitted with `{RootNamespace}.Clients`
- **THEN** implementation references to interfaces and models are fully valid across namespace boundaries

#### Scenario: Structured DI extension references segmented namespaces
- **WHEN** DI registration extension is generated in structured mode
- **THEN** extension type is emitted in `{RootNamespace}.Configuration`
- **THEN** it references client interfaces/implementations using segmented namespaces correctly

#### Scenario: Flat mode keeps root client namespaces
- **WHEN** output style is `TypedClientFlat`
- **THEN** interfaces, implementations, and helper files use `{RootNamespace}`
