## Why

Generated client files are currently emitted into a single flat folder, which becomes noisy as APIs grow and makes it harder to navigate interfaces, clients, models, and infrastructure helpers. We should support selectable output layouts and make structured layout the default to improve developer ergonomics.

## What Changes

- Add configurable output layout modes for generated files (flat and structured).
- Make structured layout the default output mode.
- Define deterministic folder placement rules for interfaces, clients, models, and infrastructure/config files in structured mode.
- Keep flat layout available as an explicit opt-in mode.
- Extend CLI/config parsing and validation to support the new layout options.

## Capabilities

### New Capabilities
- `output-layouts`: Defines supported generated file layout modes, default selection, and deterministic folder placement rules.

### Modified Capabilities
- `configuration`: `outputStyle` values and defaults change to support layout selection with structured default.
- `cli-and-file-output`: CLI output style handling and generated-file path behavior change to respect selected layout mode.
- `client-emission`: Relative file paths emitted by client/model/config emitters change based on selected layout mode.
- `model-emission`: Relative file paths for model files and JsonSerializerContext change based on selected layout mode.

## Impact

- Affected code: output style enum/config parsing, CLI option validation, emitter file path mapping, tests/snapshots.
- API surface: generated file paths change by default (structured layout).
- Dependencies: no new external dependencies expected.
- User impact: easier code navigation by default; explicit option retains flat layout when desired.
