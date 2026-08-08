# Implementation Plan: Harden Roslyn Pipeline and Add SARIF

**Branch**: `008-harden-roslyn-pipeline` | **Date**: 2026-04-01 | **Spec**: `specs/008-harden-roslyn-pipeline/spec.md`

## Summary

Deliver the 3-6 month roadmap phase in three ordered milestones:

1. Make Roslyn and MSBuild work cancellation-aware, remove artificial asynchrony, define generated-code policy, and surface workspace diagnostics.
2. Add repeatable solution-scale baselines and lightweight instrumentation, then remove repeated syntax and semantic work only where measurements prove value.
3. Project aggregate consistency data into deterministic source findings and serialize those findings as GitHub-compatible SARIF 2.1.0.

The implementation preserves current public overloads and report behavior. New cancellation/reporting context APIs are additive. Semantic recognition remains symbol-based; syntax names are only candidate filters.

## Technical Context

**Language/Version**: C# 14, .NET 10  
**Primary Dependencies**: Microsoft.CodeAnalysis 5.6, Microsoft.CodeAnalysis.Workspaces.MSBuild, Microsoft.Extensions.Logging, System.Text.Json  
**Storage**: None; caches are bounded to one extraction  
**Testing**: Microsoft Testing Platform, xUnit, FluentAssertions, integration tests through `LoggerUsageExtractor` public entrypoints  
**Target Platform**: Cross-platform .NET library and CLI; CI currently includes Windows and Ubuntu  
**Project Type**: Multi-project .NET repository  
**Performance Goals**: Existing 5,000-line median under 500 ms; 100-file peak managed memory under 500 MB; accepted optimization improves its target by at least 25% or removes at least 50% redundant semantic binds  
**Constraints**: No user-code execution, deterministic results under parallel analysis, no public API break, no literal semantic identity comparisons  
**Scale/Scope**: Single files through multi-project solutions with many LoggerMessage declarations and cross-project callers

## Constitution Check

### Code Quality Gates

- [x] **Symbol Fidelity**: Invocation indexes retain `IInvocationOperation`/`IMethodSymbol`; syntax-name filters must be followed by canonical symbol comparison.
- [x] **Thread Safety**: Indexes are immutable after construction or use concurrent collections scoped to one extraction.
- [x] **Error Handling**: Cancellation propagates; workspace diagnostics are surfaced; recoverable generated or semantic gaps degrade without false success.
- [x] **Performance**: Baselines precede optimization and constitutional latency/memory contracts remain blocking.

### Testing Gates

- [x] **Test-First**: Each implementation slice begins with public-entrypoint integration tests or report contract tests.
- [x] **Test Coverage**: Cancellation timing, generated-code false positives/negatives, workspace diagnostics, concurrency, deterministic ordering, and invalid paths are covered.
- [x] **Performance Tests**: Existing fixture is retained and solution/caller-search fixtures are added before optimization.

### User Experience Gates

- [x] **Output Consistency**: Existing formats remain unchanged; SARIF intentionally contains actionable findings rather than extraction inventory.
- [x] **Accessibility**: No HTML presentation change is required.
- [x] **Schema Versioning**: JSON extraction schema is unchanged; SARIF declares version 2.1.0 and its official schema URI.

### Documentation Gates

- [x] **XML Documentation**: Additive public cancellation/report-context APIs receive XML documentation.
- [x] **Change Documentation**: README and release notes document `.sarif`, cancellation overloads, and generated-code policy.
- [x] **Example Updates**: CLI quickstart includes local SARIF generation and GitHub upload.

## Project Structure

