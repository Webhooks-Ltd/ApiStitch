## 1. Corpus scaffolding

- [x] 1.1 Define the real-world corpus layout under the integration tests, including fixture storage and manifest metadata for source, pinned revision, and expected outcome
- [x] 1.2 Add shared test models/helpers to load corpus metadata and iterate entries deterministically
- [x] 1.3 Decide and document whether the optional stress specimen runs in the default suite or under a separate trait/category

## 2. Harness implementation

- [x] 2.1 Add library-path regression tests that run `GenerationPipeline` for each corpus entry and assert the declared outcome class
- [x] 2.2 Add compile verification for corpus entries classified as `Success` or `SuccessWithWarnings`
- [x] 2.3 Add CLI-path regression tests that assert controlled exit codes, diagnostics, and absence of stack traces for each corpus entry
- [x] 2.4 Keep assertions stable by matching primarily on outcome class and diagnostic codes, using message fragments only where necessary

## 3. Seed corpus

- [x] 3.1 Add a pinned GitHub REST API fixture and classify its expected outcome
- [x] 3.2 Add a pinned Stripe fixture and classify its expected outcome
- [x] 3.3 Add a pinned DigitalOcean fixture and classify its expected outcome
- [x] 3.4 Add a pinned Microsoft Graph fixture and classify its expected outcome
- [x] 3.5 Evaluate a Kubernetes release snapshot as an optional stress specimen and either add it with appropriate gating or document why it is excluded

## 4. Regression workflow and documentation

- [x] 4.1 Document how future field failures should be promoted into either the real-world corpus or minimized repro fixtures
- [x] 4.2 Add an `Unreleased` entry to `CHANGELOG.md` only if the testing work introduces user-visible behavior or workflow changes; otherwise document in the change artifacts why no changelog update is needed
- [x] 4.3 Update `README.md` only if contributor or user workflow guidance changes because of the new regression harness; otherwise document in the change artifacts why no README update is needed
- [x] 4.4 Run the full test suite with the seeded corpus and fix any harness regressions before implementation is considered complete
