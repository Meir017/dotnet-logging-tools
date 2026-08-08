# Tasks: Harden Roslyn Pipeline and Add SARIF

## Dependency Graph

```text
A1 cancellation contract ─┬─> A2 analyzer propagation ─> A3 async cleanup
                          └─> A4 workspace cancellation
A5 generated policy ─────────> A6 extractor integration
A7 workspace diagnostics

B1 fixtures ─> B2 metrics ─> B3 baseline evidence ─> B4 invocation index
                                             └──────> B5 cross-solution caches ─> B6 SymbolFinder gate

C1 rule taxonomy ─> C2 finding projection ─> C3 fingerprints ─> C4 SARIF serializer
                                                           └─> C5 report/CLI context
C4 + C5 ─> C6 schema/GitHub validation

A milestone complete + B result stability ─> C final integration
```

## Milestone A - Reliability Foundation

- [ ] **A001 - Add cancellation acceptance tests**: Add failing public-entrypoint tests for pre-cancelled extraction, cancellation during tree analysis, and cancellation during cross-project LoggerMessage caller discovery.
- [ ] **A002 - Extend analysis context**: Add a required/defaulted `CancellationToken` to `LoggingAnalysisContext` without changing `ILoggerUsageAnalyzer.AnalyzeAsync`.
- [ ] **A003 - Add extraction overloads**: Add cancellation-aware workspace and compilation extraction overloads; preserve existing overloads by delegation.
- [ ] **A004 - Propagate analyzer cancellation**: Pass the token to syntax roots, semantic models, compilations, SymbolFinder, loops, and parallel scheduling.
- [ ] **A005 - Remove artificial async**: Remove all analyzer `Task.Yield()` calls and use completed tasks for synchronous implementations.
- [ ] **A006 - Extend workspace factory compatibly**: Add `Create(FileInfo, CancellationToken)` while preserving `Create(FileInfo)`.
- [ ] **A007 - Wire host cancellation**: Pass available CLI/MCP host tokens into workspace creation, extraction, and file writing.
- [ ] **A008 - Define generated-code test matrix**: Add failing tests for source-generated documents, auto-generated headers, conventional suffixes without headers, and current LoggerMessage output.
- [ ] **A009 - Implement generated-code detector**: Add reusable positive-evidence classification and a reason enum.
- [ ] **A010 - Integrate generated-code policy**: Replace the `LoggerMessage.g.cs` suffix filter and cache classifications per extraction.
- [ ] **A011 - Add MSBuild integration test project**: Create `LoggerUsage.MSBuild.Tests`, add it to `logging-usage.slnx`, and reference the MSBuild integration library using the repository's Microsoft Testing Platform conventions.
- [ ] **A012 - Add WorkspaceFailed tests**: Create MSBuild integration fixtures that produce recoverable workspace diagnostics.
- [ ] **A013 - Surface workspace diagnostics**: Subscribe before open, log structured kind/message/path, deduplicate per open, and preserve fatal exceptions/cancellation.
- [ ] **A014 - Reliability gate**: Run targeted and full core/MSBuild tests; document additive APIs and generated-code behavior.

## Milestone B - Measurement and Optimization