```text
src/LoggerUsage/
├── Analyzers/
│   ├── ILoggerUsageAnalyzer.cs
│   ├── LoggingAnalysisContext.cs
│   ├── InvocationAnalysisIndex.cs             # proposed
│   └── LoggerMessageAttributeAnalyzer*.cs
├── Diagnostics/
│   ├── LoggerUsageFinding.cs                  # proposed internal projection
│   ├── LoggerUsageFindingFactory.cs           # proposed
│   └── LoggerUsageRules.cs                    # proposed centralized taxonomy
├── GeneratedCode/
│   ├── IGeneratedCodeDetector.cs              # proposed
│   └── GeneratedCodeDetector.cs               # proposed
├── ReportGenerator/
│   ├── ILoggerReportGenerator.cs
│   ├── LoggerReportGeneratorFactory.cs
│   ├── ReportGenerationContext.cs             # proposed additive context
│   └── SarifLoggerReportGenerator.cs           # proposed
├── IWorkspaceFactory.cs
└── LoggerUsageExtractor.cs

src/LoggerUsage.MSBuild/
└── MSBuildWorkspaceFactory.cs

src/LoggerUsage.Cli/
└── LoggerUsageWorker.cs

test/LoggerUsage.Tests/
├── CancellationTests.cs                       # proposed
├── GeneratedCodeTests.cs                      # proposed
├── LoggerUsagePerformanceTests.cs
├── LoggerMessagePerformanceTests.cs           # proposed
└── SarifLoggerReportGeneratorTests.cs          # proposed

test/LoggerUsage.MSBuild.Tests/
└── MSBuildWorkspaceFactoryTests.cs

specs/008-harden-roslyn-pipeline/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/sarif-contract.md
└── tasks.md
```

**Structure decision**: Extend the existing library, MSBuild, CLI, and integration-test projects. Do not add a new production project or external SARIF dependency unless hand-authored `System.Text.Json` output fails schema validation.

## Architecture

### 1. Cancellation without a breaking analyzer contract

`ILoggerUsageAnalyzer.AnalyzeAsync(LoggingAnalysisContext)` already receives a context object. Add `CancellationToken` to that context rather than changing the analyzer method signature. Public extraction APIs gain overloads accepting a token; existing overloads delegate with `CancellationToken.None`.

`IWorkspaceFactory` keeps `Create(FileInfo)` and gains `Create(FileInfo, CancellationToken)`. The old method delegates to the new overload in built-in implementations. CLI and MCP callers use the cancellation-aware overload where their host provides a token.

Cancellation checkpoints:

1. Before opening a project or solution.
2. Before scheduling each project/tree/analyzer unit.
3. In syntax enumeration and index construction.
4. On every `GetRootAsync`, `GetSemanticModelAsync`, `GetCompilationAsync`, and SymbolFinder call.
5. Before summary generation and report serialization for large result sets.

Do not catch `OperationCanceledException` except to add context and rethrow when a host convention requires it.

### 2. Generated-code policy

Replace the extractor's `LoggerMessage.g.cs` suffix check with `IGeneratedCodeDetector`. Classification uses positive evidence:

- Roslyn source-generated document identity when available.
- A leading `<auto-generated` or `<autogenerated` marker in trivia within a bounded header scan.
- A known compiler/source-generator hint-name pattern only when paired with source-generated document metadata.

A suffix such as `.g.cs`, `.generated.cs`, or `.designer.cs` alone is insufficient. The detector returns a reason enum for diagnostics/tests. Cache the decision by `SyntaxTree` for one extraction.

### 3. Workspace diagnostics

Subscribe to `MSBuildWorkspace.WorkspaceFailed` before opening the project/solution. Log a structured event containing `Diagnostic.Kind`, `Diagnostic.Message`, and input path. Maintain a small per-open deduplication set because MSBuild can repeat equivalent diagnostics. Unsubscribe when ownership ends or rely on workspace disposal only where event lifetime cannot escape the factory.

Do not transform recoverable diagnostics into exceptions. Preserve existing exceptions for registration/open failures, but ensure cancellation is never wrapped as a generic load failure.

### 4. Measurement model

Introduce an internal per-extraction metrics sink disabled by default. Counters:

- syntax trees visited/skipped;
- invocation syntax candidates;
- `GetOperation` requests and successful invocation operations;
- syntax roots, semantic models, and compilations requested;
- LoggerMessage declarations;
- SymbolFinder searches and caller locations;
- cache hits/misses;
- elapsed time by extraction stage.

Tests can enable the sink through an internal test hook or DI service. Production reporting does not expose metrics in JSON/HTML/Markdown.

### 5. Shared invocation index

After recording the baseline, build one immutable `InvocationAnalysisIndex` per analyzed syntax tree:

```text
InvocationExpressionSyntax -> IInvocationOperation
```

The index performs one descendant traversal and at most one `GetOperation` for each invocation candidate. Log-method, BeginScope, LoggerMessage.Define, and local LoggerMessage invocation analysis consume this index rather than rescanning and rebinding independently.

The index is not allowed to classify semantics by member-name literals. Optional `nameof`-based syntax prefilters may reduce work only when every retained candidate is validated against symbols from `LoggingTypes`.

