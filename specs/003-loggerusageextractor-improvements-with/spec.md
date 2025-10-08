# Feature 003: LoggerUsageExtractor Progress Reporting and AdhocWorkspace Support

## Overview

Enhance `LoggerUsageExtractor` to support progress reporting via `IProgress<T>` and improve the flow for compilation handling by automatically creating an `AdhocWorkspace` when a single `Compilation` is passed. This enables leveraging Solution APIs such as `SymbolFinder` for single-compilation scenarios.

## Motivation

### Current Limitations

1. **No Progress Visibility**: Long-running analysis operations provide no feedback to callers about progress, making it difficult to:
   - Display progress indicators in UIs (CLI progress bars, VS Code extension progress notifications)
   - Monitor analysis performance and bottlenecks
   - Provide responsive user experiences during large workspace analysis

2. **Limited Solution API Access**: When analyzing a single `Compilation`, the current API doesn't provide access to Solution-level APIs like `SymbolFinder`, which are valuable for:
   - Finding all references to logging symbols across the compilation
   - Analyzing inheritance hierarchies for custom logger implementations
   - Performing cross-file semantic analysis

### Proposed Solution

1. **IProgress<T> Integration**: Add optional `IProgress<T>` parameters to extraction methods, reporting:
   - Project-level progress (when analyzing workspaces)
   - File-level progress (when analyzing individual syntax trees)
   - Analyzer-level progress (when multiple analyzers run)

2. **Automatic AdhocWorkspace Creation**: When `ExtractLoggerUsagesWithSolutionAsync` receives a `Compilation` without a `Solution`, automatically create an `AdhocWorkspace` to enable Solution API access.

## Requirements

### Functional Requirements

**FR1: Progress Reporting Interface**
- Add `IProgress<LoggerUsageProgress>` parameter to both extraction methods
- Parameter must be optional (nullable) to maintain backward compatibility
- Progress reports must include: percentage complete, current operation description, current file path (when applicable)

**FR2: Progress Granularity**
- Report progress at project level when analyzing workspaces
- Report progress at file level when analyzing compilations
- Report progress before and after each analyzer runs (for performance monitoring)

**FR3: AdhocWorkspace Auto-Creation**
- When `ExtractLoggerUsagesWithSolutionAsync` receives `null` solution, create `AdhocWorkspace`
- Add compilation to workspace as a new project
- Pass the created solution to analysis context
- Dispose workspace after analysis completes

**FR4: Solution API Enablement**
- `LoggingAnalysisContext` must receive valid `Solution` instance for single-compilation scenarios
- Analyzers can use `SymbolFinder` and other Solution APIs regardless of entry point

### Non-Functional Requirements

**NFR1: Performance**
- Progress reporting overhead must be negligible (< 5% performance impact)
- AdhocWorkspace creation must complete in < 500ms for typical compilations
- Memory overhead for AdhocWorkspace must be < 10% of compilation size

**NFR2: Backward Compatibility**
- Existing method signatures must remain functional
- Callers not passing `IProgress<T>` must experience no behavior changes
- Recompilation required but no code changes needed for existing consumers

**NFR3: Error Handling**
- Progress reporting failures must not abort analysis
- AdhocWorkspace creation failures must fall back to compilation-only analysis with warning log
- Invalid progress values (< 0% or > 100%) must be clamped/corrected

**NFR4: Thread Safety**
- Progress reporting must be thread-safe (multiple analyzers may report concurrently)
- AdhocWorkspace creation and disposal must be properly synchronized

## Design

### Progress Model

```csharp
/// <summary>
/// Represents progress information for logger usage extraction operations.
/// </summary>
public sealed class LoggerUsageProgress
{
    /// <summary>
    /// Gets the percentage of completion (0-100).
    /// </summary>
    public required int PercentComplete { get; init; }
    
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

### API Changes

**LoggerUsageExtractor.cs**

```csharp
public class LoggerUsageExtractor
{
    // Updated method signature - IProgress parameter added
    public async Task<LoggerUsageExtractionResult> ExtractLoggerUsagesAsync(
        Workspace workspace,
        IProgress<LoggerUsageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Implementation details in tasks
    }
    
