## 1. Namespace mapping implementation

- [x] 1.1 Add structured namespace role mapping helper(s) for Contracts, Clients, Models, Infrastructure, and Configuration
- [x] 1.2 Update client emitter namespace assignment to use role-aligned namespaces in `TypedClientStructured`
- [x] 1.3 Update model emitter namespace assignment to use role-aligned namespaces in `TypedClientStructured`
- [x] 1.4 Keep `TypedClientFlat` namespace behavior unchanged (root namespace)

## 2. Cross-namespace reference correctness

- [x] 2.1 Ensure client implementations reference contract/model/infrastructure/configuration namespaces correctly in structured mode
- [x] 2.2 Ensure DI/service extension generation compiles with segmented namespaces
- [x] 2.3 Ensure JsonSerializerContext and json options wrappers resolve model/context types across segmented namespaces

## 3. Test coverage and validation

- [x] 3.1 Add/adjust emitter tests asserting structured namespace values per folder role
- [x] 3.2 Add/adjust generation integration tests to verify structured namespace coherence and compilability
- [x] 3.3 Add/adjust flat-mode tests to verify root-namespace behavior remains unchanged
- [x] 3.4 Run full solution build/tests and resolve regressions

## 4. Documentation updates

- [x] 4.1 Add `CHANGELOG.md` `Unreleased` entry for structured namespace alignment behavior
- [x] 4.2 Update `README.md` layout documentation to clarify namespace behavior in structured vs flat modes