If the baseline shows index construction regresses trees with no relevant invocations, use a two-stage candidate collector:

1. Cheap syntax filter for supported invocation shapes and `nameof` names.
2. One semantic bind per candidate.

### 6. Cross-solution caller-search optimization

First propagate cancellation and instrument the existing SymbolFinder path. Then apply low-risk per-extraction caches:

- `ProjectId -> Compilation`
- `DocumentId -> SyntaxNode root`
- `DocumentId -> SemanticModel`
- `IMethodSymbol -> caller locations`

Use `SymbolEqualityComparer.Default` for symbol keys. Cache tasks only if concurrent consumers can request the same value; remove failed or cancelled task entries so cancellation is not permanently cached.

Do not replace `SymbolFinder.FindCallersAsync` with a custom solution-wide search unless:

- SymbolFinder remains at least 40% of total runtime after low-risk caching; and
- a prototype solution-wide symbol-keyed invocation index improves median runtime by at least 25% without increasing peak memory beyond the contract.

This is an explicit decision gate, not guaranteed scope.

### 7. Deterministic finding projection

Do not add findings to `LoggerUsageExtractionResult` in this phase. An internal `LoggerUsageFindingFactory` projects current results plus summary groups into source-level occurrences for report generators.

For each inconsistency group:

1. Match affected `MessageParameter` occurrences by exact name/type pairs.
2. Emit one finding per unique `(rule, normalized path, span, parameter name, parameter type)`.
3. Sort by normalized path, start line, end line, rule ID, parameter name, and type.
4. Build a deterministic message listing conflicting names/types in ordinal order.

This avoids changing JSON schema 2.0 and keeps SARIF focused on actionable alerts.

### 8. SARIF generation

Add `.sarif` to `LoggerReportGeneratorFactory`. The generator writes SARIF 2.1.0 with one run and a stable tool driver:

- tool name `LoggerUsage`;
- semantic version from assembly informational version when stable, otherwise omit;
- centralized rule metadata for `LUT001` and `LUT002`;
- `originalUriBaseIds` entry for `%SRCROOT%` when a source root is supplied;
- relative artifact URIs using `/`;
- one-based start/end lines;
- `partialFingerprints.primaryLocationLineHash` replaced by a project-owned stable key such as `loggerUsage/v1`;
- deterministic JSON property and array order;
- no generation timestamp.

The custom fingerprint input is:

```text
fingerprintVersion
ruleId
normalized relative path
normalized message template
parameter name
parameter type
sorted conflicting name/type set
```

It deliberately excludes absolute paths, line numbers, timestamps, and parallel result order. Hash with SHA-256 and lowercase hexadecimal output.

`ReportGenerationContext` carries `SourceRoot`. Existing `GenerateReport(result)` remains supported and delegates with an empty context. CLI uses the nearest Git repository root when available; otherwise the generator derives a common source root from the findings.

## Milestones and PR Slices

### Milestone A - Reliability foundation

**PR A1: Cancellation and async cleanup**

- Add compatible cancellation overloads.
- Add token to analysis context and propagate through Roslyn calls.
- Remove all artificial `Task.Yield`.
- Add cancellation integration tests.

**PR A2: Generated code and workspace diagnostics**

- Add detector and reason model.
- Replace suffix filtering and add generated/user-authored tests.
- Add `WorkspaceFailed` structured logging and MSBuild integration tests.

Exit gate: cancellation and generated-code behavior are deterministic; no performance optimization begins before this gate.

### Milestone B - Measurement and optimization

**PR B1: Scale fixtures and instrumentation**

- Commit single-tree, multi-file, multi-project, and caller-search fixtures.
- Add metrics sink and baseline documentation.
- Record medians/allocations in PR evidence, not hard-coded machine-specific absolute timings beyond constitutional limits.

**PR B2: Shared per-tree invocation index**

- Add the index and migrate invocation analyzers.
- Prove result equivalence.
- Require FR-012 improvement.

**PR B3: Cross-solution cache**

- Cache roots/models/compilations/caller results within extraction.
- Evaluate SymbolFinder decision gate.
- Implement broader invocation indexing only if the gate passes.

Exit gate: existing 5,000-line and 100-file contracts pass; target fixture improves; no output behavior changes.

### Milestone C - Findings and SARIF

**PR C1: Finding taxonomy and deterministic projection**

- Centralize rule metadata.
- Add occurrence projection, deduplication, messages, fingerprints, and ordering tests.

**PR C2: SARIF generator and CLI**

- Add serializer and `.sarif` factory support.
- Add source-root context, CLI wiring, schema tests, snapshots, and README examples.
- Validate with GitHub code scanning on a fixture repository/workflow.

Exit gate: repeated generation is deterministic, SARIF 2.1.0 validates, and GitHub accepts the upload without duplicate alerts on rerun.

## Testing Strategy

- **Public extraction integration tests**: all cancellation, generated-code, analyzer equivalence, and concurrency behavior runs through `LoggerUsageExtractor`.
- **MSBuild integration tests**: open controlled valid/invalid project fixtures and assert structured `WorkspaceFailed` records.
- **Pure contract tests**: finding projection, fingerprinting, and SARIF serialization are stable pure transformations and may be tested directly.
- **Determinism tests**: randomize input result order and execute parallel extraction repeatedly; compare normalized SARIF bytes.
- **Performance tests**: warm up, collect at least three samples, compare median, and keep fixtures deterministic. Use allocation counters and metrics rather than wall time alone.
- **Compatibility tests**: compile/use old public overloads, preserve existing report factory extensions, and assert JSON schema version remains 2.0.

Targeted commands:

```powershell
dotnet build .\logging-usage.slnx --no-restore
dotnet run --project .\test\LoggerUsage.Tests\LoggerUsage.Tests.csproj --no-build -- --progress off
dotnet run --project .\test\LoggerUsage.MSBuild.Tests\LoggerUsage.MSBuild.Tests.csproj --no-build -- --progress off
```

Use Microsoft Testing Platform class filters for targeted iterations before the full suite.

## Rollout and Compatibility

- Existing extraction and report APIs remain callable; new overloads are additive.
- Cancellation defaults to `CancellationToken.None`.
- Existing JSON, HTML, and Markdown serializers do not receive findings or metrics.
- SARIF rule IDs and fingerprint version become compatibility contracts after release.
- The generated-code policy change is called out because it may intentionally include user-authored `.g.cs` files previously excluded only by naming.
- If GitHub upload validation reveals a path-root mismatch, add an explicit CLI `--source-root` option rather than guessing from the current directory.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Public cancellation changes break analyzer plugins | High | Put token on existing context; preserve method signature and overloads |
| Shared operation index increases memory | Medium | Scope per tree/extraction, measure 100-file peak, use candidate filtering |
| SymbolFinder cancellation is coarse | Medium | Add checkpoints around calls and preserve cancellation after awaited work |
| Generated-code heuristics exclude user code | High | Require positive evidence; suffix alone never excludes |
| Workspace diagnostics are noisy/duplicated | Medium | Structured levels and per-open deduplication |
| SARIF fingerprints churn after line movement | High | Exclude line numbers and absolute paths from custom fingerprint |
| Aggregate summaries lack exact source identity | High | Re-project matched parameter occurrences from `Results`, not summary alone |
| Hand-authored SARIF diverges from schema | Medium | Schema/contract validation and GitHub upload smoke test |
| Performance tests are machine-sensitive | Medium | Gate absolute constitutional limits; use relative counters/medians for optimization |

## Decision Gates

1. **Analyzer API gate**: if cancellation cannot be propagated through `LoggingAnalysisContext`, stop and design an additive interface rather than breaking `ILoggerUsageAnalyzer`.
2. **Invocation index gate**: implement only after metrics demonstrate repeated semantic binding; require FR-012.
3. **SymbolFinder replacement gate**: use the 40% runtime and 25% improvement thresholds in section 6.
4. **SARIF dependency gate**: use `System.Text.Json` unless validation shows an external SARIF object model materially reduces correctness risk without excessive dependency cost.
5. **Public findings gate**: keep findings internal unless a non-SARIF consumer requirement appears; a future public diagnostic API requires a separate versioned design.

## Definition of Done

- All specification success criteria pass.
- Every optimization includes before/after evidence and equivalent extracted results.
- No semantic identity comparison was added using literal names.
- Existing report formats and public overloads remain compatible.
- SARIF reruns produce stable alert identities in GitHub.
- Documentation covers cancellation, generated-code behavior, `.sarif`, source roots, and CI upload.
