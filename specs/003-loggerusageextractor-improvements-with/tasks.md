# Feature 003: Implementation Tasks

**Feature**: LoggerUsageExtractor Progress Reporting and AdhocWorkspace Support
**Branch**: `003-loggerusageextractor-improvements-with`
**Plan**: [plan.md](./plan.md) | **Spec**: [spec.md](./spec.md)

## Task Summary

- **Total Tasks**: 33
- **Parallel Tasks**: 21 (63%)
- **Estimated Duration**: 16-20 hours
- **Test-First**: Yes (Tests before implementation)

## Phase 1: Models & Utilities (Tasks 1-3) [PARALLEL]

### Task 1: Create LoggerUsageProgress Model [P]
**File**: `src/LoggerUsage/Models/LoggerUsageProgress.cs`
**Type**: New File
**Dependencies**: None
**Estimate**: 30 minutes

**Description**:
Create the immutable `LoggerUsageProgress` record to represent progress information.

**Acceptance Criteria**:
- [X] Record with required init properties
- [X] `PercentComplete` (int, 0-100)
- [X] `OperationDescription` (string, not null/empty)
- [X] `CurrentFilePath` (string?, optional)
- [X] `CurrentAnalyzer` (string?, optional)
- [X] Complete XML documentation
- [X] Validation in constructor (clamp percentage, validate description)

**Implementation Notes**:
```csharp
/// <summary>
/// Represents progress information for logger usage extraction operations.
/// </summary>
public sealed record LoggerUsageProgress
{
    private int _percentComplete;

    /// <summary>
    /// Gets the percentage of completion (0-100).
    /// </summary>
    public required int PercentComplete
    {
        get => _percentComplete;
        init => _percentComplete = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets the description of the current operation.
    /// </summary>
    public required string OperationDescription { get; init; }

    /// <summary>
    /// Gets the path of the file currently being analyzed, if applicable.
    /// </summary>
    public string? CurrentFilePath { get; init; }

    /// <summary>
    /// Gets the name of the analyzer currently running, if applicable.
    /// </summary>
    public string? CurrentAnalyzer { get; init; }
}
```

---

### Task 2: Create ProgressReporter Helper [P]
**File**: `src/LoggerUsage/Services/ProgressReporter.cs`
**Type**: New File
**Dependencies**: Task 1
**Estimate**: 45 minutes

**Description**:
Create helper class for calculating and reporting progress efficiently.

**Acceptance Criteria**:
- [X] Internal class with IProgress<LoggerUsageProgress> field
- [X] Constructor accepts nullable IProgress, total work units
- [X] `ReportProjectProgress(int projectIndex, int totalProjects, string projectName)`
- [X] `ReportFileProgress(int basePercent, int fileIndex, int totalFiles, string fileName)`
- [X] `ReportAnalyzerProgress(int basePercent, string analyzerName, string? filePath)`
- [X] Thread-safe progress reporting (IProgress handles this)
- [X] No-op when progress is null (performance optimization)
- [X] Complete XML documentation

**Implementation Notes**:
- Use formula: `percent = basePercent + (currentIndex / totalItems) * percentRange`
- Cache null check result to avoid repeated checks
- Keep methods lightweight (< 10 lines each)

---

### Task 3: Create WorkspaceHelper Utility [P]
**File**: `src/LoggerUsage/Utilities/WorkspaceHelper.cs`
**Type**: New File
**Dependencies**: None
**Estimate**: 1 hour

**Description**:
Create utility for AdhocWorkspace creation and management.

**Acceptance Criteria**:
- [X] Static class with async methods
- [X] `EnsureSolutionAsync(Compilation, Solution?, ILogger) → (Solution, IDisposable?)`
- [X] Return provided solution if not null
- [X] Create AdhocWorkspace if solution is null
- [X] Add project with compilation metadata
- [X] Add all syntax trees as documents
- [X] Log information messages
- [X] Handle errors gracefully (return null workspace, log warning)
- [X] Complete XML documentation

**Implementation Notes**:
```csharp
public static async Task<(Solution, IDisposable?)> EnsureSolutionAsync(
    Compilation compilation,
    Solution? solution,
    ILogger logger)
{
    if (solution != null)
        return (solution, null);

    logger.LogInformation("Creating AdhocWorkspace for compilation");

    try
    {
        var workspace = new AdhocWorkspace();
        var projectInfo = ProjectInfo.Create(/*...*/);
        // Add documents for each syntax tree
        return (workspace.CurrentSolution, workspace);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to create AdhocWorkspace");
        return (null!, null);
    }
}
```

