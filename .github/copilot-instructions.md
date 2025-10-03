
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

### 6. Testing Patterns

Tests use `TestUtils.CreateCompilationAsync()` and `TestUtils.CreateLoggerUsageExtractor()` for setup. Key patterns:

- **Basic Tests**: Create compilation, extract usages, assert results
- **Theory Tests**: Use `[MemberData]` for parameter variations and edge cases
- **Test Organization**: Separate files by analyzer type (`LoggerMethodsTests`, `LoggerMessageAttributeTests`, etc.)

**Best Practices:**
- Use descriptive test names with pattern: `TestMethod_WithCondition_ExpectedResult`
- Test edge cases: missing symbols, malformed templates, complex expressions
- Validate complete results: method type, parameters, location, EventId/LogLevel
- Mock generated code for LoggerMessage patterns when needed


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