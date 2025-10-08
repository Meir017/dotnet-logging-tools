
# LoggerUsage Library Development Guidelines

This document provides comprehensive guidelines for developing and maintaining the LoggerUsage library, a .NET library for analyzing logging patterns in C# code using Roslyn.

## Project Overview

The LoggerUsage library is a sophisticated static code analysis tool that extracts and analyzes logging patterns from .NET projects. It targets .NET 10 and focuses on Microsoft.Extensions.Logging patterns, providing insights into logging consistency and usage.

### Key Components

1. **LoggerUsageExtractor** - Main orchestrator that coordinates analysis across workspace/compilations
2. **ILoggerUsageAnalyzer** - Plugin-style analyzers for different logging patterns
3. **LoggingTypes** - Central registry of Roslyn symbols for logging-related types
4. **Models** - Data structures representing extracted logging information
5. **DependencyInjection** - Service registration and configuration

## Git Workflow

### Branch Strategy

This project follows a **feature branch workflow** with strict protections on the main branch:

- **NEVER commit directly to `main`** - The main branch is protected and should only receive changes via pull requests
- **Each feature requires a new branch** - Create a feature branch using the pattern `NNN-feature-name` where NNN is a three-digit number
- **Use the `.specify` workflow** - Create new features using the `.specify/scripts/powershell/create-new-feature.ps1` script which automatically:
  - Determines the next feature number
  - Creates a properly named branch (e.g., `001-my-feature`)
  - Sets up the feature directory structure in `specs/`
  - Configures the `SPECIFY_FEATURE` environment variable

### Working with Feature Branches

1. **Creating a feature**: Run the create-new-feature script with a description:
   ```powershell
   .specify/scripts/powershell/create-new-feature.ps1 "feature description"
   ```

2. **Committing changes**: Use the `report_progress` tool to commit and push changes:
   - This tool automatically runs `git add .`, `git commit`, and `git push`
   - Provide a clear commit message and updated PR description with checklist
   - Never use `git` commands directly to commit/push as this bypasses the workflow

3. **Branch naming**: Feature branches follow the pattern `NNN-short-description`:
   - `001-logging-analyzer`
   - `002-html-reports`
   - `003-performance-improvements`

### Why This Matters

- **Protected main**: Ensures the main branch always contains stable, reviewed code
- **Feature isolation**: Each feature is developed and tested independently
- **Clear history**: Numbered branches create a chronological record of features
- **Review process**: All changes go through pull request review before merging

## Architecture Principles

### 1. Analyzer Pattern
- Each logging pattern (ILogger extensions, LoggerMessage attribute, etc.) has its own analyzer
- Analyzers implement `ILoggerUsageAnalyzer` interface
- All analyzers run in parallel for each syntax tree
- Results are aggregated into `LoggerUsageExtractionResult`

### 2. Roslyn Symbol Resolution
- Always use `LoggingTypes` class to access well-known logging symbols
- Never compare by string names alone - always use symbol comparison
- Use `ISymbol.Equals()` or `SymbolEqualityComparer` for symbol comparisons

### 3. Thread Safety
- Extraction runs in parallel across syntax trees
- Use `ConcurrentBag<T>` or similar thread-safe collections for result aggregation
- Analyzers should be stateless and thread-safe

## Using the Roslyn API Correctly

When checking if a symbol matches a well-known method/type, always compare the symbol directly, not just names. Use the `LoggingTypes` class and its modeler classes:

```csharp
// Access pre-resolved symbols through LoggingTypes
if (loggingTypes.LoggerExtensionModeler.IsLoggerMethod(operation.TargetMethod))
{
    // This is a logger extension method
}

// Or check against specific symbols
if (SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, loggingTypes.ILogger))
{
    // This method is on ILogger interface
}
```

## Coding Guidelines

### 1. Naming Conventions
- Use descriptive names for analyzers: `LogMethodAnalyzer`, `LoggerMessageAttributeAnalyzer`
- Models should end with appropriate suffixes: `LoggerUsageInfo`, `ExtractionResult`
- Extractors and utilities should be named by their function: `EventIdExtractor`, `LocationHelper`

