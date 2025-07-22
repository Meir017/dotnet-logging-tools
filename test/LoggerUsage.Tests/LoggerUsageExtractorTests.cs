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
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Equal(2, loggerUsages.Results.Count);
    }

}

