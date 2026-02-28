## Context

Operation parsing currently skips endpoints when a success response uses an inline complex object schema, producing `AS401`. This behavior is safe but over-restrictive for common OpenAPI patterns (for example object maps in inventory endpoints) and removes callable methods from generated clients.

## Goals / Non-Goals

**Goals:**
- Support generation for supported inline success-response schemas (object + primitive + supported array forms) instead of skipping operations.
- Keep deterministic output with stable synthetic type naming.
- Reuse existing model/client emitter flows once synthetic schemas are in the semantic model.
- Preserve explicit diagnostics for unsupported inline response compositions.

**Non-Goals:**
- Support every inline schema form in this change (`oneOf`/`anyOf`/`allOf`/`not` remain out of scope).
- Add per-operation custom error model generation.
- Change request-body inline complex schema behavior.

## Decisions

1. **Add inline response schema support during operation parsing**
   - For supported inline success-response object schemas, create a generated synthetic `ApiSchema` and set it as `ApiResponse.Schema`.
   - For supported inline primitive success-response schemas, map directly to primitive `ApiSchema` without synthetic model creation.
   - Name synthetic schemas deterministically from operation method + status (for example `{MethodName}{StatusCode}Response`).
   - Maintain collision resolution with existing naming helper strategy.
   - Alternative considered: defer to emitter-level anonymous type generation. Rejected because emitters rely on semantic model types and deterministic file naming.

2. **Support constrained inline shapes first**
   - v1 support: inline `type: object` with properties and/or `additionalProperties` where each member resolves to existing supported primitive/ref/array-of-supported-member forms.
   - v1 support: inline primitive responses (`string`, numeric, boolean, date/date-time, uuid, binary/byte) mapped through existing primitive mapper.
   - v1 support: inline arrays whose items are supported refs/primitives.
   - v1 non-support: nested inline object members and inline composition nodes (`oneOf`/`anyOf`/`allOf`/`not`) inside the response schema tree.
   - Unsupported compositions and unresolved nested inline complex nodes continue emitting `AS401` with a specific reason.
   - Alternative considered: broad recursive inline complex support now. Rejected to reduce regression risk and scope.

3. **Thread synthetic schemas into spec-wide emission model**
   - Extend semantic assembly so synthetic inline response schemas are included in `ApiSpecification.Schemas` for model/context emission.
   - Keep ordering deterministic by stable sort on schema name.

4. **No client-template special casing**
   - Client emitter should treat synthetic response models like any normal schema type.
   - This keeps templates simpler and limits risk to parsing/model assembly layers.

5. **Select first supported success response in ascending 2xx order**
   - Continue evaluating 2xx responses in ascending status order until a supported success response is found.
   - If a lower 2xx response is unsupported (for example inline composition), do not immediately skip the operation when a later 2xx response is supported.
   - If no supported success response exists, preserve skip behavior with `AS401` diagnostics.

6. **AS401 message contract for response-inline failures**
   - Keep `AS401` code, but response-inline messages must include operation location, unsupported reason category, and remediation hint to move schema to `components` + `$ref`.
   - Distinguish response-inline messages from existing parameter/request-body `AS401` text.

7. **Tolerant runtime handling for JSON string responses**
   - For success responses typed as `string` under JSON content, generated client code reads raw response text and attempts JSON string unquoting when payload is quoted.
   - If server returns unquoted raw text with JSON content-type, generated client returns raw text instead of failing deserialization.
   - This tolerance is scoped to `string` responses only; other JSON response types remain strict deserialization.

## Risks / Trade-offs

- **[Risk] Synthetic name collisions with user schemas** → **Mitigation:** use naming helper collision resolution against existing schema names.
- **[Risk] Partial inline support may still produce AS401 in advanced specs** → **Mitigation:** emit clear unsupported-shape diagnostics and add targeted tests.
- **[Risk] More generated files for specs with many inline responses** → **Mitigation:** deterministic naming and dedup where identical generated names collide.
