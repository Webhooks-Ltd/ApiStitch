## MODIFIED Requirements

### Requirement: Emit one file per type

The model emitter SHALL emit one `.cs` file per object/record/enum schema and one JsonSerializerContext file.

When output style is `TypedClientStructured`:
- model files SHALL be emitted under `Models/`
- JsonSerializerContext SHALL be emitted under `Infrastructure/`

When output style is `TypedClientFlat`:
- model files and JsonSerializerContext SHALL be emitted at output root.

#### Scenario: simple model emission with structured layout
- **WHEN** schemas include `Pet` object, `Category` object, and `PetStatus` enum and output style is `TypedClientStructured`
- **THEN** files include `Models/Pet.cs`, `Models/Category.cs`, `Models/PetStatus.cs`, and `Infrastructure/{ClientName}JsonContext.cs`

#### Scenario: simple model emission with flat layout
- **WHEN** schemas include `Pet` object, `Category` object, and `PetStatus` enum and output style is `TypedClientFlat`
- **THEN** files include `Pet.cs`, `Category.cs`, `PetStatus.cs`, and `{ClientName}JsonContext.cs` at output root

#### Scenario: deterministic model path ordering
- **WHEN** generation runs repeatedly with same input and output style
- **THEN** emitted model/context relative paths are stable and deterministic