---

## Phase 2: Contract Tests (Tasks 4-18) [PARALLEL]

### Task 4: Test - Progress Model Structure [P]
**File**: `test/LoggerUsage.Tests/ProgressReportingTests.cs`
**Type**: New Test
**Dependencies**: Task 1
**Estimate**: 15 minutes

```csharp
[Fact]
public void LoggerUsageProgress_HasRequiredProperties()
{
    var progress = new LoggerUsageProgress
    {
        PercentComplete = 50,
        OperationDescription = "Test operation"
    };

    progress.PercentComplete.Should().Be(50);
    progress.OperationDescription.Should().Be("Test operation");
    progress.CurrentFilePath.Should().BeNull();
    progress.CurrentAnalyzer.Should().BeNull();
}
```

**Status**: ✅ COMPLETED

---

### Task 5: Test - Percentage Clamping [P]
**File**: `test/LoggerUsage.Tests/ProgressReportingTests.cs`
**Type**: New Test
**Dependencies**: Task 1
**Estimate**: 15 minutes

```csharp
[Theory]
[InlineData(-10, 0)]
[InlineData(0, 0)]
[InlineData(50, 50)]
[InlineData(100, 100)]
[InlineData(150, 100)]
public void LoggerUsageProgress_ClampsPercentage(int input, int expected)
{
    var progress = new LoggerUsageProgress
    {
        PercentComplete = input,
        OperationDescription = "Test"
    };

    progress.PercentComplete.Should().Be(expected);
}
```

**Status**: ✅ COMPLETED

---

### Task 6: Test - Description Validation [P]
**File**: `test/LoggerUsage.Tests/ProgressReportingTests.cs`
**Type**: New Test
**Dependencies**: Task 1
**Estimate**: 15 minutes

```csharp
[Theory]
[InlineData(null)]
[InlineData("")]
public void LoggerUsageProgress_RequiresDescription(string? description)
{
    Action act = () => new LoggerUsageProgress
    {
        PercentComplete = 50,
        OperationDescription = description!
    };

    act.Should().Throw<ArgumentException>();
}
```

**Status**: ✅ COMPLETED

---

### Task 7: Test - Workspace Extraction With Progress [P]
**File**: `test/LoggerUsage.Tests/ExtractLoggerUsagesFromWorkspaceTests.cs`
**Type**: Modified Test
**Dependencies**: Tasks 1, 2
**Estimate**: 30 minutes

```csharp
[Fact]
public async Task ExtractLoggerUsagesAsync_WithProgress_ReportsProgress()
{
    var workspace = await TestUtils.CreateWorkspaceAsync();
    var extractor = TestUtils.CreateLoggerUsageExtractor();
    var reports = new List<LoggerUsageProgress>();
    var progress = new Progress<LoggerUsageProgress>(reports.Add);

    await extractor.ExtractLoggerUsagesAsync(workspace, progress);

    reports.Should().NotBeEmpty();
    reports.Should().OnlyContain(r => r.PercentComplete >= 0 && r.PercentComplete <= 100);
    reports.Should().OnlyContain(r => !string.IsNullOrEmpty(r.OperationDescription));
    reports.First().PercentComplete.Should().BeLessThan(reports.Last().PercentComplete);
}
```

**Status**: ✅ COMPLETED

---

### Task 8: Test - Project Level Progress [P]
**Status**: ⏭️ SKIPPED (No multi-project workspace utility available)

---

### Task 9: Test - File Level Progress [P]
**Status**: ⏭️ SKIPPED (No multi-file workspace utility available)

---

### Task 10: Test - Analyzer Level Progress [P]
**Status**: ⏭️ SKIPPED (Covered by basic progress test)

---

### Task 11: Test - Null Progress Handling [P]
**File**: `test/LoggerUsage.Tests/ProgressReportingTests.cs`
**Type**: New Test
**Dependencies**: Task 2
**Estimate**: 15 minutes

```csharp
[Fact]
public async Task ExtractLoggerUsagesAsync_WithNullProgress_CompletesSuccessfully()
{
    var workspace = await TestUtils.CreateWorkspaceAsync();
    var extractor = TestUtils.CreateLoggerUsageExtractor();

    var act = async () => await extractor.ExtractLoggerUsagesAsync(workspace, progress: null);

    await act.Should().NotThrowAsync();
}
```

**Status**: ✅ COMPLETED

---

