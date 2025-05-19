using LoggerUsage.Models;
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        Assert.Equal(LoggerUsageMethodType.LoggerMessageAttribute, loggerUsages.Results[0].MethodType);
    }

    public static TheoryData<string, int?, string?> LoggerMessageEventIdScenarios() => new()
    {
        { "EventId = 1,", 1, null },
        { "EventId = 2, EventName = \"Name2\",", 2, "Name2" },
        { "EventId = 0,", 0, null },
        { "EventId = -1,", -1, null },
        { "EventId = IdConstant, EventName = NameConstant,", 6, "ConstantNameField" },
        { "EventId = int.MaxValue,", int.MaxValue, null },
        { "EventId = 1 + 2,", 3, null },
        { "EventName = nameof(TestMethod),", null, "TestMethod" },
        { "1, LogLevel.Information, \"ctor message\",", 1, null },
        { "1, LogLevel.Information, \"ctor message\", EventId = 3,", 3, null },
        { "1, LogLevel.Information, \"ctor message\", EventId = int.MaxValue,", int.MaxValue, null },
        { "1, LogLevel.Information, \"ctor message\", EventId = IdConstant,", 6, null },
        { "1, LogLevel.Information, \"ctor message\", EventId = IdConstant,", 6, null },
        { "eventId: 1, level: LogLevel.Information, message: \"ctor message\",", 1, null },
        { "level: LogLevel.Information, eventId: 1, message: \"ctor message\",", 1, null },
        { "level: LogLevel.Information, message: \"ctor message\", eventId: 1,", 1, null },


        { string.Empty, null, null },
    };

    [Theory]
    [MemberData(nameof(LoggerMessageEventIdScenarios))]
    public async Task LoggerMessage_EventId_EventName_Scenarios(string? eventIdAndNameArg, int? expectedId, string? expectedName)
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        var usage = loggerUsages.Results[0];
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

    public static TheoryData<string, LogLevel?> LoggerMessageLogLevelScenarios() => new()
    {
        { "Level = LogLevel.Information,", LogLevel.Information },
        { "Level = LogLevel.Warning,", LogLevel.Warning },
        { "Level = LogLevel.Error,", LogLevel.Error },
        { "Level = LogLevel.Critical,", LogLevel.Critical },
        { "Level = LogLevel.Trace,", LogLevel.Trace },
        { "Level = LogLevel.Debug,", LogLevel.Debug },
        { "Level = LogLevel.None,", LogLevel.None },

        { "(LogLevel)0,", LogLevel.Trace },
        { "(LogLevel)1,", LogLevel.Debug },
        { "(LogLevel)2,", LogLevel.Information },
        { "(LogLevel)3,", LogLevel.Warning },
        { "(LogLevel)4,", LogLevel.Error },
        { "(LogLevel)5,", LogLevel.Critical },
        { "(LogLevel)6,", LogLevel.None },
        { "LogLevel.Information,", LogLevel.Information },
        { "LogLevel.Information, \"ctor message\",", LogLevel.Information },
        { "1, LogLevel.Information, \"ctor message\",", LogLevel.Information },
        { "1, LogLevel.Warning, \"ctor message\",", LogLevel.Warning },
        { "level: LogLevel.Error, eventId: 1, message: \"ctor message\",", LogLevel.Error },
        { "eventId: 1, level: LogLevel.Critical, message: \"ctor message\",", LogLevel.Critical },
        { "eventId: 1, message: \"ctor message\", level: LogLevel.Trace,", LogLevel.Trace },
        { "LogLevel.Warning, Level = LogLevel.Information,", LogLevel.Information },
        { string.Empty, null },
    };

    [Theory]
    [MemberData(nameof(LoggerMessageLogLevelScenarios))]
    public async Task LoggerMessage_LogLevel_Scenarios(string? logLevelArg, LogLevel? expectedLogLevel)
    {
        // Arrange
        var code = $@"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public static partial class Log
{{
    [LoggerMessage(
        {logLevelArg}
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        Assert.Equal(expectedLogLevel, loggerUsages.Results[0].LogLevel);
    }

    public static TheoryData<string, string?> LoggerMessageMessageScenarios() => new()
    {
        { "Message = \"Test message\",", "Test message" },
        { "Message = \"Another message\",", "Another message" },
        { "1, LogLevel.Information, \"Ctor message\",", "Ctor message" },
        { "1, LogLevel.Information, \"\",", "" },
        { "1, LogLevel.Information, null,", null },
        { "message: \"Named message\",", "Named message" },
        { "LogLevel.Information, \"Ctor message 2\",", "Ctor message 2" },
        { "1, LogLevel.Information, \"Ctor message 3\", Message = \"Override message\",", "Override message" },
        { string.Empty, null },
    };

    [Theory]
    [MemberData(nameof(LoggerMessageMessageScenarios))]
    public async Task LoggerMessage_Message_Scenarios(string? messageArg, string? expectedMessage)
    {
        // Arrange
        var code = $@"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public static partial class Log
{{
    [LoggerMessage(
        {messageArg}
        Level = LogLevel.Information
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        if (expectedMessage == null)
            Assert.True(string.IsNullOrEmpty(loggerUsages.Results[0].MessageTemplate));
        else
            Assert.Equal(expectedMessage, loggerUsages.Results[0].MessageTemplate);
    }

    public static TheoryData<string, string, List<MessageParameter>> LoggerMessageParameterScenarios() => new()
    {
        // Single parameter
        { "Message = \"User {UserId} logged in\",", "ILogger logger, int userId", [new MessageParameter("userId", "int", null)] },
        // Multiple parameters
        { "Message = \"User {UserId} performed {Action} at {Time}\",", "ILogger logger, int userId, string action, System.DateTime time", [new MessageParameter("userId", "int", null), new MessageParameter("action", "string", null), new MessageParameter("time", "System.DateTime", null)] },
        // Case insensitivity
        { "Message = \"User {userid} did {ACTION}\",", "ILogger logger, int UserId, string Action", [new MessageParameter("UserId", "int", null), new MessageParameter("Action", "string", null)] },
        // Exclude ILogger, LogLevel, Exception
        { "Message = \"Error for {UserId}\",", "ILogger logger, int userId, System.Exception ex", [new MessageParameter("userId", "int", null)] },
        // Complex placeholder syntax
        { "Message = \"User {UserId:X}\",", "ILogger logger, int userId", [new MessageParameter("userId", "int", null)] },
    };

    [Theory]
    [MemberData(nameof(LoggerMessageParameterScenarios))]
    public async Task LoggerMessage_Parameter_Scenarios(string messageArg, string methodParameters, List<MessageParameter> expectedParameters)
    {
        // Arrange
        var code = $@"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public static partial class Log
{{
    [LoggerMessage(
        {messageArg}
        Level = LogLevel.Information
    )]
    public static partial void TestMethod({methodParameters});
}}

// mock generated code:
partial class Log
{{
    public static partial void TestMethod({methodParameters}) {{ }}
}}
";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        var usage = loggerUsages.Results[0];
        if (expectedParameters.Count == 0)
        {
            Assert.Empty(usage.MessageParameters);
        }
        else
        {
            Assert.Equal(expectedParameters.Count, usage.MessageParameters.Count);
            Assert.Equal(expectedParameters, usage.MessageParameters);
        }
    }
}