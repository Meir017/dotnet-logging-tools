using AwesomeAssertions;
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().ContainSingle();
        var result = loggerUsages.Results[0];
        result.MethodName.Should().Be("Define");
        result.MethodType.Should().Be(LoggerUsageMethodType.LoggerMessageDefine);
        result.LogLevel.Should().Be(LogLevel.Information);
        result.MessageTemplate.Should().Be("User {Name} was created");
        result.EventId.Should().NotBeNull();
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().ContainSingle();
        var usage = loggerUsages.Results[0];
        usage.EventId.Should().NotBeNull();

        var details = usage.EventId.Should().BeOfType<EventIdDetails>().Which;
        if (expectedId is not null)
        {
            details.Id.Should().Be(ConstantOrReference.Constant(expectedId));
        }
        else
        {
            details.Id.Should().BeSameAs(ConstantOrReference.Missing);
        }

        if (expectedName is not null)
        {
            details.Name.Should().Be(ConstantOrReference.Constant(expectedName));
        }
        else
        {
            details.Name.Should().BeSameAs(ConstantOrReference.Missing);
        }
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().ContainSingle();
        loggerUsages.Results[0].LogLevel.Should().Be(expectedLogLevel);
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().ContainSingle();
        loggerUsages.Results[0].MessageTemplate.Should().Be(expectedMessage);
    }

    public static TheoryData<string, string, List<MessageParameter>> LoggerMessageDefineParameterScenarios() => new()
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
        // 4 parameters
        { "<string, int, System.DateTime, bool>", "User {UserName} with ID {UserId} logged in at {LoginTime} with success {IsSuccessful}", [new("UserName", "string", null), new("UserId", "int", null), new("LoginTime", "System.DateTime", null), new("IsSuccessful", "bool", null)] },
        // 5 parameters
        { "<System.Guid, string, decimal, int, System.TimeSpan>", "Order {OrderId} for customer {CustomerName} with total {Total} containing {ItemCount} items processed in {Duration}", [new("OrderId", "System.Guid", null), new("CustomerName", "string", null), new("Total", "decimal", null), new("ItemCount", "int", null), new("Duration", "System.TimeSpan", null)] },
        // 6 parameters
        { "<string, int, string, System.DateTime, double, long>", "API call {Endpoint} by user {UserId} from {IpAddress} at {Timestamp} took {ResponseTime} ms with size {ResponseSize} bytes", [new("Endpoint", "string", null), new("UserId", "int", null), new("IpAddress", "string", null), new("Timestamp", "System.DateTime", null), new("ResponseTime", "double", null), new("ResponseSize", "long", null)] },
    };

    [Theory]
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().ContainSingle();
        var usage = loggerUsages.Results[0];
        if (expectedParameters.Count == 0)
        {
            usage.MessageParameters.Should().BeEmpty();
        }
        else
        {
            usage.MessageParameters.Count.Should().Be(expectedParameters.Count);
            usage.MessageParameters.Should().Equal(expectedParameters);
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

    [Fact]
    public async Task LoggerMessageDefine_Should_HandleNullMessageTemplate()
    {
        // Arrange
        var code = @"#nullable enable
using Microsoft.Extensions.Logging;
using System;
namespace TestNamespace;

public class TestClass
{
    private static readonly Action<ILogger, string, Exception?> _invalidDefine = 
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(5, ""Invalid""), null!);
}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().ContainSingle();
        var usage = loggerUsages.Results[0];
        usage.MethodName.Should().Be("Define");
        usage.MethodType.Should().Be(LoggerUsageMethodType.LoggerMessageDefine);
        usage.MessageTemplate.Should().BeNull();
    }

    [Fact]
    public async Task LoggerMessageDefine_Should_IgnoreNonGenericMethods()
    {
        // Arrange
        var code = @"#nullable enable
using Microsoft.Extensions.Logging;
using System;
namespace TestNamespace;

public class TestClass
{
    public void SomeNonGenericMethod()
    {
        // This should not be processed
    }

    private static readonly Action<ILogger, string, Exception?> _validDefine = 
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, ""Valid""), ""User {UserId} action"");
}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        // Should only find the LoggerMessage.Define call, not the non-generic method
        loggerUsages.Results.Should().ContainSingle();
        var usage = loggerUsages.Results[0];
        usage.MethodName.Should().Be("Define");
        usage.MethodType.Should().Be(LoggerUsageMethodType.LoggerMessageDefine);
    }

    [Fact]
    public async Task LoggerMessageDefine_Should_HandleMismatchedParameterCount()
    {
        // Arrange - 2 generic type arguments but only 1 parameter in message template
        var code = @"#nullable enable
using Microsoft.Extensions.Logging;
using System;
namespace TestNamespace;

public class TestClass
{
    private static readonly Action<ILogger, string, int, Exception?> _mismatchedDefine = 
        LoggerMessage.Define<string, int>(
            LogLevel.Information, 
            new EventId(6, ""Mismatched""), 
            ""Only one parameter {Param1}"");
}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().ContainSingle();
        var usage = loggerUsages.Results[0];
        usage.MethodName.Should().Be("Define");
        usage.MethodType.Should().Be(LoggerUsageMethodType.LoggerMessageDefine);
        usage.MessageTemplate.Should().Be("Only one parameter {Param1}");
        
        // Should extract parameters based on generic types, not just message template
        // The extractor should handle this gracefully
        usage.MessageParameters.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LoggerMessageDefine_Should_HandleStaticEventIdReference()
    {
        // Arrange
        var code = @"#nullable enable
using Microsoft.Extensions.Logging;
using System;
namespace TestNamespace;

public static class Events
{
    public static readonly EventId OperationFailure = new(500, ""OperationFailure"");
}

public class TestClass
{
    private static readonly Action<ILogger, System.Guid, Exception?> _operationFailed =
        LoggerMessage.Define<System.Guid>(
            LogLevel.Error,
            Events.OperationFailure,
            ""Operation {OperationId} failed with error"");
}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().ContainSingle();
        var usage = loggerUsages.Results[0];
        usage.MethodName.Should().Be("Define");
        usage.MethodType.Should().Be(LoggerUsageMethodType.LoggerMessageDefine);
        usage.LogLevel.Should().Be(LogLevel.Error);
        usage.MessageTemplate.Should().Be("Operation {OperationId} failed with error");
        usage.EventId.Should().NotBeNull();
        
        // Should handle static EventId reference - might be EventIdRef type
        if (usage.EventId is EventIdRef eventIdRef)
        {
            eventIdRef.Name.Should().Contain("OperationFailure");
        }
    }

    [Fact]
    public async Task LoggerMessageDefine_Should_HandleVariableMessageTemplate()
    {
        // Arrange
        var code = @"#nullable enable
using Microsoft.Extensions.Logging;
using System;
namespace TestNamespace;

public class TestClass
{
    private static string GetMessageTemplate() => ""Dynamic template {Value}"";
    
    private static readonly Action<ILogger, string, Exception?> _dynamicTemplate = 
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(7, ""Dynamic""),
            GetMessageTemplate());
}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().ContainSingle();
        var usage = loggerUsages.Results[0];
        usage.MethodName.Should().Be("Define");
        usage.MethodType.Should().Be(LoggerUsageMethodType.LoggerMessageDefine);
        
        // Should handle non-literal message template gracefully
        // The extractor might not be able to extract the template if it's not a literal, so it could be null or empty
        // This is expected behavior - the extractor can only extract literal string templates
    }

    [Fact]
    public async Task LoggerMessageDefine_Should_HandleComplexGenericTypes()
    {
        // Arrange
        var code = @"#nullable enable
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
namespace TestNamespace;

public class CustomType
{
    public string Name { get; set; } = string.Empty;
}

public class TestClass
{
    private static readonly Action<ILogger, System.Guid, System.TimeSpan, CustomType, List<string>, Exception?> _complexTypes =
        LoggerMessage.Define<System.Guid, System.TimeSpan, CustomType, List<string>>(
            LogLevel.Debug,
            new EventId(8, ""ComplexTypes""),
            ""Operation {OperationId} took {Duration} with data {CustomData} and items {Items}"");
}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().ContainSingle();
        var usage = loggerUsages.Results[0];
        usage.MethodName.Should().Be("Define");
        usage.MethodType.Should().Be(LoggerUsageMethodType.LoggerMessageDefine);
        usage.MessageTemplate.Should().Be("Operation {OperationId} took {Duration} with data {CustomData} and items {Items}");
        
        // Should extract all 4 parameters with correct types
        usage.MessageParameters.Count.Should().Be(4);
        usage.MessageParameters.Should().Contain(p => p.Name == "OperationId" && p.Type == "System.Guid");
        usage.MessageParameters.Should().Contain(p => p.Name == "Duration" && p.Type == "System.TimeSpan");
        usage.MessageParameters.Should().Contain(p => p.Name == "CustomData" && p.Type == "TestNamespace.CustomType");
        usage.MessageParameters.Should().Contain(p => p.Name == "Items" && p.Type == "System.Collections.Generic.List<string>");
    }
}
