## 1. Inline response semantic model support

- [x] 1.1 Extend operation parsing to detect supported inline success-response object schemas and create synthetic ApiSchema instances
- [x] 1.2 Implement deterministic synthetic naming with collision-safe resolution against existing schema names
- [x] 1.3 Ensure unsupported inline response compositions still emit `AS401` with explicit reason text
- [x] 1.4 Implement success-response selection that continues past unsupported lower 2xx responses and chooses the first supported 2xx response
- [x] 1.5 Enforce response-inline `AS401` message contract (location + reason category + `$ref` remediation hint)
- [x] 1.6 Extend supported inline success-response handling to include inline primitive schemas (for example `type: string`) without emitting `AS401`

## 2. Spec assembly and emission wiring

- [x] 2.1 Include synthetic inline response schemas in `ApiSpecification.Schemas` deterministically
- [x] 2.2 Ensure model emission generates `.cs` files and JsonSerializerContext metadata for synthetic response schemas
- [x] 2.3 Verify client emission returns synthesized response types for supported inline object responses
- [x] 2.4 Add tolerant JSON string response handling for `string` return types (quoted JSON string and raw unquoted text)

## 3. Tests and verification

- [x] 3.1 Add regression test: Petstore `/store/inventory`-style inline object response generates method and model instead of skipping
- [x] 3.2 Add unit/integration coverage for deterministic synthetic naming and collision resolution
- [x] 3.3 Add negative coverage: unsupported inline compositions still skip with `AS401`
- [x] 3.4 Add coverage for supported vs unsupported shape matrix (additionalProperties primitive supported; nested inline object member unsupported)
- [x] 3.5 Add coverage for mixed 2xx responses (`200` unsupported inline, `201` supported) selecting the supported response without skipping method
- [x] 3.6 Run targeted parsing/emission tests and resolve failures
- [x] 3.7 Run full solution build/tests and confirm deterministic output behavior remains unchanged
- [x] 3.8 Add regression coverage for inline primitive success responses (Petstore `/user/login`-style `application/json` string schema)
- [x] 3.9 Add coverage for tolerant JSON string response generation path in client emitter templates

## 4. Documentation updates

- [x] 4.1 Add `CHANGELOG.md` `Unreleased` entry for inline response generation support and updated AS401 behavior
- [x] 4.2 Evaluate `README.md` impact; if usage/config guidance is unchanged, record rationale in change notes/tasks and skip README update

### Notes

- README update skipped intentionally: this change adjusts generation behavior for supported/unsupported response schema shapes but does not change installation, configuration keys, CLI flags, or end-user workflow steps.
