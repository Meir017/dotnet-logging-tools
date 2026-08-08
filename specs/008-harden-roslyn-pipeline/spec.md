# Feature Specification: Harden Roslyn Pipeline and Add SARIF

**Feature Branch**: `008-harden-roslyn-pipeline`  
**Created**: 2026-04-01  
**Status**: Planned  
**Input**: Harden the extraction pipeline for cancellation, diagnostics, generated code, and scale; then add deterministic SARIF output for actionable logging inconsistencies.

## Problem Statement

LoggerUsage extracts useful logging data, but several pipeline behaviors do not yet meet the reliability and scale expected from a solution-wide Roslyn tool:

- Long-running workspace, compilation, syntax, and caller-search operations cannot be cancelled end to end.
- Analyzers repeat broad syntax traversal and semantic binding, while some use artificial asynchronous yields.
- Generated-code exclusion relies on one file-name suffix and can both miss generated files and exclude legitimate user files.
- MSBuild workspace diagnostics are not surfaced consistently.
- The only performance contract covers one large syntax tree; solution and cross-project caller-search costs are not measured.
- Existing reports summarize inconsistencies but cannot be uploaded to GitHub code scanning.

The phase must first make the Roslyn pipeline predictable and measurable, then optimize only proven hot paths, and finally expose stable actionable findings through SARIF 2.1.0.

## User Scenarios and Testing

### User Story 1 - Cancel long-running analysis

As a CLI, MCP, or library consumer, I can cancel workspace loading and extraction so large or unhealthy solutions do not leave expensive Roslyn work running.

**Acceptance scenarios**

1. Given a cancellation token cancelled before extraction, when extraction starts, then it terminates through normal cancellation semantics without returning a success-shaped partial result.
2. Given cancellation during solution caller discovery, when Roslyn APIs observe the token, then caller discovery stops and `OperationCanceledException` is propagated.
3. Given an existing consumer using the current public overloads, when the library is upgraded, then the consumer continues to compile and receives the existing non-cancellable behavior.

### User Story 2 - Trust generated-code and workspace handling

As a user analyzing a real solution, I receive results from user-authored code without duplicates from generated logging implementations, and I receive actionable workspace diagnostics when projects fail to load fully.

**Acceptance scenarios**

1. Given source-generator output or a file marked with a recognized generated-code header, when extraction runs, then generated implementations are excluded.
2. Given a user-authored file whose name ends in `.g.cs` but has no generated-code evidence, when extraction runs, then its logging usages remain included.
3. Given recoverable `MSBuildWorkspace` diagnostics, when a project or solution loads, then each diagnostic is logged with structured failure kind and message.
4. Given a fatal workspace load failure, when creation cannot produce a usable workspace, then the original failure remains visible and is not converted into an empty successful result.

### User Story 3 - Analyze large solutions efficiently

As a maintainer, I can reproduce and measure syntax, semantic-binding, and cross-project caller-search costs before accepting performance changes.

**Acceptance scenarios**

1. Given the existing warmed 5,000-line fixture, when the optimized pipeline runs, then its median remains below 500 ms.
2. Given a deterministic multi-project fixture with LoggerMessage declarations and cross-project callers, when the performance test runs, then it reports stable median time, allocation, candidate count, semantic-bind count, and caller-search count.
3. Given an optimization, when compared with the committed pre-optimization baseline on the same machine and fixture, then the targeted metric improves by at least 25% and unrelated extraction results remain identical.
4. Given a 100-file fixture, when analysis completes, then peak managed memory remains below the constitutional 500 MB limit.

### User Story 4 - Upload findings to GitHub code scanning

As a CI user, I can request a `.sarif` report containing deterministic logging-consistency findings with repository-relative locations and stable fingerprints.

**Acceptance scenarios**

1. Given parameter type mismatches, when a SARIF report is generated, then each affected source occurrence produces rule `LUT001` at the correct location.
2. Given parameter casing inconsistencies, when a SARIF report is generated, then each affected source occurrence produces rule `LUT002` at the correct location.
3. Given identical source and extraction results, when SARIF is generated repeatedly, then rule IDs, result order, locations, messages, and partial fingerprints are byte-for-byte stable.
4. Given an output path ending in `.sarif`, when the CLI runs, then the report factory selects SARIF 2.1.0 and emits a file accepted by GitHub code scanning.
5. Given ordinary logger usages without an actionable inconsistency, when SARIF is generated, then they are not emitted as alerts.

## Edge Cases

- Cancellation is requested before workspace creation, during project loading, during syntax-root retrieval, during compilation retrieval, or during `SymbolFinder` work.
- A solution contains unloaded projects, missing references, duplicate workspace diagnostics, linked documents, or source-generated syntax trees.
- A user deliberately names a source file `Something.g.cs`.
- A generated file has no conventional suffix but has an auto-generated header.
- An inconsistency spans projects or contains several spellings and several parameter types.
- A source path is outside the selected source root, uses different casing, or cannot be made relative.
- An extraction result has an unknown line, missing parameter type, duplicate occurrences, or no inconsistencies.
- A performance fixture has no LoggerMessage declarations, no callers, or many declarations called from the same document.

## Requirements

### Pipeline hardening

