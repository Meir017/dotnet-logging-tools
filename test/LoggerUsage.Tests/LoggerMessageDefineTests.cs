using LoggerUsage.Models;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Tests;

public class LoggerMessageDefineTests
{
    [Fact]
    public async Task BasicTest()
    {
        // Arrange
        var code = @"#nullable enable
using Microsoft.Extensions.Logging;
using System;
namespace TestNamespace;

public class TestClass
{
    private static readonly Action<ILogger, string, Exception?> _logUserCreated = 
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, ""UserCreated""), ""User {Name} was created"");
}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        var result = loggerUsages.Results[0];
        Assert.Equal("Define", result.MethodName);
        Assert.Equal(LoggerUsageMethodType.LoggerMessageDefine, result.MethodType);
        Assert.Equal(LogLevel.Information, result.LogLevel);
        Assert.Equal("User {Name} was created", result.MessageTemplate);
        Assert.NotNull(result.EventId);
    }

    public static TheoryData<string, int?, string?> LoggerMessageDefineEventIdScenarios() => new()
    {
        { "new EventId(1, \"UserCreated\")", 1, "UserCreated" },
        { "new EventId(100, \"OrderProcessed\")", 100, "OrderProcessed" },
        { "new EventId(0)", 0, null },
        { "new EventId(-1)", -1, null },
        { "1", 1, null },
    };

    [Theory]
    [MemberData(nameof(LoggerMessageDefineEventIdScenarios))]
    public async Task LoggerMessageDefine_EventId_Scenarios(string eventIdArg, int? expectedId, string? expectedName)
    {
        // Arrange
        var code = $@"#nullable enable
using Microsoft.Extensions.Logging;
using System;
namespace TestNamespace;

public class TestClass
{{
    private static readonly Action<ILogger, string, Exception?> _logAction = 
        LoggerMessage.Define<string>(LogLevel.Information, {eventIdArg}, ""Test message {{Name}}"");
}}";

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        var usage = loggerUsages.Results[0];
        Assert.NotNull(usage.EventId);

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

    public static TheoryData<string, LogLevel> LoggerMessageDefineLogLevelScenarios() => new()
    {
        { "LogLevel.Information", LogLevel.Information },
        { "LogLevel.Warning", LogLevel.Warning },
        { "LogLevel.Error", LogLevel.Error },
        { "LogLevel.Critical", LogLevel.Critical },
        { "LogLevel.Trace", LogLevel.Trace },
        { "LogLevel.Debug", LogLevel.Debug },
        { "LogLevel.None", LogLevel.None },
    };

    [Theory]
    [MemberData(nameof(LoggerMessageDefineLogLevelScenarios))]
    public async Task LoggerMessageDefine_LogLevel_Scenarios(string logLevelArg, LogLevel expectedLogLevel)
    {
        // Arrange
        var code = $@"#nullable enable
using Microsoft.Extensions.Logging;
using System;
namespace TestNamespace;

public class TestClass
{{
    private static readonly Action<ILogger, Exception?> _logAction = 
        LoggerMessage.Define({logLevelArg}, new EventId(1), ""Test message"");
}}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        Assert.Equal(expectedLogLevel, loggerUsages.Results[0].LogLevel);
    }

    public static TheoryData<string, string> LoggerMessageDefineMessageScenarios() => new()
    {
        { "\"Test message\"", "Test message" },
        { "\"User {Name} was created\"", "User {Name} was created" },
        { "\"Order {OrderId} was processed\"", "Order {OrderId} was processed" },
        { "\"User {UserName} logged in from IP {IpAddress}\"", "User {UserName} logged in from IP {IpAddress}" },
        { "\"System startup completed\"", "System startup completed" },
        { "\"\"", "" },
    };

    [Theory]
    [MemberData(nameof(LoggerMessageDefineMessageScenarios))]
    public async Task LoggerMessageDefine_Message_Scenarios(string messageArg, string expectedMessage)
    {
        // Arrange
        var code = $@"#nullable enable
using Microsoft.Extensions.Logging;
using System;
namespace TestNamespace;

public class TestClass
{{
    private static readonly Action<ILogger, Exception?> _logAction = 
        LoggerMessage.Define(LogLevel.Information, new EventId(1), {messageArg});
}}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        Assert.Equal(expectedMessage, loggerUsages.Results[0].MessageTemplate);
    }    public static TheoryData<string, string, List<MessageParameter>> LoggerMessageDefineParameterScenarios() => new()
    {
        // No parameters
        { "", "System startup completed", [] },
        // Single parameter
        { "<string>", "User {Name} was created", [new("Name", "string", null)] },
        // Multiple parameters
        { "<string, int>", "User {UserName} logged in from IP {IpAddress}", [new("UserName", "string", null), new("IpAddress", "int", null)] },
        // Different types
        { "<int, System.DateTime>", "Order {OrderId} processed at {ProcessedTime}", [new("OrderId", "int", null), new("ProcessedTime", "System.DateTime", null)] },
        // Complex types
        { "<System.Guid, string, double>", "Transaction {Id} for {Customer} amount {Amount}", [new("Id", "System.Guid", null), new("Customer", "string", null), new("Amount", "double", null)] },
    };    [Theory]
    [MemberData(nameof(LoggerMessageDefineParameterScenarios))]
    public async Task LoggerMessageDefine_Parameter_Scenarios(string genericTypes, string messageTemplate, List<MessageParameter> expectedParameters)
    {
        // Generate the correct Action delegate type based on generic parameters
        var actionType = GenerateActionType(genericTypes);
        
        // Arrange
        var code = $@"#nullable enable
using Microsoft.Extensions.Logging;
using System;
namespace TestNamespace;

public class TestClass
{{
    private static readonly {actionType} _logAction = 
        LoggerMessage.Define{genericTypes}(LogLevel.Information, new EventId(1), ""{messageTemplate}"");
}}";
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

    private static string GenerateActionType(string genericTypes)
    {
        if (string.IsNullOrEmpty(genericTypes))
        {
            return "Action<ILogger, Exception?>";
        }

        // Parse the generic types and create the Action type
        var typeList = genericTypes.Trim('<', '>');
        if (string.IsNullOrEmpty(typeList))
        {
            return "Action<ILogger, Exception?>";
        }

        var types = typeList.Split(',').Select(t => t.Trim()).ToArray();
        var actionParams = new List<string> { "ILogger" };
        actionParams.AddRange(types);
        actionParams.Add("Exception?");

        return $"Action<{string.Join(", ", actionParams)}>";
    }
}
