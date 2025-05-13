using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Tests;

public class LoggerMessageAttributeTests
{
    [Fact]
    public async Task BasicTest()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Test message""
    )]
    public static partial void TestMethod(ILogger logger);
}

// mock generated code:
partial class Log
{
    public static partial void TestMethod(ILogger logger) { }
}
";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();

        // Act
        var result = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
    }
}