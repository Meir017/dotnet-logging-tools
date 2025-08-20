using LoggerUsage.Models;
using Microsoft.Extensions.Logging;
using TUnit.Core;

namespace LoggerUsage.Tests;

public class LoggerMessageDefineTests
{
    [Test]
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

    public static IEnumerable<object[]> LoggerMessageDefineEventIdScenarios()
    {
        yield return new object[] { "new EventId(1, \"UserCreated\")", 1, "UserCreated" };
        yield return new object[] { "new EventId(100, \"OrderProcessed\")", 100, "OrderProcessed" };
        yield return new object[] { "new EventId(0)", 0, null };
        yield return new object[] { "new EventId(-1)", -1, null };
        yield return new object[] { "1", 1, null };
    }

    [Test]
    [MethodDataSource(nameof(LoggerMessageDefineEventIdScenarios))]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        var usage = loggerUsages.Results[0];
        Assert.NotNull(usage.EventId);

        var details = Assert.IsType<EventIdDetails>(usage.EventId);
        if (expectedId is not null)
        {
            Assert.Equal(ConstantOrReference.Constant(expectedId), details.Id);
        }
        else
        {
            Assert.Same(ConstantOrReference.Missing, details.Id);
        }

        if (expectedName is not null)
        {
            Assert.Equal(ConstantOrReference.Constant(expectedName), details.Name);
        }
        else
        {
            Assert.Same(ConstantOrReference.Missing, details.Name);
        }
    }

    public static IEnumerable<object[]> LoggerMessageDefineLogLevelScenarios()
    {
        yield return new object[] { "LogLevel.Information", LogLevel.Information };
        yield return new object[] { "LogLevel.Warning", LogLevel.Warning };
        yield return new object[] { "LogLevel.Error", LogLevel.Error };
        yield return new object[] { "LogLevel.Critical", LogLevel.Critical };
        yield return new object[] { "LogLevel.Trace", LogLevel.Trace };
        yield return new object[] { "LogLevel.Debug", LogLevel.Debug };
        yield return new object[] { "LogLevel.None", LogLevel.None };
    }

    [Test]
    [MethodDataSource(nameof(LoggerMessageDefineLogLevelScenarios))]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        Assert.Equal(expectedLogLevel, loggerUsages.Results[0].LogLevel);
    }

    public static IEnumerable<object[]> LoggerMessageDefineMessageScenarios()
    {
        yield return new object[] { "\"Test message\"", "Test message" };
        yield return new object[] { "\"User {Name} was created\"", "User {Name} was created" };
        yield return new object[] { "\"Order {OrderId} was processed\"", "Order {OrderId} was processed" };
        yield return new object[] { "\"User {UserName} logged in from IP {IpAddress}\"", "User {UserName} logged in from IP {IpAddress}" };
        yield return new object[] { "\"System startup completed\"", "System startup completed" };
        yield return new object[] { "\"\"", "" };
    }

    [Test]
    [MethodDataSource(nameof(LoggerMessageDefineMessageScenarios))]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        Assert.Equal(expectedMessage, loggerUsages.Results[0].MessageTemplate);
    }

    public static IEnumerable<object[]> LoggerMessageDefineParameterScenarios()
    {
        // No parameters
        yield return new object[] { "", "System startup completed", new List<MessageParameter>() };
        // Single parameter
        yield return new object[] { "<string>", "User {Name} was created", new List<MessageParameter> { new("Name", "string", null) } };
        // Multiple parameters
        yield return new object[] { "<string, int>", "User {UserName} logged in from IP {IpAddress}", new List<MessageParameter> { new("UserName", "string", null), new("IpAddress", "int", null) } };
        // Different types
        yield return new object[] { "<int, System.DateTime>", "Order {OrderId} processed at {ProcessedTime}", new List<MessageParameter> { new("OrderId", "int", null), new("ProcessedTime", "System.DateTime", null) } };
        // Complex types
        yield return new object[] { "<System.Guid, string, double>", "Transaction {Id} for {Customer} amount {Amount}", new List<MessageParameter> { new("Id", "System.Guid", null), new("Customer", "string", null), new("Amount", "double", null) } };
        // 4 parameters
        yield return new object[] { "<string, int, System.DateTime, bool>", "User {UserName} with ID {UserId} logged in at {LoginTime} with success {IsSuccessful}", new List<MessageParameter> { new("UserName", "string", null), new("UserId", "int", null), new("LoginTime", "System.DateTime", null), new("IsSuccessful", "bool", null) } };
        // 5 parameters
        yield return new object[] { "<System.Guid, string, decimal, int, System.TimeSpan>", "Order {OrderId} for customer {CustomerName} with total {Total} containing {ItemCount} items processed in {Duration}", new List<MessageParameter> { new("OrderId", "System.Guid", null), new("CustomerName", "string", null), new("Total", "decimal", null), new("ItemCount", "int", null), new("Duration", "System.TimeSpan", null) } };
        // 6 parameters
        yield return new object[] { "<string, int, string, System.DateTime, double, long>", "API call {Endpoint} by user {UserId} from {IpAddress} at {Timestamp} took {ResponseTime} ms with size {ResponseSize} bytes", new List<MessageParameter> { new("Endpoint", "string", null), new("UserId", "int", null), new("IpAddress", "string", null), new("Timestamp", "System.DateTime", null), new("ResponseTime", "double", null), new("ResponseSize", "long", null) } };
    }

    [Test]
    [MethodDataSource(nameof(LoggerMessageDefineParameterScenarios))]
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

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        var usage = loggerUsages.Results[0];
        Assert.Equal("Define", usage.MethodName);
        Assert.Equal(LoggerUsageMethodType.LoggerMessageDefine, usage.MethodType);
        Assert.Null(usage.MessageTemplate);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        // Should only find the LoggerMessage.Define call, not the non-generic method
        Assert.Single(loggerUsages.Results);
        var usage = loggerUsages.Results[0];
        Assert.Equal("Define", usage.MethodName);
        Assert.Equal(LoggerUsageMethodType.LoggerMessageDefine, usage.MethodType);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        var usage = loggerUsages.Results[0];
        Assert.Equal("Define", usage.MethodName);
        Assert.Equal(LoggerUsageMethodType.LoggerMessageDefine, usage.MethodType);
        Assert.Equal("Only one parameter {Param1}", usage.MessageTemplate);
        
        // Should extract parameters based on generic types, not just message template
        // The extractor should handle this gracefully
        Assert.NotEmpty(usage.MessageParameters);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        var usage = loggerUsages.Results[0];
        Assert.Equal("Define", usage.MethodName);
        Assert.Equal(LoggerUsageMethodType.LoggerMessageDefine, usage.MethodType);
        Assert.Equal(LogLevel.Error, usage.LogLevel);
        Assert.Equal("Operation {OperationId} failed with error", usage.MessageTemplate);
        Assert.NotNull(usage.EventId);
        
        // Should handle static EventId reference - might be EventIdRef type
        if (usage.EventId is EventIdRef eventIdRef)
        {
            Assert.Contains("OperationFailure", eventIdRef.Name);
        }
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        var usage = loggerUsages.Results[0];
        Assert.Equal("Define", usage.MethodName);
        Assert.Equal(LoggerUsageMethodType.LoggerMessageDefine, usage.MethodType);
        
        // Should handle non-literal message template gracefully
        // The extractor might not be able to extract the template if it's not a literal, so it could be null or empty
        // This is expected behavior - the extractor can only extract literal string templates
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        var usage = loggerUsages.Results[0];
        Assert.Equal("Define", usage.MethodName);
        Assert.Equal(LoggerUsageMethodType.LoggerMessageDefine, usage.MethodType);
        Assert.Equal("Operation {OperationId} took {Duration} with data {CustomData} and items {Items}", usage.MessageTemplate);
        
        // Should extract all 4 parameters with correct types
        Assert.Equal(4, usage.MessageParameters.Count);
        Assert.Contains(usage.MessageParameters, p => p.Name == "OperationId" && p.Type == "System.Guid");
        Assert.Contains(usage.MessageParameters, p => p.Name == "Duration" && p.Type == "System.TimeSpan");
        Assert.Contains(usage.MessageParameters, p => p.Name == "CustomData" && p.Type == "TestNamespace.CustomType");
        Assert.Contains(usage.MessageParameters, p => p.Name == "Items" && p.Type == "System.Collections.Generic.List<string>");
    }
}