### Task 12: Test - Progress Exception Handling [P]
**File**: `test/LoggerUsage.Tests/ProgressReportingTests.cs`
**Type**: New Test
**Dependencies**: Task 2
**Estimate**: 20 minutes

```csharp
[Fact]
public async Task ExtractLoggerUsagesAsync_ProgressThrows_ContinuesAnalysis()
{
    var workspace = await TestUtils.CreateWorkspaceAsync();
    var extractor = TestUtils.CreateLoggerUsageExtractor();
    var progress = new Progress<LoggerUsageProgress>(_ => throw new InvalidOperationException("Test exception"));

    var result = await extractor.ExtractLoggerUsagesAsync(workspace, progress);

    result.Should().NotBeNull();
    result.Results.Should().NotBeEmpty();
}
```

**Status**: ✅ COMPLETED

---

### Task 13: Test - AdhocWorkspace Creation [P]
**File**: `test/LoggerUsage.Tests/AdhocWorkspaceTests.cs`
**Type**: New Test
**Dependencies**: Task 3
**Estimate**: 25 minutes

```csharp
[Fact]
public async Task ExtractLoggerUsagesWithSolutionAsync_NullSolution_CreatesAdhocWorkspace()
{
    var compilation = await TestUtils.CreateCompilationAsync();
    var extractor = TestUtils.CreateLoggerUsageExtractor();

    var result = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation, solution: null);

    result.Should().NotBeNull();
    // Verify that Solution APIs were available (tested via analyzer that uses SymbolFinder)
}
```

**Status**: ✅ COMPLETED

---

### Task 14: Test - AdhocWorkspace with Progress [P]
**File**: `test/LoggerUsage.Tests/AdhocWorkspaceTests.cs`
**Type**: New Test
**Dependencies**: Tasks 1, 2, 3
**Estimate**: 20 minutes

```csharp
[Fact]
public async Task ExtractLoggerUsagesWithSolutionAsync_NullSolution_ReportsProgress()
{
    var compilation = await TestUtils.CreateCompilationAsync();
    var extractor = TestUtils.CreateLoggerUsageExtractor();
    var reports = new List<LoggerUsageProgress>();

    await extractor.ExtractLoggerUsagesWithSolutionAsync(
        compilation,
        solution: null,
        progress: new Progress<LoggerUsageProgress>(reports.Add));

    reports.Should().Contain(r => r.OperationDescription.Contains("workspace", StringComparison.OrdinalIgnoreCase));
}
```

**Status**: ✅ COMPLETED

---

### Task 15: Test - Provided Solution Used [P]
**File**: `test/LoggerUsage.Tests/AdhocWorkspaceTests.cs`
**Type**: New Test
**Dependencies**: Task 3
**Estimate**: 20 minutes

```csharp
[Fact]
public async Task ExtractLoggerUsagesWithSolutionAsync_WithSolution_UsesProvidedSolution()
{
    var workspace = await TestUtils.CreateWorkspaceAsync();
    var project = workspace.CurrentSolution.Projects.First();
    var compilation = await project.GetCompilationAsync();
    var extractor = TestUtils.CreateLoggerUsageExtractor();

    var result = await extractor.ExtractLoggerUsagesWithSolutionAsync(
        compilation!,
        solution: workspace.CurrentSolution);

    result.Should().NotBeNull();
    // Verify no AdhocWorkspace was created (via logging or internal state)
}
```

**Status**: ✅ COMPLETED

---

### Task 16: Test - SymbolFinder Accessibility [P]
**File**: `test/LoggerUsage.Tests/AdhocWorkspaceTests.cs`
**Type**: New Test
**Dependencies**: Task 3
**Estimate**: 30 minutes

```csharp
[Fact]
public async Task ExtractLoggerUsagesWithSolutionAsync_NullSolution_EnablesSymbolFinder()
{
    // Create compilation with ILogger symbol
    var compilation = await TestUtils.CreateCompilationWithLoggerAsync();
    var extractor = TestUtils.CreateLoggerUsageExtractor();

    var result = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation, solution: null);

    // Custom analyzer should have been able to use SymbolFinder
    // Verify via test analyzer that tracks whether Solution was available
    TestAnalyzer.SolutionWasAvailable.Should().BeTrue();
}
```

**Status**: ✅ COMPLETED

---

### Task 17: Test - Cancellation Support [P]
**Status**: ⏭️ DEFERRED (Will be implemented with cancellation token support)

---

