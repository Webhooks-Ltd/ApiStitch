## Why

ApiStitch currently skips operations when a success response uses an inline complex object schema, emitting `AS401`. This drops otherwise valid endpoints (for example inventory/map responses) and forces users to rewrite specs just to generate clients.

## What Changes

- Add generation support for inline success-response schemas instead of skipping operations for supported inline object/primitive/array forms.
- Create deterministic synthetic model types for supported inline response object shapes and wire them through client/model emission.
- Support inline primitive success responses directly as primitive return types (for example `string`), without synthetic model generation.
- For JSON `string` success responses, generate tolerant client deserialization that accepts both quoted JSON strings and raw unquoted text payloads returned by non-conformant servers.
- Keep clear diagnostics for unsupported inline compositions/nested-inline-object shapes and update `AS401` behavior so it is not emitted for supported inline response cases.

## Capabilities

### New Capabilities
- `inline-response-models`: Deterministic synthetic model generation for supported inline success-response object schemas.

### Modified Capabilities
- `operation-parsing`: Supported inline success-response schemas (object/primitive/array forms) are transformed into generated/typed responses instead of skipping operations.
- `model-emission`: Synthetic inline response models are emitted and included in JsonSerializerContext like other generated models.
- `client-emission`: Generated client methods for affected operations use the synthesized response model types.

## Impact

- Affected code: `OperationTransformer`, semantic model assembly, model/client emitters, diagnostics, and integration fixtures.
- APIs: previously skipped operations become generated methods with typed return models.
- Dependencies: no new external dependencies expected.
- Systems: improves compatibility with public OpenAPI specs that use inline response objects.