    // Updated method signature - IProgress parameter added, AdhocWorkspace auto-creation
    public async Task<LoggerUsageExtractionResult> ExtractLoggerUsagesWithSolutionAsync(
        Compilation compilation,
        Solution? solution = null,
        IProgress<LoggerUsageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // If solution is null, create AdhocWorkspace
        // Implementation details in tasks
    }
}
```

### Progress Reporting Strategy

**For `ExtractLoggerUsagesAsync` (Workspace Analysis):**

1. **Project-level progress** (major steps):
   - "Analyzing project X of Y: {ProjectName}"
   - Percentage: `(currentProjectIndex / totalProjects) * 100`

2. **File-level progress** (within each project):
   - "Analyzing file {FileName} in {ProjectName}"
   - Percentage: `(baseProjectPercent + (currentFileIndex / totalFiles) * (100 / totalProjects))`

3. **Analyzer-level progress** (within each file):
   - "Running analyzer {AnalyzerName} on {FileName}"
   - Percentage: Fine-grained within file progress

**For `ExtractLoggerUsagesWithSolutionAsync` (Single Compilation):**

1. **Workspace creation** (if needed):
   - "Creating AdhocWorkspace for compilation"
   - Percentage: 5%

2. **File-level progress**:
   - "Analyzing file X of Y: {FileName}"
   - Percentage: `5 + ((currentFileIndex / totalFiles) * 95)`

3. **Analyzer-level progress**:
   - "Running analyzer {AnalyzerName} on {FileName}"
   - Percentage: Fine-grained within file progress

### AdhocWorkspace Creation Flow

```csharp
private async Task<(Solution, IDisposable?)> EnsureSolutionAsync(Compilation compilation, Solution? solution)
{
    if (solution != null)
    {
        return (solution, null);
    }
    
    _logger.LogInformation("Creating AdhocWorkspace for compilation '{AssemblyName}'", compilation.AssemblyName);
    
    var workspace = new AdhocWorkspace();
    var projectInfo = ProjectInfo.Create(
        ProjectId.CreateNewId(),
        VersionStamp.Default,
        compilation.AssemblyName ?? "Project",
        compilation.AssemblyName ?? "Project",
        LanguageNames.CSharp,
        compilationOptions: compilation.Options,
        metadataReferences: compilation.References);
    
    var project = workspace.AddProject(projectInfo);
    
    // Add syntax trees to project
    foreach (var syntaxTree in compilation.SyntaxTrees)
    {
        var documentInfo = DocumentInfo.Create(
            DocumentId.CreateNewId(project.Id),
            Path.GetFileName(syntaxTree.FilePath),
            filePath: syntaxTree.FilePath);
        
        var document = workspace.AddDocument(documentInfo);
        await document.WithSyntaxRoot(syntaxTree.GetRoot());
    }
    
    return (workspace.CurrentSolution, workspace);
}
```

## Implementation Strategy

### Phase 1: Progress Infrastructure
1. Create `LoggerUsageProgress` model
2. Add `IProgress<T>` parameters to extraction methods
3. Implement progress reporting helper methods
4. Add unit tests for progress reporting

### Phase 2: AdhocWorkspace Support
1. Implement `EnsureSolutionAsync` helper method
2. Update `ExtractLoggerUsagesWithSolutionAsync` to use helper
3. Add proper disposal pattern for created workspaces
4. Add unit tests for AdhocWorkspace creation

### Phase 3: Integration & Testing
1. Update `LoggerUsage.Cli` to display progress bars
2. Update `LoggerUsage.VSCode.Bridge` to report progress to extension
3. Add integration tests with real workspaces
4. Performance testing and optimization

### Phase 4: Documentation
1. Update XML documentation for modified methods
2. Add code examples showing progress reporting usage
3. Update README with new capabilities
4. Add migration guide for consumers

## Testing Strategy

### Unit Tests

**Progress Reporting Tests:**
- `ExtractLoggerUsagesAsync_WithProgress_ReportsProjectProgress`
- `ExtractLoggerUsagesAsync_WithProgress_ReportsFileProgress`
- `ExtractLoggerUsagesAsync_WithProgress_ReportsAnalyzerProgress`
- `ExtractLoggerUsagesAsync_WithNullProgress_CompletesSuccessfully`
- `ExtractLoggerUsagesWithSolutionAsync_WithProgress_ReportsProgress`

**AdhocWorkspace Tests:**
- `ExtractLoggerUsagesWithSolutionAsync_WithNullSolution_CreatesAdhocWorkspace`
- `ExtractLoggerUsagesWithSolutionAsync_WithNullSolution_DisposesWorkspace`
- `ExtractLoggerUsagesWithSolutionAsync_WithProvidedSolution_UsesProvidedSolution`
- `ExtractLoggerUsagesWithSolutionAsync_AdhocWorkspace_EnablesSolutionApis`

**Error Handling Tests:**
- `ExtractLoggerUsagesAsync_ProgressReportThrows_ContinuesAnalysis`
- `ExtractLoggerUsagesWithSolutionAsync_WorkspaceCreationFails_FallsBackGracefully`

### Integration Tests

**CLI Integration:**
- `Cli_WithProgressBar_DisplaysProgressDuringAnalysis`
- `Cli_LargeWorkspace_ProgressReachesOneHundredPercent`

**VS Code Bridge Integration:**
- `Bridge_ReportsProgress_ToExtension`
- `Bridge_WithAdhocWorkspace_UsesSymbolFinder`

### Performance Tests

**Benchmarks:**
- Progress reporting overhead measurement
- AdhocWorkspace creation latency
- Memory overhead comparison

## Success Criteria

### Functional Success
- ✅ Progress is reported at project, file, and analyzer levels
- ✅ Progress values are accurate and monotonically increasing
- ✅ AdhocWorkspace is created automatically when solution is null
- ✅ Solution APIs work correctly with created AdhocWorkspace
- ✅ Workspaces are properly disposed after analysis

### Non-Functional Success
- ✅ Progress reporting adds < 5% overhead
- ✅ AdhocWorkspace creation completes in < 500ms
- ✅ Memory overhead < 10% for AdhocWorkspace scenarios
- ✅ All existing tests pass without modification
- ✅ No breaking API changes

### Quality Gates
- ✅ 100% test coverage for new progress reporting code
- ✅ 100% test coverage for AdhocWorkspace creation code
- ✅ All integration tests pass with progress reporting enabled
- ✅ Performance benchmarks meet NFR thresholds
- ✅ XML documentation complete for all public APIs

## Dependencies

### Internal Dependencies
- `LoggerUsage.Models` - Add `LoggerUsageProgress` model
- `LoggerUsage.Analyzers` - Update to support progress context
- `LoggerUsage.Cli` - Update to display progress
- `LoggerUsage.VSCode.Bridge` - Update to report progress to extension

### External Dependencies
- `Microsoft.CodeAnalysis.Workspaces` - For `AdhocWorkspace`
- `Microsoft.CodeAnalysis.CSharp.Workspaces` - For C# workspace support
- No new NuGet packages required (already referenced)

## Migration Guide

### For Existing Consumers

**No code changes required** - existing calls will continue to work:

```csharp
// Existing code - still works
var result = await extractor.ExtractLoggerUsagesAsync(workspace);
```

**Optional progress reporting** - add `IProgress<T>` parameter:

```csharp
// New code with progress
var progress = new Progress<LoggerUsageProgress>(p =>
{
    Console.WriteLine($"{p.PercentComplete}% - {p.OperationDescription}");
});

