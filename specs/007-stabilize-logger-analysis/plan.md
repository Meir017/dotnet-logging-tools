# Implementation Plan: Stabilize Logger Analysis

**Branch**: `007-stabilize-logger-analysis` | **Date**: 2026-08-07 | **Spec**: [spec.md](spec.md)

## Summary

Close the highest-value correctness gaps in the core extractor, prove compatibility with current C#/.NET logging patterns, establish a repeatable scale baseline, and remove release credibility issues. Changes remain within existing projects and use integration tests through `LoggerUsageExtractor`.

## Technical Context

**Language/Version**: C# 14, .NET 10  
**Primary Dependencies**: Microsoft.CodeAnalysis 5.6, Microsoft.Extensions.Logging 10.x, xUnit v3, AwesomeAssertions  
**Storage**: N/A  
**Testing**: Existing `dotnet test` projects and VS Code npm scripts  
**Target Platform**: Cross-platform .NET SDK and VS Code extension CI  
**Project Type**: Multi-project library/tooling repository  
**Performance Goal**: Single syntax tree up to 5,000 lines analyzed in under 500 ms after warm-up  
**Constraints**: Symbol-based detection, public-entrypoint integration tests, thread-safe analyzers, no new report schema  
**Scale/Scope**: Core library plus documentation, package metadata, and CI configuration

## Constitution Check

### Code Quality Gates

- [x] **Symbol Fidelity**: direct `ILogger.Log` detection uses symbols already resolved by `LoggingTypes`.
- [x] **Thread Safety**: changes add no analyzer state or shared mutable collections.
- [x] **Error Handling**: unknown state expressions produce partial results rather than exceptions.
- [x] **Performance**: a scale test covers the documented single-tree contract.

### Testing Gates

- [x] **Test-First**: direct logger and summary tests are changed before implementation.
- [x] **Test Coverage**: basic, dynamic, unsupported, and modern syntax scenarios use public entrypoints.
- [x] **Performance Tests**: a repeatable scale test measures the core extraction path.

### User Experience Gates

- [x] **Output Consistency**: populated summaries originate in the shared extraction result.
- [x] **Accessibility**: no HTML behavior changes.
- [x] **Schema Versioning**: no model or JSON schema changes.

### Documentation Gates

- [x] **XML Documentation**: no new public API is planned.
- [x] **Change Documentation**: README roadmap and supported APIs are updated.
- [x] **Example Updates**: unsupported and completed roadmap items are clarified.

## Project Structure

```text
src/LoggerUsage/
├── Analyzers/LogMethodAnalyzer.cs
├── LoggerUsageExtractor.cs
└── existing extraction helpers

test/LoggerUsage.Tests/
├── LoggerMethodsTests.cs
├── LoggerMessageAttributeTests.cs
├── LoggerUsageExtractorTests.cs
└── performance/compatibility coverage in existing test project

src/LoggerUsage.VSCode/package.json
.github/workflows/vscode-extension.yml
Directory.Build.props
README.md
specs/007-stabilize-logger-analysis/
```

**Structure Decision**: Reuse existing core and integration-test projects. Do not introduce a benchmark project until a repeatable test demonstrates that a dedicated runner is necessary.

## Workstreams

### 1. Core correctness

1. Convert the skipped `ILogger.Log<TState>` theory into expected-result tests.
2. Distinguish direct interface methods from extension methods in `LogMethodAnalyzer`.
3. Extract direct-call message state without treating formatter delegates as templates.
4. Populate summaries in `ExtractLoggerUsagesWithSolutionAsync`.
5. Add equivalence tests for workspace and compilation summaries.

### 2. Modern compatibility

1. Verify `[LoggerMessage]` in a type using a primary-constructor logger.
2. Verify omitted and empty message values.
3. Verify framework logging inside a C# 14 extension block.
4. Verify custom span-based helpers are not false positives.
5. Verify unbound-generic `nameof` constant extraction where valid.
6. Fix only defects demonstrated by these tests.

### 3. Performance baseline

1. Generate a deterministic near-5,000-line syntax tree in memory.
2. Warm the extractor once to avoid first-use/JIT noise.
3. Measure the same public extraction entrypoint used by consumers.
4. Assert result completeness and a stable latency budget.
5. Keep the test isolated from network, MSBuild loading, and disk variability.

### 4. Release hygiene

1. Replace stale repository URLs in package metadata.
2. Make TypeScript lint blocking in CI.
3. Update README supported APIs and roadmap status.
4. Search active manifests and documentation for stale repository references.

### 5. Integration and release

1. Review merged workstreams for duplicated or conflicting behavior.
2. Run targeted tests, full .NET tests, VS Code lint/compile/tests, and pack/build checks already defined by the repository.
3. Inspect the final diff for unrelated changes.
4. Commit and push through the approved repository workflow.
5. Create a PR with scope, implementation notes, risk notes, and validation evidence.

## Risk Management

| Risk | Mitigation |
|---|---|
| Direct `ILogger.Log<TState>` state is not always a message template | Extract only statically known strings; retain partial usage for other state types |
| Extension-block syntax shape differs from traditional methods | Assert behavior through semantic operations rather than syntax assumptions |
| Performance assertions are noisy | Warm up, isolate in-memory compilation, and use a conservative threshold tied to the constitution |
| Broad compatibility tests accidentally test source-generator diagnostics | Assert compilation diagnostics and extractor behavior separately |
| Parallel workstreams edit the same test file | Assign file ownership and integrate centrally before validation |

## Progress Tracking

- [x] Feature specification complete
- [x] Technical plan complete
- [x] Research decisions recorded
- [x] Task breakdown complete
- [x] Core correctness complete
- [x] Modern compatibility complete
- [x] Performance baseline complete
- [x] Release hygiene complete
- [x] Full validation passed
- [ ] Pull request created
