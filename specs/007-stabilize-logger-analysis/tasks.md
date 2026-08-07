# Tasks: Stabilize Logger Analysis

## Planning

- [x] T001 Create feature branch and specification.
- [x] T002 Document technical decisions, risks, and acceptance criteria.

## Core correctness

- [x] T003 Update direct `ILogger.Log<TState>` integration tests to assert expected usage details.
- [x] T004 Implement direct logger method classification and safe state extraction.
- [x] T005 Add compilation-entrypoint summary tests.
- [x] T006 Populate compilation-entrypoint summaries and verify workspace equivalence.

## Modern compatibility

- [x] T007 Add primary-constructor `[LoggerMessage]` coverage.
- [x] T008 Add omitted/empty message coverage.
- [x] T009 Add C# 14 extension-block coverage.
- [x] T010 Add span-based custom helper negative coverage.
- [x] T011 Add unbound-generic `nameof` coverage where valid.
- [x] T012 Fix compatibility defects demonstrated by T007-T011.

## Performance baseline

- [x] T013 Add deterministic near-5,000-line extraction scale test.
- [x] T014 Record and enforce the supported latency contract.

## Release hygiene

- [x] T015 Correct repository URLs in package and extension metadata.
- [x] T016 Make VS Code lint blocking in CI.
- [x] T017 Update supported APIs and roadmap in README.
- [x] T018 Verify no active stale repository URLs remain.

## Integration and delivery

- [x] T019 Run targeted core and compatibility tests.
- [x] T020 Run full .NET validation; VS Code package validation deferred to CI because npm registry access is blocked locally.
- [x] T021 Review final diff and update plan status.
- [ ] T022 Commit, push, and create the pull request.