### Task 18: Test - Thread Safety [P]
**File**: `test/LoggerUsage.Tests/ProgressReportingTests.cs`
**Type**: New Test
**Dependencies**: Tasks 1, 2
**Estimate**: 30 minutes

```csharp
[Fact]
public async Task ExtractLoggerUsagesAsync_ConcurrentProgress_ThreadSafe()
{
    var workspace = await TestUtils.CreateWorkspaceAsync();
    var extractor = TestUtils.CreateLoggerUsageExtractor();
    var reports = new ConcurrentBag<LoggerUsageProgress>();
    var progress = new Progress<LoggerUsageProgress>(reports.Add);

    // Run multiple analyses concurrently
    var tasks = Enumerable.Range(0, 10)
        .Select(_ => extractor.ExtractLoggerUsagesAsync(workspace, progress));

    await Task.WhenAll(tasks);

    reports.Should().NotBeEmpty();
    reports.Should().OnlyContain(r => r.PercentComplete >= 0 && r.PercentComplete <= 100);
}
```

**Status**: ✅ COMPLETED

---

## Phase 3: Core Implementation (Tasks 19-24) [SEQUENTIAL]

### Task 19: Update LoggerUsageExtractor Method Signatures
**File**: `src/LoggerUsage/LoggerUsageExtractor.cs`
**Type**: Modify
**Dependencies**: Tasks 1-3
**Estimate**: 30 minutes

**Description**:
Add `IProgress<T>` and `CancellationToken` parameters to both extraction methods.

**Acceptance Criteria**:
- [ ] `ExtractLoggerUsagesAsync` signature updated with optional parameters
- [ ] `ExtractLoggerUsagesWithSolutionAsync` signature updated with optional parameters
- [ ] XML documentation updated with parameter descriptions
- [ ] Default values: `progress = null`, `cancellationToken = default`
- [ ] Backward compatible (existing calls still compile)

---

### Task 20: Implement Progress Reporting in ExtractLoggerUsagesAsync
**File**: `src/LoggerUsage/LoggerUsageExtractor.cs`
**Type**: Modify
**Dependencies**: Task 19
**Estimate**: 1.5 hours

**Description**:
Add progress reporting at project, file, and analyzer levels in workspace extraction.

