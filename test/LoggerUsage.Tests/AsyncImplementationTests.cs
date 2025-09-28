using LoggerUsage.Analyzers;
using LoggerUsage.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Tests;

/// <summary>
/// Tests for the async implementation of ILoggerUsageAnalyzer.
/// </summary>
public class AsyncImplementationTests
{
    [Fact]
    public async Task AllAnalyzers_ImplementAsyncMethod_Successfully()
    {
        // Arrange
        var code = @"
using Microsoft.Extensions.Logging;

public class TestClass
{
    private readonly ILogger<TestClass> _logger;
    
    public TestClass(ILogger<TestClass> logger)
    {
        _logger = logger;
    }
    
    public void TestMethod()
    {
        _logger.LogInformation(""Test message with {UserId}"", 123);
    }
}";

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractorService = TestUtils.CreateLoggerUsageExtractor();
        
        // Act - Test the new async method
        var result = await extractorService.ExtractLoggerUsagesWithSolutionAsync(compilation);
        
        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Results);
        
        // Should find at least one LogInformation usage
        var logInfoUsage = result.Results.FirstOrDefault(r => r.MethodName == "LogInformation");
        Assert.NotNull(logInfoUsage);
        Assert.Equal("Test message with {UserId}", logInfoUsage.MessageTemplate);
    }

    [Fact]
    public async Task AsyncAnalyzers_MaintainParallelExecution_Performance()
    {
        // Arrange - Create code with multiple log statements
        var code = @"
using Microsoft.Extensions.Logging;
public class TestClass
{
    private readonly ILogger<TestClass> _logger;
    
    public TestClass(ILogger<TestClass> logger) 
    { 
        _logger = logger; 
    }
    
    public void Method1() 
    { 
        _logger.LogInformation(""Message 1"");
        _logger.LogWarning(""Message 2""); 
        _logger.LogError(""Message 3"");
    }
}";

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractorService = TestUtils.CreateLoggerUsageExtractor();
        
        // Act
        var startTime = DateTime.UtcNow;
        var result = await extractorService.ExtractLoggerUsagesWithSolutionAsync(compilation);
        var duration = DateTime.UtcNow - startTime;
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Results.Count); // Should find all three log messages
        
        // Parallel execution should be relatively fast (this is a basic smoke test)
        Assert.True(duration.TotalSeconds < 10, $"Execution took too long: {duration.TotalSeconds} seconds");
        
        // Verify all messages were found
        Assert.Contains(result.Results, r => r.MethodName == "LogInformation");
        Assert.Contains(result.Results, r => r.MethodName == "LogWarning"); 
        Assert.Contains(result.Results, r => r.MethodName == "LogError");
    }

    [Fact]
    public async Task BackwardCompatibility_SyncMethod_StillWorks()
    {
        // Arrange
        var code = @"
using Microsoft.Extensions.Logging;

public class TestClass
{
    private readonly ILogger<TestClass> _logger;
    
    public TestClass(ILogger<TestClass> logger)
    {
        _logger = logger;
    }
    
    public void TestMethod()
    {
        _logger.LogDebug(""Debug message"");
    }
}";

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractorService = TestUtils.CreateLoggerUsageExtractor();
        
        // Act - Test the original sync method still works
        var result = extractorService.ExtractLoggerUsagesWithSolution(compilation);
        
        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Results);
        
        var logDebugUsage = result.Results.FirstOrDefault(r => r.MethodName == "LogDebug");
        Assert.NotNull(logDebugUsage);
        Assert.Equal(LogLevel.Debug, logDebugUsage.LogLevel);
    }
}