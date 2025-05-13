using LoggerUsage.Models;
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

    public static TheoryData<string, int?, string?> LoggerMessageEventIdNamedArgumentsScenarios() => new()
    {
        { "EventId = 1,", 1, null },
        { "EventId = 2, EventName = \"Name2\",", 2, "Name2" },
        { "EventId = 0,", 0, null },
        { "EventId = -1,", -1, null },
        { "EventId = IdConstant, EventName = NameConstant,", 6, "ConstantNameField" },
        { "EventId = int.MaxValue,", int.MaxValue, null },
        { "EventId = 1 + 2,", 3, null },
        { "EventName = nameof(TestMethod),", null, "TestMethod" },
        { string.Empty, null, null },
    };

    [Theory]
    [MemberData(nameof(LoggerMessageEventIdNamedArgumentsScenarios))]
    public async Task LoggerMessage_EventId_EventName_NamesArguments_Scenarios(string? eventIdAndNameArg, int? expectedId, string? expectedName)
    {
        // Arrange
        var code = $@"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public static partial class Log
{{
    const int IdConstant = 6;
    const string NameConstant = ""ConstantNameField"";

    [LoggerMessage(
        {eventIdAndNameArg}
        Level = LogLevel.Information,
        Message = ""Test message""
    )]
    public static partial void TestMethod(ILogger logger);
}}

// mock generated code:
partial class Log
{{
    public static partial void TestMethod(ILogger logger) {{ }}
}}
";

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();

        // Act
        var result = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        var usage = result[0];
        if (expectedId == null && expectedName == null)
        {
            Assert.Null(usage.EventId);
            return;
        }

        var details = Assert.IsType<EventIdDetails>(usage.EventId);
        if (expectedId is not null)
            Assert.Equal(ConstantOrReference.Constant(expectedId), details.Id);
        else
            Assert.Same(ConstantOrReference.Missing, details.Id);

        if (expectedName is not null)
            Assert.Equal(ConstantOrReference.Constant(expectedName), details.Name);
        else
            Assert.Same(ConstantOrReference.Missing, details.Name);
    }
}