- [ ] **B001 - Add deterministic scale fixtures**: Add multi-file, 100-file, multi-project, and cross-project LoggerMessage declaration/caller builders.
- [ ] **B002 - Add metrics abstraction**: Implement a disabled-by-default per-extraction metrics sink with syntax, bind, Roslyn retrieval, SymbolFinder, cache, and stage counters.
- [ ] **B003 - Instrument existing pipeline**: Measure current traversal, operation binding, roots/models/compilations, and caller-search work without changing behavior.
- [ ] **B004 - Record pre-optimization baselines**: Capture warmed medians, allocations, operation counts, result counts, and 100-file memory.
- [ ] **B005 - Add result-equivalence harness**: Compare normalized complete extraction results before and after candidate optimizations.
- [ ] **B006 - Prototype invocation index**: Build one immutable source-ordered invocation-operation index per syntax tree with cancellation.
- [ ] **B007 - Migrate invocation analyzers**: Move LogMethod, BeginScope, LoggerMessage.Define, and local LoggerMessage matching to the shared index.
- [ ] **B008 - Evaluate candidate prefilter**: If empty/non-logging trees regress, add `nameof` syntax candidate filtering followed by mandatory symbol validation.
- [ ] **B009 - Enforce invocation-index gate**: Keep the index only if it improves the target by 25% or removes 50% redundant binds without contract regressions.
- [ ] **B010 - Add per-extraction Roslyn caches**: Cache project compilations and document roots/models with cancellation-safe task eviction.
- [ ] **B011 - Cache caller searches**: Cache SymbolFinder results by method symbol within one extraction using `SymbolEqualityComparer.Default`.
- [ ] **B012 - Evaluate SymbolFinder replacement gate**: Measure runtime share; prototype solution-wide invocation mapping only if SymbolFinder remains at least 40% of runtime.
- [ ] **B013 - Enforce solution optimization gate**: Accept broader indexing only with at least 25% median improvement and memory below 500 MB.
- [ ] **B014 - Performance gate**: Confirm 5,000-line median below 500 ms, 100-file memory below 500 MB, deterministic results, and no concurrency regressions.

## Milestone C - Deterministic Findings and SARIF

- [ ] **C001 - Add rule contract tests**: Lock `LUT001`/`LUT002` IDs, names, descriptions, levels, and deterministic order.
- [ ] **C002 - Centralize rule metadata**: Implement immutable `LoggerUsageRules` definitions with no duplicated rule literals.
- [ ] **C003 - Add finding projection tests**: Cover single/multi-project type conflicts, casing groups, missing types, duplicate usages, and shuffled input.
- [ ] **C004 - Implement finding projection**: Match summary name/type groups back to exact result parameter occurrences and produce source-level findings.
- [ ] **C005 - Add deduplication and ordering**: Deduplicate by occurrence identity and sort using ordinal normalized keys.
- [ ] **C006 - Add fingerprint tests**: Prove stability across line movement, absolute root changes, input order, and parallel execution; prove changes for semantic conflict changes.
- [ ] **C007 - Implement fingerprint v1**: Normalize canonical fields, hash with SHA-256, and expose lowercase hexadecimal fingerprints.
- [ ] **C008 - Add report context compatibly**: Introduce source-root context while preserving existing report-generator calls.
- [ ] **C009 - Add SARIF contract tests**: Assert version/schema/tool/rules/results/locations/path rules/fingerprints and byte determinism.
- [ ] **C010 - Implement typed SARIF serializer**: Serialize the required SARIF 2.1.0 subset with `System.Text.Json` and no timestamp.
- [ ] **C011 - Register `.sarif`**: Add report-factory selection and preserve existing extensions/errors.
- [ ] **C012 - Wire CLI source root**: Derive source root from analyzed solution/project and add an explicit override if validation requires it.
- [ ] **C013 - Validate paths outside root**: Define explicit error/fallback behavior and prevent traversal artifact URIs.
- [ ] **C014 - Validate schema**: Validate fixture output against SARIF 2.1.0 and required GitHub fields.
- [ ] **C015 - Validate GitHub upload**: Upload the same fixture twice and confirm stable alert identity/no duplicates.
- [ ] **C016 - Update documentation**: Add README CLI example, GitHub Actions upload example, rule documentation, source-root behavior, and compatibility notes.
- [ ] **C017 - Final phase gate**: Run all core, CLI, MCP, MSBuild, determinism, and performance suites and review the diff for symbol-fidelity violations.

## Suggested Execution Order

1. Deliver A001-A014 as two small PRs.
2. Deliver B001-B005 before proposing optimization code.
3. Deliver B006-B014 as one or two independently measurable PRs.
4. Start C001-C007 after extraction result stability is proven; these pure tasks may overlap with late B work.
5. Deliver C008-C017 only after the findings contract is fixed.

## Stop Conditions

- Stop and redesign if cancellation requires breaking `ILoggerUsageAnalyzer`.
- Drop an optimization if it misses its measurement gate.
- Do not implement a custom caller search without the SymbolFinder decision gate.
- Do not publish findings on the extraction model without a separate schema/version decision.
- Do not release SARIF until repeated GitHub uploads retain alert identity.
