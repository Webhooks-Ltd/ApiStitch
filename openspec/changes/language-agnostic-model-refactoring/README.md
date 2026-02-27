# language-agnostic-model-refactoring

Refactor semantic model to separate language-specific naming (CSharpTypeName, CSharpName, CSharpMethodName) from the core model. Enables multi-language emission (TypeScript, etc.) by making the pipeline language-agnostic up to the emitter boundary.
