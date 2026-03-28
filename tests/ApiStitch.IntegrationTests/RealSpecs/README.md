# Real-World Regression Corpus

This corpus captures pinned snapshots of public OpenAPI descriptions that have exposed generator failures or realistic large-spec behavior.

Guidelines:
- Keep fixtures repository-local and deterministic. Do not fetch live specs during tests.
- Record upstream source and pinned revision or snapshot provenance in `manifest.json`.
- Promote a newly observed field failure into this corpus when the full upstream shape matters.
- Add or update a minimized fixture under `Specs/` when the failure is understood well enough to isolate.
- Treat optional stress specimens as a separate category instead of part of the default suite.

Current stress-specimen decision:
- Kubernetes remains excluded for now because fixture size and runtime cost are not justified until the default corpus settles.

Documentation note:
- `CHANGELOG.md` records the addition of the pinned real-spec regression corpus.
- `README.md` documents the Git LFS prerequisite for contributors who want to run the full corpus locally.
