
# LoggerUsage Library Development Guidelines

This document provides comprehensive guidelines for developing and maintaining the LoggerUsage library, a .NET library for analyzing logging patterns in C# code using Roslyn.

## Project Overview

The LoggerUsage library is a sophisticated static code analysis tool that extracts and analyzes logging patterns from .NET projects. It focuses on Microsoft.Extensions.Logging patterns and provides insights into logging consistency and usage.

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

When checking if a symbol matches some well-known method/type, always compare the symbol, not just the name and type name. This ensures that you are checking the correct method or type, especially in cases where there might be multiple definitions or overloads.

For example, instead of checking just the name and type name:

DON'T DO THIS:

```csharp
if (symbol.Name == "LogInformation" && symbol.ContainingType.Name == "ILogger")
{
    // This is the correct symbol
}
```

DO THIS:

use the `ISymbol` interface to check the symbol directly:

```csharp
var logInformationSymbol = context.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.LoggerExtensions")?
    .GetMembers("LogInformation").FirstOrDefault();
if (symbol is IMethodSymbol methodSymbol && methodSymbol.Name == "LogInformation" && methodSymbol.ContainingType.ToDisplayString() == "Microsoft.Extensions.Logging.ILogger")
{
    // This is the correct symbol
}
```

When working with the Roslyn API, always ensure you are using the correct symbol type and properties to avoid issues with method resolution and type checking. This will help maintain the integrity of your code analysis and extraction logic.

### Better Symbol Comparison Pattern

Use the `LoggingTypes` class and its modeler classes:

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

Example:
```csharp
_logger.LogInformation("Analyzer {AnalyzerType} found {UsageCount} usages in {FilePath} in {Duration}ms", 
    analyzer.GetType().Name, usageCount, filePath, duration.TotalMilliseconds);
```

### 5. Model Design
- Use required properties for essential data
- Provide sensible defaults for collections (empty lists, not null)
- Use nullable reference types appropriately
- Include comprehensive XML documentation for public API's.

### 6. Testing Patterns

The test suite follows established patterns for Roslyn-based analyzers with comprehensive coverage of logging scenarios.

#### Test Infrastructure

**TestUtils Class**: Central utility for test setup
```csharp
// Create compilations with proper references
var compilation = await TestUtils.CreateCompilationAsync(sourceCode);
var extractor = TestUtils.CreateLoggerUsageExtractor();
```

**Key Features:**
- Uses `ReferenceAssemblies.Net.Net90` for consistent .NET references
- Automatically includes `Microsoft.Extensions.Logging` assemblies
- Suppresses common compiler warnings for test scenarios
- Validates compilation has no errors before analysis

#### Test Organization

**By Analyzer Type:**
- `LoggerMethodsTests.cs` - ILogger extension method analysis
- `LoggerMessageAttributeTests.cs` - [LoggerMessage] attribute patterns
- `LoggerMessageDefineTests.cs` - LoggerMessage.Define usage
- `BeginScopeTests.cs` - Scope creation patterns
- `LoggerUsageSummarizerTests.cs` - Summary generation logic

#### Common Test Patterns

**Basic Test Structure:**
```csharp
[Fact]
public async Task BasicTest()
{
    // Arrange
    var compilation = await TestUtils.CreateCompilationAsync(@"
        using Microsoft.Extensions.Logging;
        namespace TestNamespace;
        
        public class TestClass
        {
            public void TestMethod(ILogger logger)
            {
                logger.LogInformation(""Test message"");
            }
        }");
    var extractor = TestUtils.CreateLoggerUsageExtractor();

    // Act
    var result = extractor.ExtractLoggerUsages(compilation);

    // Assert
    Assert.Single(result.Results);
    Assert.Equal(LoggerUsageMethodType.LoggerExtensions, result.Results[0].MethodType);
}
```