var result = await extractor.ExtractLoggerUsagesAsync(workspace, progress);
```

**Optional cancellation support** - add `CancellationToken`:

```csharp
// New code with cancellation
var cts = new CancellationTokenSource();
var result = await extractor.ExtractLoggerUsagesAsync(workspace, progress, cts.Token);
```

### For Single Compilation Analysis

**Before (limited Solution API access):**
```csharp
var result = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);
// Solution APIs not available in analyzers
```

**After (automatic Solution API access):**
```csharp
var result = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);
// AdhocWorkspace created automatically, Solution APIs available
```

## Risks & Mitigations

### Risk 1: Performance Degradation
**Impact:** Progress reporting may slow down analysis significantly  
**Likelihood:** Medium  
**Mitigation:** 
- Measure overhead in benchmarks
- Make progress reporting configurable
- Use efficient progress calculation (avoid expensive operations)
- Cache progress calculations where possible

### Risk 2: Memory Overhead
**Impact:** AdhocWorkspace creation may increase memory usage substantially  
**Likelihood:** Low  
**Mitigation:**
- Dispose workspaces promptly after analysis
- Monitor memory in integration tests
- Add configuration to disable AdhocWorkspace creation if needed

### Risk 3: Thread Safety Issues
**Impact:** Concurrent progress reports may cause race conditions  
**Likelihood:** Low  
**Mitigation:**
- Use thread-safe progress reporting patterns
- Add concurrency tests
- Document thread-safety guarantees

### Risk 4: Breaking Changes
**Impact:** API changes may break existing consumers  
**Likelihood:** Very Low  
**Mitigation:**
- Use optional parameters for backward compatibility
- Maintain existing method signatures
- Add comprehensive tests for backward compatibility

## Alternatives Considered

### Alternative 1: Callback-Based Progress
**Description:** Use callback methods instead of `IProgress<T>`  
**Pros:** More flexible, allows for different progress patterns  
**Cons:** Less idiomatic for .NET, harder to use with async/await  
**Decision:** Rejected - `IProgress<T>` is the standard .NET pattern

### Alternative 2: Event-Based Progress
**Description:** Use events for progress notifications  
**Pros:** Familiar pattern, supports multiple subscribers  
**Cons:** Requires careful event handler management, memory leak risks  
**Decision:** Rejected - `IProgress<T>` is safer and more modern

### Alternative 3: Always Create AdhocWorkspace
**Description:** Always create `AdhocWorkspace` regardless of solution parameter  
**Pros:** Simpler API, consistent behavior  
**Cons:** Performance overhead when solution is already available  
**Decision:** Rejected - Conditional creation provides better performance

### Alternative 4: Separate Method for AdhocWorkspace
**Description:** Create new `ExtractLoggerUsagesWithAdhocWorkspace` method  
**Pros:** Explicit API, no auto-magic behavior  
**Cons:** API proliferation, extra method to maintain  
**Decision:** Rejected - Auto-creation keeps API cleaner

## Open Questions

None - all clarifications have been resolved:

1. **API Compatibility:** Modify existing methods with optional `IProgress<T>` parameter ✅
2. **Progress Granularity:** Project + file + analyzer level ✅
3. **Progress Structure:** Percentage + description + optional file path + optional analyzer name ✅
4. **Performance Overhead:** < 5% for progress reporting ✅
5. **AdhocWorkspace Latency:** < 500ms for typical compilations ✅
6. **Memory Increase:** < 10% for AdhocWorkspace scenarios ✅
7. **Error Handling:** Progress failures don't abort; workspace failures fall back with warning ✅
8. **Cancellation Support:** Yes, via `CancellationToken` parameter ✅
9. **Thread Safety:** Yes, progress reporting is thread-safe ✅
10. **Backwards Compatibility:** Recompilation needed but no code changes required ✅

## References

- [IProgress<T> Interface](https://docs.microsoft.com/en-us/dotnet/api/system.iprogress-1)
- [AdhocWorkspace Class](https://docs.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.adhocworkspace)
- [SymbolFinder Class](https://docs.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.findSymbols.symbolfinder)
- [Roslyn Workspace API](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/work-with-workspace)
- [Progress Reporting in .NET](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/how-to-report-progress-from-a-task)