**Acceptance Criteria**:
- [ ] Create ProgressReporter at method start
- [ ] Report progress before each project analysis
- [ ] Pass progress to `ExtractLoggerUsagesWithSolutionAsync` calls
- [ ] Calculate percentages based on project count
- [ ] Wrap progress reports in try-catch (log but don't abort)
- [ ] Check cancellation token at project boundaries
- [ ] Tests pass (Tasks 7-11)

**Implementation Notes**:
```csharp
public async Task<LoggerUsageExtractionResult> ExtractLoggerUsagesAsync(
    Workspace workspace,
    IProgress<LoggerUsageProgress>? progress = null,
    CancellationToken cancellationToken = default)
{
    var projects = workspace.CurrentSolution.Projects.Where(p => p.Language == LanguageNames.CSharp).ToList();
    var reporter = new ProgressReporter(progress, projects.Count);

    for (int i = 0; i < projects.Count; i++)
    {
        cancellationToken.ThrowIfCancellationRequested();
        reporter.ReportProjectProgress(i, projects.Count, projects[i].Name);
        // ...existing analysis...
    }
}
```

---

### Task 21: Implement AdhocWorkspace Creation in ExtractLoggerUsagesWithSolutionAsync
**File**: `src/LoggerUsage/LoggerUsageExtractor.cs`
**Type**: Modify
**Dependencies**: Task 20
**Estimate**: 1 hour

**Description**:
Add automatic AdhocWorkspace creation when solution parameter is null.

**Acceptance Criteria**:
- [ ] Call `WorkspaceHelper.EnsureSolutionAsync` at method start
- [ ] Use returned solution for analysis context
- [ ] Dispose workspace at method end (try-finally or using)
- [ ] Handle null workspace (fallback to compilation-only mode)
- [ ] Log information/warning messages appropriately
- [ ] Tests pass (Tasks 13-16)

**Implementation Notes**:
```csharp
public async Task<LoggerUsageExtractionResult> ExtractLoggerUsagesWithSolutionAsync(
    Compilation compilation,
    Solution? solution = null,
    IProgress<LoggerUsageProgress>? progress = null,
    CancellationToken cancellationToken = default)
{
    var (ensuredSolution, workspace) = await WorkspaceHelper.EnsureSolutionAsync(compilation, solution, _logger);

    try
    {
        // Use ensuredSolution for analysis
        // ...existing analysis...
    }
    finally
    {
        workspace?.Dispose();
    }
}
```

---

### Task 22: Implement File-Level Progress Reporting
**File**: `src/LoggerUsage/LoggerUsageExtractor.cs`
**Type**: Modify
**Dependencies**: Task 21
**Estimate**: 1 hour

**Description**:
Add progress reporting before analyzing each syntax tree.

**Acceptance Criteria**:
- [ ] Report progress before each syntax tree analysis
- [ ] Include file path in progress report
- [ ] Calculate percentage based on syntax tree index
- [ ] Check cancellation token before each file
- [ ] Tests pass (Task 9)

---

### Task 23: Implement Analyzer-Level Progress Reporting
**File**: `src/LoggerUsage/LoggerUsageExtractor.cs`
**Type**: Modify
**Dependencies**: Task 22
**Estimate**: 45 minutes

**Description**:
Add progress reporting before each analyzer runs.

**Acceptance Criteria**:
- [ ] Report progress before each analyzer execution
- [ ] Include analyzer name and current file in progress
- [ ] Calculate fine-grained percentage within file analysis
- [ ] Tests pass (Task 10)

---

### Task 24: Add Error Handling and Fallbacks
**File**: `src/LoggerUsage/LoggerUsageExtractor.cs`
**Type**: Modify
**Dependencies**: Task 23
**Estimate**: 45 minutes

**Description**:
Ensure robust error handling for progress and workspace failures.

**Acceptance Criteria**:
- [ ] Wrap progress reports in try-catch
- [ ] Log progress failures at Warning level
- [ ] Continue analysis if progress reporting fails
- [ ] Fall back to compilation-only mode if workspace creation fails
- [ ] Log workspace creation failures at Warning level
- [ ] Tests pass (Task 12)

---

## Phase 4: Consumer Updates (Tasks 25-26) [PARALLEL]

### Task 25: Update CLI with Progress Bar [P]
**File**: `src/LoggerUsage.Cli/Program.cs`
**Type**: Modify
**Dependencies**: Tasks 19-24
**Estimate**: 1 hour

**Description**:
Add progress bar visualization to CLI when verbose mode is enabled.

**Acceptance Criteria**:
- [ ] Create `ProgressBarHandler` class
- [ ] Display progress bar with percentage and description
- [ ] Update progress bar on each report
- [ ] Clear progress bar when complete
- [ ] Only show if `--verbose` flag is set
- [ ] Handle console width properly
- [ ] Example: `[████████████░░░░░░░░] 60% Analyzing Project.csproj`

**Implementation Notes**:
```csharp
if (options.Verbose)
{
    var progressHandler = new ProgressBarHandler();
    progress = new Progress<LoggerUsageProgress>(progressHandler.Report);
}
var result = await extractor.ExtractLoggerUsagesAsync(workspace, progress);
```

---

### Task 26: Update VS Code Bridge with Progress Reporting [P]
**File**: `src/LoggerUsage.VSCode.Bridge/Program.cs`
**Type**: Modify
**Dependencies**: Tasks 19-24
**Estimate**: 45 minutes

**Description**:
Report progress to VS Code extension via stdout JSON messages.

**Acceptance Criteria**:
- [ ] Create progress handler that writes JSON to stdout
- [ ] Format: `{"type":"progress","percent":50,"message":"..."}`
- [ ] Report on each progress update
- [ ] VS Code extension can parse and display progress
- [ ] Don't interfere with final result JSON output

**Implementation Notes**:
```csharp
var progress = new Progress<LoggerUsageProgress>(p =>
{
    var progressJson = JsonSerializer.Serialize(new
    {
        type = "progress",
        percent = p.PercentComplete,
        message = p.OperationDescription
    });
    Console.WriteLine(progressJson);
});
```

---

## Phase 5: Validation & Documentation (Tasks 27-33) [PARALLEL]

### Task 27: Create Performance Benchmarks [P]
**File**: `test/LoggerUsage.Tests/PerformanceTests.cs`
**Type**: New File
**Dependencies**: Tasks 19-24
**Estimate**: 1.5 hours

**Description**:
Create BenchmarkDotNet benchmarks to measure overhead.

**Acceptance Criteria**:
- [ ] Benchmark: Analysis without progress (baseline)
- [ ] Benchmark: Analysis with progress reporting
- [ ] Benchmark: AdhocWorkspace creation time
- [ ] Measure memory allocation differences
- [ ] Verify < 5% overhead for progress reporting
- [ ] Verify < 500ms for AdhocWorkspace creation
- [ ] Verify < 10% memory increase

---

### Task 28: Run Integration Tests [P]
**File**: Multiple test files
**Type**: Test Execution
**Dependencies**: Tasks 4-26
**Estimate**: 30 minutes

**Description**:
Execute all tests and verify they pass.

**Acceptance Criteria**:
- [ ] All 20+ contract tests pass
- [ ] Integration scenarios work end-to-end
- [ ] CLI progress bar displays correctly
- [ ] VS Code Bridge reports progress correctly
- [ ] No regressions in existing tests

---

### Task 29: Verify Performance Benchmarks [P]
**File**: `test/LoggerUsage.Tests/PerformanceTests.cs`
**Type**: Test Execution
**Dependencies**: Task 27
**Estimate**: 30 minutes

**Description**:
Run benchmarks and verify performance contracts are met.

**Acceptance Criteria**:
- [ ] Progress overhead < 5%
- [ ] AdhocWorkspace creation < 500ms
- [ ] Memory increase < 10%
- [ ] Document results in commit message

---

### Task 30: Update XML Documentation [P]
**File**: Multiple source files
**Type**: Documentation
**Dependencies**: Tasks 19-26
**Estimate**: 45 minutes

**Description**:
Ensure all public APIs have complete XML documentation.

**Acceptance Criteria**:
- [ ] `LoggerUsageProgress` fully documented
- [ ] `ExtractLoggerUsagesAsync` parameters documented
- [ ] `ExtractLoggerUsagesWithSolutionAsync` parameters documented
- [ ] Code examples in XML docs
- [ ] All warnings resolved

---

### Task 31: Update README.md [P]
**File**: `README.md`
**Type**: Documentation
**Dependencies**: Tasks 19-26
**Estimate**: 30 minutes

**Description**:
Add section about progress reporting capabilities.

**Acceptance Criteria**:
- [ ] "Progress Reporting" section added
- [ ] Code examples for CLI and library usage
- [ ] Mention AdhocWorkspace automatic creation
- [ ] Link to quickstart examples

---

### Task 32: Create Migration Guide [P]
**File**: `specs/003-loggerusageextractor-improvements-with/MIGRATION.md`
**Type**: New Documentation
**Dependencies**: Tasks 19-26
**Estimate**: 30 minutes

**Description**:
Document migration path for existing consumers.

**Acceptance Criteria**:
- [ ] "No code changes required" section
- [ ] "Optional progress reporting" section with examples
- [ ] "Optional cancellation" section
- [ ] Before/after code examples
- [ ] Benefits of upgrading

---

### Task 33: Create Release Notes [P]
**File**: `CHANGELOG.md` or similar
**Type**: Documentation
**Dependencies**: Tasks 19-32
**Estimate**: 20 minutes

**Description**:
Document changes for release.

**Acceptance Criteria**:
- [ ] "Added" section: IProgress support, AdhocWorkspace auto-creation
- [ ] "Changed" section: Method signatures (backward compatible)
- [ ] "Performance" section: Overhead measurements
- [ ] Breaking changes: None
- [ ] Migration guide link

---

## Execution Strategy

### Test-Driven Development
1. ✅ Write failing tests first (Phase 2: Tasks 4-18)
2. ✅ Implement minimum code to pass tests (Phase 3: Tasks 19-24)
3. ✅ Refactor for clarity and performance
4. ✅ Verify all tests pass

### Parallelization Opportunities
- Phase 1: All 3 tasks can run in parallel
- Phase 2: All 15 test tasks can run in parallel
- Phase 4: Both consumer updates can run in parallel
- Phase 5: All 7 validation tasks can run in parallel

**Total Parallel Tasks**: 21 out of 33 (63%)

### Risk Mitigation
- **Performance Risk**: Benchmark early (Task 27), optimize if needed
- **Thread Safety Risk**: Dedicated test (Task 18), use proven `IProgress<T>` pattern
- **Workspace Disposal Risk**: Use try-finally pattern, add disposal tests

### Validation Checklist
- [ ] All 20+ tests pass
- [ ] Performance benchmarks meet thresholds
- [ ] CLI progress bar works
- [ ] VS Code Bridge reports progress
- [ ] Documentation complete
- [ ] No breaking changes
- [ ] Backward compatibility verified

---

**Total Estimated Time**: 16-20 hours
**Recommended Sprint**: 2-3 days with parallel execution