### 2. Error Handling
- Log warnings for compilation issues (missing references, symbols not found)
- Use structured logging with meaningful context
- Never throw exceptions from analyzers - return empty results instead
- Skip analysis gracefully when required symbols are missing

### 3. Performance Considerations
- Cache expensive symbol lookups in `LoggingTypes`
- Use `Stopwatch` for performance measurement in debug scenarios

### 4. Logging and Diagnostics
- Use the injected `ILogger<T>` for diagnostics
- Log at appropriate levels: Debug for detailed analysis, Information for significant findings
- Include relevant context: file paths, method names, symbol information
- Use structured logging parameters consistently

### 5. Model Design
- Use required properties for essential data
- Provide sensible defaults for collections (empty lists, not null)
- Use nullable reference types appropriately
- Include comprehensive XML documentation for public APIs

### 6. Testing Philosophy: Integration-Only Approach

**The LoggerUsage library follows a strict integration testing approach** where all tests go through the public entrypoints of `LoggerUsageExtractor`. This is the **desired and correct approach**.

#### Why Integration Tests Only?

**Implementation Details Are Hidden**

All internal components are **implementation details** that should not be tested directly:
- Individual analyzers (`LogMethodAnalyzer`, `LoggerMessageAttributeAnalyzer`, etc.)
- Symbol resolution (`LoggingTypes`, `LoggerExtensionModeler`)
- Utility functions (`EventIdExtractor`, `LocationHelper`)
- Parameter extraction services
- Internal context objects

Testing these directly would:
1. ❌ Couple tests to implementation details
2. ❌ Make refactoring harder
3. ❌ Create brittle tests that break when internals change
4. ❌ Duplicate test coverage unnecessarily

**Public API Surface is the Contract**

The **only** contract users care about is:
- `LoggerUsageExtractor.ExtractLoggerUsagesAsync(Workspace, ...)`
- `LoggerUsageExtractor.ExtractLoggerUsagesWithSolutionAsync(Compilation, ...)`

These entrypoints define what the library does. Everything else is how it does it.

#### Correct Testing Pattern

**All tests go through `LoggerUsageExtractor` entrypoints:**

```csharp
var compilation = await TestUtils.CreateCompilationAsync(code);
var extractor = TestUtils.CreateLoggerUsageExtractor();
var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);
```

**Why this approach:**
- ✅ Tests the public API contract
- ✅ Implementation details can change freely
- ✅ Tests are resilient to refactoring
- ✅ Validates the full pipeline works correctly
- ✅ Focuses on user-facing behavior

**Test organization:**
- Separate files by feature/pattern being tested (`LoggerMethodsTests`, `LoggerMessageAttributeTests`, etc.)
- Use descriptive test names: `TestMethod_WithCondition_ExpectedResult`
- Use `[Theory]` and `[MemberData]` for variations and edge cases
- Test edge cases through the public API

**DO:**
- ✅ Test all features through `LoggerUsageExtractor` entrypoints
- ✅ Create compilations with test code using `TestUtils.CreateCompilationAsync()`
- ✅ Assert on the complete `LoggerUsageExtractionResult` returned
- ✅ Test edge cases: missing symbols, malformed templates, complex expressions
- ✅ Validate complete results: method type, parameters, location, EventId/LogLevel

**DO NOT:**
- ❌ Test individual analyzers directly
- ❌ Test internal utilities in isolation
- ❌ Test symbol resolution logic separately
- ❌ Create test doubles for internal components
- ❌ Couple tests to implementation details

**Exception:** Pure utility functions with stable contracts (like `LogValuesFormatter` and `LoggerUsageSummarizer`) may warrant separate test files, but this is rare. These have clear, stable contracts independent of the extraction pipeline and provide complex logic worth testing in isolation.

#### Benefits of This Approach

**Maximum Refactoring Freedom:**
- Change analyzer implementations → tests still pass
- Refactor symbol resolution → tests still pass
- Reorganize utilities → tests still pass
- **As long as behavior through `LoggerUsageExtractor` stays the same, tests pass**

