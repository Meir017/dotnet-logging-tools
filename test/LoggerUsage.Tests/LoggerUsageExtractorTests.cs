namespace LoggerUsage.Tests;

public class LoggerUsageExtractorTests
{
    [Fact]
    public async Task BasicTestWithLogMethodAndLoggerMessageAttribute()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public partial class TestClass
{
    public void TestMethod(ILogger logger)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(""Test message"");
        }
        TestLogMethod(logger);
    }

    [LoggerMessage(
        LogLevel.Information,
        ""Test message""
    )]
    private static partial void TestLogMethod(ILogger logger);
}

partial class TestClass
{
    private static partial void TestLogMethod(ILogger logger) {}
}");
        var extractor = new LoggerUsageExtractor();

        // Act
        var result = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

}