**Theory Tests for Parameter Variations:**
```csharp
[Theory]
[MemberData(nameof(LoggerMessageParameterCases))]
public async Task TestLoggerMessageParameters(
    string template, 
    string[] argNames, 
    List<MessageParameter> expectedParameters)
{
    // Generate test code dynamically
    var methodArgs = '"' + template + '"' + 
        (argNames.Length > 0 ? ", " : "") + 
        string.Join(", ", argNames);
    
    var code = $@"
        using Microsoft.Extensions.Logging;
        public class TestClass {{
            public void TestMethod(ILogger logger, string strArg, int intArg) {{
                logger.LogInformation({methodArgs});
            }}
        }}";
        
    // Test parameter extraction accuracy
    Assert.Equal(expectedParameters, result.Results[0].MessageParameters);
}
```

**Parameter Test Data:**
```csharp
public static TheoryData<string, string[], List<MessageParameter>> LoggerMessageParameterCases() => new()
{
    { "Test {arg1}", ["strArg"], [ 
        new MessageParameter("arg1", "string", nameof(OperationKind.ParameterReference))
    ] },
    { "Test {arg1} {arg2}", ["strArg.Length", "intArg.ToString()"], [
        new MessageParameter("arg1", "int", nameof(OperationKind.PropertyReference)),
        new MessageParameter("arg2", "string", nameof(OperationKind.Invocation))
    ] },
    // ... more complex scenarios
};
```

#### Specialized Test Scenarios

**LoggerMessage Attribute Tests:**
```csharp
[Fact]
public async Task LoggerMessageAttributeTest()
{
    var code = @"
        using Microsoft.Extensions.Logging;
        public static partial class Log
        {
            [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = ""Test message"")]
            public static partial void TestMethod(ILogger logger);
        }
        
        // Mock generated code:
        partial class Log
        {
            public static partial void TestMethod(ILogger logger) { }
        }";
        
    // Verify attribute-based pattern detection
    Assert.Equal(LoggerUsageMethodType.LoggerMessageAttribute, result.MethodType);
}
```

**Scope Analysis Tests:**
```csharp
[Theory]
[InlineData("logger.BeginScope(\"Test scope\")", "Test scope")]
[InlineData("logger.BeginScope(\"User {userId}\", userId)", "User {userId}")]
public async Task BeginScopeTests(string scopeCall, string expectedTemplate)
{
    // Test scope pattern recognition and template extraction
}
```

#### Custom Test Infrastructure

**XUnit Serialization:**
- Custom `MessageParameterListXunitSerializer` for complex model comparison
- Registered via assembly attribute for seamless theory test support

**Test Compilation Features:**
- Nullable reference types enabled (`#nullable enable`)
- Comprehensive field/property scenarios for parameter extraction
- Edge cases: conditional access (`?.`), method chaining, constants

#### Best Practices for New Tests

1. **Use Descriptive Test Names**: `TestLoggerMessageParameters_WithPropertyAccess_ExtractsCorrectType`

2. **Test Edge Cases:**
   - Missing references/symbols
   - Malformed templates
   - Complex parameter expressions
   - Generated code patterns

3. **Validate Complete Results:**
   - Method type classification
   - Template extraction accuracy
   - Parameter name/type/operation mapping
   - Location information
   - EventId and LogLevel extraction

4. **Use Theory Tests for Variations:**
   - Different logging method overloads
   - Various parameter types and expressions
   - Template complexity scenarios

5. **Mock Generated Code When Needed:**
   - LoggerMessage source generators
   - Partial method implementations
   - Compiler-generated patterns


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

## Future Considerations

- Support for custom logging frameworks
- Enhanced parameter type analysis
- Performance optimizations for large codebases
- Integration with static analysis tools
- Support for configuration-based analysis rules

## References

- [Microsoft.CodeAnalysis Documentation](https://docs.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis)
- [Microsoft.Extensions.Logging](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging)
- [Roslyn Analyzers](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix)