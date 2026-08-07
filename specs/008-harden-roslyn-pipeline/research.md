# Research: Roslyn Pipeline Hardening and SARIF

## Decision 1: Propagate cancellation through existing context

**Decision**: Add `CancellationToken` to `LoggingAnalysisContext`, add cancellation-aware public overloads, and preserve existing signatures by delegation.

**Rationale**: Every analyzer already receives the context. This reaches analyzer internals without breaking third-party implementations of the public plugin interface.

**Alternatives considered**:

- Add a token parameter to `ILoggerUsageAnalyzer.AnalyzeAsync`: direct but source-breaking for every analyzer implementation.
- Use ambient cancellation: difficult to test and unsafe under concurrent extractions.
- Cancel only the CLI task: does not stop Roslyn workspace or SymbolFinder work.

## Decision 2: Remove artificial asynchrony

**Decision**: Remove `Task.Yield()` from analyzers. Return completed tasks for synchronous work and await only genuine asynchronous Roslyn operations.

**Rationale**: Yielding adds scheduling overhead and obscures where cancellation and concurrency actually occur. The extractor already controls concurrency.

**Alternatives considered**:

- Keep yields for responsiveness: responsiveness should come from cancellation checkpoints and bounded scheduling.
- Convert every analyzer to synchronous interfaces: larger public API change with no immediate benefit for the cross-solution analyzer.

## Decision 3: Generated-code detection requires positive evidence

**Decision**: Use source-generated document identity and bounded generated-header inspection. File suffixes alone do not exclude a tree.

**Rationale**: A suffix-only policy is both incomplete and capable of hiding user code. Positive evidence gives explainable behavior and testable reasons.

**Alternatives considered**:

- Continue excluding only `LoggerMessage.g.cs`: misses other generators and is tied to one implementation.
- Exclude all `.g.cs`/`.generated.cs`: creates false negatives for legitimate source.
- Analyze everything and deduplicate later: wastes semantic work and can produce ambiguous source locations.

## Decision 4: Measure operations, not only wall time

**Decision**: Add internal counters for traversal, semantic binds, roots, compilations, and caller searches, alongside warmed median and allocation measurements.

**Rationale**: CI wall time is noisy. Operation counts identify regressions and prove that a proposed cache or index removes the intended work.

**Alternatives considered**:

- BenchmarkDotNet project: useful for microbenchmarks but adds tooling and does not naturally exercise the public async extraction pipeline.
- Stopwatch only: insufficient to diagnose why performance changed.

## Decision 5: Share invocation semantic binding per tree

**Decision**: Build one immutable invocation index per syntax tree after baselines confirm repeated work.

**Rationale**: Multiple analyzers currently traverse every invocation and call `GetOperation` independently. Sharing retains symbol fidelity while reducing duplicate Roslyn work.

**Alternatives considered**:

- One syntax walker per analyzer: current behavior and simplest ownership, but repeats expensive work.
- Name-only dispatch: faster but violates symbol-fidelity requirements and risks false positives.
- Register Roslyn operation actions: appropriate for `DiagnosticAnalyzer`, but this library operates over workspaces and custom result models.

## Decision 6: Optimize SymbolFinder incrementally

**Decision**: Instrument first, then cache roots, semantic models, compilations, and per-symbol caller results within one extraction. Replace SymbolFinder only if an explicit decision gate passes.

**Rationale**: SymbolFinder is suspected but not yet proven to dominate end-to-end time. Low-risk caching addresses known repeated project/document work without reimplementing caller semantics.

**Alternatives considered**:

- Build a solution-wide invocation map immediately: potentially faster, but increases memory and architectural scope.
- Persist cross-run caches: invalidation complexity conflicts with this stabilization phase.
- Search invocation text: cannot preserve semantic correctness.

## Decision 7: SARIF contains findings, not inventory

**Decision**: Emit only actionable parameter consistency findings. Initial rules are `LUT001` type mismatch and `LUT002` casing inconsistency.

**Rationale**: GitHub code scanning is an alert surface. Emitting all logging calls would be noisy and would not represent defects. Existing summaries already identify two actionable consistency categories.

**Alternatives considered**:

- Emit one SARIF result per logger usage: high noise and no remediation meaning.
- Emit one result per aggregate inconsistency: lacks a precise primary source location.
- Add sensitive-data findings: no repository policy currently defines when classification metadata is a violation.

## Decision 8: Keep the findings projection internal

**Decision**: Derive `LoggerUsageFinding` records from `LoggerUsageExtractionResult` only for SARIF, without adding a public `Findings` property.

**Rationale**: This avoids changing JSON schema 2.0 and lets rule/fingerprint behavior stabilize before becoming a general public API.

**Alternatives considered**:

- Add findings to extraction result: attractive long term but changes all formats and public schema.
- Generate SARIF directly from summary groups: cannot reliably locate every affected occurrence.

## Decision 9: Use stable project-owned fingerprints

**Decision**: Hash rule ID, relative path, message template, parameter identity, and sorted conflict set with a versioned SHA-256 algorithm. Exclude line number, timestamp, absolute path, and list order.

**Rationale**: GitHub uses partial fingerprints to track alerts. Source lines move frequently; machine paths and parallel ordering are unstable.

**Alternatives considered**:

- Hash the full SARIF result: changes on line movement and metadata changes.
- Use path plus line: simple but causes alert churn.
- Omit partial fingerprints: leaves duplicate tracking entirely to platform heuristics.

## Decision 10: Serialize SARIF with System.Text.Json first

**Decision**: Implement the small required SARIF 2.1.0 subset with typed internal records and `System.Text.Json`, validated against the schema and GitHub upload.

**Rationale**: The project already uses `System.Text.Json`; the initial SARIF surface is small. Avoiding a dependency is reasonable if contract tests prove correctness.

**Alternatives considered**:

- Microsoft SARIF SDK: richer model and validation helpers, but a sizable dependency for two rules and one run.
- Anonymous JSON objects: less code but weak compile-time contract and harder deterministic ordering.

## Sources Consulted

- Roslyn workspace and symbol APIs in Microsoft.CodeAnalysis 5.6 documentation.
- SARIF 2.1.0 OASIS specification and schema.
- GitHub code-scanning SARIF support documentation, including result limits, relative paths, rule IDs, and partial fingerprints.
- Current repository extraction, analyzer, MSBuild factory, report factory, CLI, summary, and performance-test implementations.