- **FR-001**: Public extraction entrypoints MUST offer cancellation-aware overloads while preserving existing overloads.
- **FR-002**: Cancellation MUST flow through workspace creation, project and solution loading, syntax-root retrieval, compilation retrieval, analyzer execution, and SymbolFinder calls wherever the underlying API supports it.
- **FR-003**: Cancellation MUST propagate as cancellation and MUST NOT be logged or returned as a successful empty extraction.
- **FR-004**: Analyzers MUST not use `Task.Yield()` or equivalent artificial asynchrony.
- **FR-005**: Semantic identity MUST continue to use canonical Roslyn symbols. Syntax names may only reduce candidates before mandatory symbol validation.
- **FR-006**: Generated-code classification MUST use an explicit reusable policy based on generated-code evidence, not a single suffix check.
- **FR-007**: The generated-code policy MUST preserve user-authored files that merely use a generated-looking name.
- **FR-008**: MSBuild workspace failures MUST be surfaced with structured diagnostics, including diagnostic kind, message, and analyzed path.
- **FR-009**: Recoverable workspace diagnostics MUST not abort extraction; unrecoverable failures MUST remain failures.

### Measured performance

- **FR-010**: The repository MUST include deterministic single-tree, multi-file, multi-project, and cross-project LoggerMessage performance fixtures.
- **FR-011**: Instrumentation MUST count syntax candidates, semantic operation binds, compilation/root retrievals, and SymbolFinder searches without affecting normal report output.
- **FR-012**: Optimizations MUST be accepted only when a committed benchmark demonstrates at least 25% improvement in the targeted measured cost or removes at least 50% of redundant semantic binds.
- **FR-013**: Per-tree invocation traversal and `GetOperation` work MUST be shared when measurements confirm repeated analyzer work is material.
- **FR-014**: Per-solution roots, semantic models, compilations, and caller-search results MUST be cached only within one extraction and only when measured to improve the cross-project fixture.
- **FR-015**: Performance work MUST preserve extraction result equivalence and thread safety.
- **FR-016**: Existing latency and memory contracts MUST remain blocking.

### Findings and SARIF

- **FR-017**: SARIF MUST represent actionable findings derived from extraction results, not every logger usage.
- **FR-018**: The initial finding taxonomy MUST contain `LUT001` for parameter type mismatch and `LUT002` for parameter casing inconsistency.
- **FR-019**: Rule identifiers, names, descriptions, default levels, and help text MUST be centralized constants.
- **FR-020**: Every SARIF result MUST include a rule ID, deterministic message, physical location, and stable `partialFingerprints`.
- **FR-021**: Artifact paths MUST use forward-slash repository-relative URI form when a source root is available.
- **FR-022**: Findings and SARIF results MUST have deterministic ordering independent of analyzer parallelism.
- **FR-023**: Fingerprints MUST not depend on timestamps, absolute machine paths, or result-list order.
- **FR-024**: SARIF output MUST conform to SARIF 2.1.0 and GitHub code-scanning ingestion requirements.
- **FR-025**: The report factory and CLI MUST recognize `.sarif` without changing existing JSON, HTML, or Markdown output.
- **FR-026**: Any additive public reporting context MUST preserve existing `ILoggerReportGenerator` consumers through compatible overloads or default behavior.

## Initial Finding Taxonomy

| Rule | Meaning | Default level | Result granularity |
|------|---------|---------------|--------------------|
| `LUT001` | The same case-sensitive logging parameter name is associated with multiple source types | warning | One result for each affected parameter occurrence |
| `LUT002` | Logging parameter names differ only by casing | note | One result for each affected parameter occurrence |

Data-classification metadata and extraction uncertainty are intentionally not findings in this phase because the project has no user-configurable policy that makes them actionable violations.

## Success Criteria

- **SC-001**: All cancellation acceptance tests pass for extraction, workspace loading, and cross-project caller discovery.
- **SC-002**: No analyzer contains artificial asynchronous yielding.
- **SC-003**: Generated-code fixtures prove generated implementations are excluded and user-authored `.g.cs` files are retained.
- **SC-004**: Workspace diagnostics are observable in integration tests.
- **SC-005**: The warmed 5,000-line median remains under 500 ms.
- **SC-006**: The 100-file fixture remains under 500 MB peak managed memory.
- **SC-007**: Each accepted optimization meets FR-012 on its targeted fixture with identical functional output.
- **SC-008**: Repeated SARIF generation from identical input is deterministic.
- **SC-009**: SARIF validation and a GitHub code-scanning upload smoke test succeed.
- **SC-010**: Existing JSON, HTML, Markdown, CLI, MCP, and extraction tests remain behaviorally unchanged except for intentional additive APIs.

## Non-Goals

- Implementing a Roslyn `DiagnosticAnalyzer` or IDE live analyzer.
- Incremental workspace watching or persistent cross-run caches.
- Executing formatter delegates or user code.
- Emitting every extracted logging call as a SARIF result.
- Adding policy rules for sensitive data, EventId conventions, template grammar, or log-level selection.
- Redesigning all public extraction models.
- Parallelizing beyond measured need or replacing Roslyn SymbolFinder before profiling demonstrates that it is necessary.

## Dependencies and Delivery Order

1. Cancellation, generated-code policy, and workspace diagnostics establish predictable behavior.
2. Instrumentation and baseline fixtures establish measurable costs.
3. Shared per-extraction indexes and caches are introduced only against those baselines.
4. A deterministic findings projection is added after extraction semantics are stable.
5. SARIF serialization, CLI integration, documentation, and GitHub upload validation complete the phase.