**User-Facing Confidence:**
- Tests verify what users experience
- Failures point to actual user-facing issues
- No false positives from internal refactoring

**Fast and Maintainable:**
- Integration tests through `LoggerUsageExtractor` are already fast (Roslyn compilation is fast)
- Well isolated (each test creates its own compilation)
- Easy to debug (failures point to user-facing issues)
- Comprehensive (cover the full pipeline)


## Extension Points

### Adding New Analyzers

1. Implement `ILoggerUsageAnalyzer`
2. Register in `LoggerUsageBuilderExtensions.AddLoggerUsageExtractor()`
3. Add necessary symbol resolution to `LoggingTypes` if needed
4. Follow the established patterns for result extraction

Example analyzer structure:
```csharp
internal class NewPatternAnalyzer : ILoggerUsageAnalyzer
{
    public IEnumerable<LoggerUsageInfo> Analyze(LoggingTypes loggingTypes, SyntaxNode root, SemanticModel semanticModel)
    {
        // Find relevant syntax nodes
        var targetNodes = root.DescendantNodes().OfType<TargetSyntaxType>();
        
        foreach (var node in targetNodes)
        {
            // Get semantic information
            if (semanticModel.GetOperation(node) is not ITargetOperation operation)
                continue;
                
            // Validate against known symbols
            if (!IsTargetPattern(operation, loggingTypes))
                continue;
                
            // Extract information and yield result
            yield return ExtractUsageInfo(operation, node, loggingTypes);
        }
    }
}
```

### Adding New Models

When adding new model classes:
- Follow the established patterns in `Models/` folder
- Use `required` properties for essential data
- Provide XML documentation
- Consider serialization needs for CLI/MCP outputs

## Dependency Injection Integration

The library uses Microsoft.Extensions.DependencyInjection:

```csharp
services.AddLoggerUsageExtractor()
    .Services.AddSingleton<ICustomService, CustomService>();
```

Register services in `LoggerUsageBuilderExtensions`:
- Core services (extractors, modelers)
- Analyzers implementing `ILoggerUsageAnalyzer`
- Parameter extraction services
- Report generators

## Common Patterns

### 1. Symbol Resolution
```csharp
// In LoggingTypes constructor
MySymbol = compilation.GetTypeByMetadataName(typeof(MyType).FullName!)!;

// In analyzer
if (SymbolEqualityComparer.Default.Equals(symbol, loggingTypes.MySymbol))
{
    // Symbol matches
}
```

### 2. Operation Analysis
```csharp
if (semanticModel.GetOperation(syntaxNode) is not IInvocationOperation operation)
    continue;

// Analyze the operation for logging patterns
```

### 3. Location Extraction
```csharp
var location = LocationHelper.CreateFromInvocation(invocationSyntax);
// or
var location = LocationHelper.CreateFromMethodDeclaration(methodSyntax);
```

### 4. Parameter Extraction
```csharp
// Use appropriate parameter extractor
if (arrayParameterExtractor.TryExtract(argument, out var parameters))
{
    usage.MessageParameters.AddRange(parameters);
}
```

## Package Structure

- **LoggerUsage** - Core library with analyzers and models
- **LoggerUsage.Cli** - Command-line interface for generating reports
- **LoggerUsage.Mcp** - Model Context Protocol server for AI integrations
- **LoggerUsage.MSBuild** - MSBuild workspace factory integration

## File Organization

```
src/LoggerUsage/
├── Analyzers/           # Pattern-specific analyzers
├── Models/              # Data models and DTOs
├── DependencyInjection/ # Service registration
├── MessageTemplate/     # Template parsing logic
├── ParameterExtraction/ # Parameter extraction utilities
├── ReportGenerator/     # Output formatting
├── Services/            # Core services
└── Utilities/           # Helper classes
```

## References

- [Microsoft.CodeAnalysis Documentation](https://docs.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis)
- [Microsoft.Extensions.Logging](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging)
- [Roslyn Analyzers](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix)