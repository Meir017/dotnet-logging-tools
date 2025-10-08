# Implementation Plan: LoggerUsageExtractor Progress Reporting and AdhocWorkspace Support

**Branch**: `003-loggerusageextractor-improvements-with` | **Date**: October 8, 2025 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/003-loggerusageextractor-improvements-with/spec.md`

## Summary

Add `IProgress<T>` support to `LoggerUsageExtractor` for progress reporting at project, file, and analyzer levels. Automatically create `AdhocWorkspace` when analyzing single `Compilation` without a `Solution` to enable Solution APIs like `SymbolFinder`. Maintain backward compatibility through optional parameters.

## Technical Context

**Language/Version**: C# 12 / .NET 10
**Primary Dependencies**: Microsoft.CodeAnalysis (Roslyn) 4.x, Microsoft.Extensions.Logging 9.x
**Storage**: N/A (in-memory analysis only)
**Testing**: xUnit 2.x with FluentAssertions
**Target Platform**: Cross-platform .NET 10 (Windows, Linux, macOS)
**Project Type**: Single project (library with CLI and VS Code Bridge consumers)
**Performance Goals**: < 5% overhead for progress reporting, < 500ms AdhocWorkspace creation
**Constraints**: < 5% performance overhead, < 10% memory increase, thread-safe
**Scale/Scope**: Analyze workspaces with 100+ projects, 10k+ files

## Constitution Check

### Code Quality Gates

- [x] **Symbol Fidelity**: No string-based type/method comparisons
  - Rationale: Uses existing `LoggingTypes` symbol resolution
- [x] **Thread Safety**: Analyzers stateless, thread-safe collections
  - Rationale: `IProgress<T>` pattern is thread-safe by design
- [x] **Error Handling**: Graceful degradation implemented
  - Rationale: Progress failures logged but don't abort; workspace creation falls back
- [x] **Performance**: Analysis meets contracts
  - Rationale: < 5% overhead, < 500ms latency, < 10% memory with tests

### Testing Gates

- [x] **Test-First**: Tests before implementation
  - Rationale: Contract tests generated in Phase 1
- [x] **Test Coverage**: Basic, edge, error, thread safety
  - Rationale: 20+ test scenarios in spec
- [x] **Performance Tests**: Benchmark tests verify contracts
  - Rationale: BenchmarkDotNet tests for overhead/latency/memory

### User Experience Gates

- [x] **Output Consistency**: All formats equivalent
  - Rationale: Progress orthogonal to output formats
- [x] **Accessibility**: Dark mode, semantic markup
  - Rationale: No UI changes
- [x] **Schema Versioning**: JSON schema updated if needed
  - Rationale: `LoggerUsageProgress` internal only

### Documentation Gates

- [x] **XML Documentation**: Public APIs documented
  - Rationale: Spec includes complete XML docs
- [x] **Change Documentation**: Breaking changes documented
  - Rationale: No breaking changes, migration guide provided
- [x] **Example Updates**: README/quickstart updated
  - Rationale: quickstart.md with examples in Phase 1

## Project Structure

### Documentation (this feature)

```
specs/003-loggerusageextractor-improvements-with/
├── spec.md              # Feature specification (complete)
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── LoggerUsageProgress.json
│   └── ExtractorAPI.json
└── tasks.md             # Phase 2 output (/tasks command)
```

### Source Code

```
src/LoggerUsage/
├── Models/
│   ├── LoggerUsageProgress.cs         # NEW
│   ├── LoggerUsageExtractionResult.cs # EXISTING
│   └── LoggerUsageInfo.cs             # EXISTING
├── Services/
│   ├── LoggerUsageExtractor.cs        # MODIFIED
│   └── ProgressReporter.cs            # NEW
├── Utilities/
│   └── WorkspaceHelper.cs             # NEW
└── LoggerUsage.csproj

test/LoggerUsage.Tests/
├── ProgressReportingTests.cs          # NEW
├── AdhocWorkspaceTests.cs             # NEW
├── PerformanceTests.cs                # NEW
└── ExtractLoggerUsagesFromWorkspaceTests.cs # MODIFIED
```

**Structure Decision**: Single project maintained. Core library gets new models/helpers. Consumers (CLI, VS Code Bridge) updated to use progress reporting.

## Phase 0: Research & Decisions

All research complete - key decisions:

1. **IProgress<T> Pattern**: Use standard .NET `IProgress<T>.Report()` with pre-calculated percentages
   - Thread-safe by design via synchronization context capture
   - Integrates with Task-based APIs
   
2. **AdhocWorkspace Lifecycle**: Create workspace, return for disposal via `using` pattern
   - Explicit resource management
   - Clear ownership semantics

3. **Progress Calculation**: Pre-calculate total work units, atomic counter, O(1) percentage calculation
   - Accurate and performant

4. **Performance Measurement**: BenchmarkDotNet with baseline comparisons
   - Statistical significance guaranteed

## Phase 1: Design & Contracts

### Data Model

**LoggerUsageProgress** (Models/LoggerUsageProgress.cs):
- `PercentComplete` (int, 0-100, required)
- `OperationDescription` (string, required, max 200 chars)
- `CurrentFilePath` (string?, optional)
- `CurrentAnalyzer` (string?, optional)
- Immutable record

**ProgressReporter Helper**:
- `ReportProjectProgress(index, name)`
- `ReportFileProgress(index, name)`
- `ReportAnalyzerProgress(analyzer, file)`

**WorkspaceHelper**:
- `CreateAdhocWorkspaceAsync(Compilation) → (Solution, IDisposable?)`

### API Contracts

**ExtractLoggerUsagesAsync**:
- New parameters: `IProgress<LoggerUsageProgress>? progress = null`, `CancellationToken cancellationToken = default`
- Progress reports at: project, file, analyzer levels
- Backward compatible (optional parameters)

**ExtractLoggerUsagesWithSolutionAsync**:
- New parameters: `IProgress<LoggerUsageProgress>? progress = null`, `CancellationToken cancellationToken = default`
- Auto-creates AdhocWorkspace when `solution == null`
- Disposes workspace after analysis

### Contract Tests (15 tests)

Progress Reporting:
- Progress structure validation
- Percentage range (0-100) validation
- Non-empty description validation
- Project-level progress reporting
- File-level progress reporting
- Analyzer-level progress reporting
- Null progress parameter handling
- Concurrent progress reports (thread safety)

AdhocWorkspace:
- Workspace creation when solution is null
- Workspace disposal after analysis
- Solution provided to analyzers
- SymbolFinder API accessibility
- Fallback on workspace creation failure

Performance:
- Progress overhead < 5%
- Workspace creation < 500ms

### Quickstart Examples

**Example 1: CLI Progress Bar**
```csharp
var progress = new Progress<LoggerUsageProgress>(p =>
    Console.WriteLine($"[{p.PercentComplete}%] {p.OperationDescription}"));
var result = await extractor.ExtractLoggerUsagesAsync(workspace, progress);
```

**Example 2: VS Code Bridge**
```csharp
var progress = new Progress<LoggerUsageProgress>(p =>
    context.ReportProgress(p.PercentComplete, p.OperationDescription));
var result = await extractor.ExtractLoggerUsagesAsync(workspace, progress, cts.Token);
```

**Example 3: Single Compilation with AdhocWorkspace**
```csharp
// AdhocWorkspace created automatically
var result = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);
```

## Phase 2: Task Planning Approach

*Executed by /tasks command - NOT by /plan*

**Task Generation**:
1. Load `.specify/templates/tasks-template.md`
2. Generate from contracts: 3 model tasks, 15 test tasks
3. Generate implementation: 6 core tasks
4. Generate consumer updates: 2 tasks
5. Generate validation: 7 tasks

**Ordering**:
- Phase 1: Models (3 tasks, parallel)
- Phase 2: Contract Tests (15 tasks, parallel)
- Phase 3: Core Implementation (6 tasks, sequential)
- Phase 4: Consumer Updates (2 tasks, parallel)
- Phase 5: Validation (7 tasks, parallel)

**Estimated Output**: 33 tasks (21 parallelizable = 63%)

## Phase 3+: Future Implementation

**Phase 3**: /tasks command creates tasks.md
**Phase 4**: TDD implementation
**Phase 5**: Validation (benchmarks, integration tests, quickstart)

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | No violations | All constitutional principles followed |

## Progress Tracking

**Phase Status**:
- [x] Phase 0: Research complete
- [x] Phase 1: Design complete
- [x] Phase 2: Task planning approach defined
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:
- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved
- [x] Complexity deviations documented (none)

---
*Based on Constitution v2.0.0*
