using LoggerUsage.Models;
using Microsoft.Extensions.Logging;
using TUnit.Core;

namespace LoggerUsage.Tests;

public class LoggerMessageAttributeTests
{
    [Test]
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
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
    public static partial void TestMethod(ILogger logger) { }
}
";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        Assert.Equal(LoggerUsageMethodType.LoggerMessageAttribute, loggerUsages.Results[0].MethodType);
    }

    public static IEnumerable<object[]> LoggerMessageEventIdScenarios()
    {
        yield return new object[] { "EventId = 1,", 1, null };
        yield return new object[] { "EventId = 2, EventName = \"Name2\",", 2, "Name2" };
        yield return new object[] { "EventId = 0,", 0, null };
        yield return new object[] { "EventId = -1,", -1, null };
        yield return new object[] { "EventId = IdConstant, EventName = NameConstant,", 6, "ConstantNameField" };
        yield return new object[] { "EventId = int.MaxValue,", int.MaxValue, null };
        yield return new object[] { "EventId = 1 + 2,", 3, null };
        yield return new object[] { "EventName = nameof(TestMethod),", null, "TestMethod" };
        yield return new object[] { "1, LogLevel.Information, \"ctor message\",", 1, null };
        yield return new object[] { "1, LogLevel.Information, \"ctor message\", EventId = 3,", 3, null };
        yield return new object[] { "1, LogLevel.Information, \"ctor message\", EventId = int.MaxValue,", int.MaxValue, null };
        yield return new object[] { "1, LogLevel.Information, \"ctor message\", EventId = IdConstant,", 6, null };
        yield return new object[] { "1, LogLevel.Information, \"ctor message\", EventId = IdConstant,", 6, null };
        yield return new object[] { "eventId: 1, level: LogLevel.Information, message: \"ctor message\",", 1, null };
        yield return new object[] { "level: LogLevel.Information, eventId: 1, message: \"ctor message\",", 1, null };
        yield return new object[] { "level: LogLevel.Information, message: \"ctor message\", eventId: 1,", 1, null };
        yield return new object[] { string.Empty, null, null };
    }

    [Test]
    [MethodDataSource(nameof(LoggerMessageEventIdScenarios))]
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
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
    public static partial void TestMethod(ILogger logger) {{ }}
}}
";

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

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

    public static IEnumerable<object[]> LoggerMessageLogLevelScenarios()
    {
        yield return new object[] { "Level = LogLevel.Information,", LogLevel.Information };
        yield return new object[] { "Level = LogLevel.Warning,", LogLevel.Warning };
        yield return new object[] { "Level = LogLevel.Error,", LogLevel.Error };
        yield return new object[] { "Level = LogLevel.Critical,", LogLevel.Critical };
        yield return new object[] { "Level = LogLevel.Trace,", LogLevel.Trace };
        yield return new object[] { "Level = LogLevel.Debug,", LogLevel.Debug };
        yield return new object[] { "Level = LogLevel.None,", LogLevel.None };

        yield return new object[] { "(LogLevel)0,", LogLevel.Trace };
        yield return new object[] { "(LogLevel)1,", LogLevel.Debug };
        yield return new object[] { "(LogLevel)2,", LogLevel.Information };
        yield return new object[] { "(LogLevel)3,", LogLevel.Warning };
        yield return new object[] { "(LogLevel)4,", LogLevel.Error };
        yield return new object[] { "(LogLevel)5,", LogLevel.Critical };
        yield return new object[] { "(LogLevel)6,", LogLevel.None };
        yield return new object[] { "LogLevel.Information,", LogLevel.Information };
        yield return new object[] { "LogLevel.Information, \"ctor message\",", LogLevel.Information };
        yield return new object[] { "1, LogLevel.Information, \"ctor message\",", LogLevel.Information };
        yield return new object[] { "1, LogLevel.Warning, \"ctor message\",", LogLevel.Warning };
        yield return new object[] { "level: LogLevel.Error, eventId: 1, message: \"ctor message\",", LogLevel.Error };
        yield return new object[] { "eventId: 1, level: LogLevel.Critical, message: \"ctor message\",", LogLevel.Critical };
        yield return new object[] { "eventId: 1, message: \"ctor message\", level: LogLevel.Trace,", LogLevel.Trace };
        yield return new object[] { "LogLevel.Warning, Level = LogLevel.Information,", LogLevel.Information };
        yield return new object[] { string.Empty, null };
    }

    [Test]
    [MethodDataSource(nameof(LoggerMessageLogLevelScenarios))]
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
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
    public static partial void TestMethod(ILogger logger) {{ }}
}}
";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        Assert.Equal(expectedLogLevel, loggerUsages.Results[0].LogLevel);
    }

    public static IEnumerable<object[]> LoggerMessageMessageScenarios()
    {
        yield return new object[] { "Message = \"Test message\",", "Test message" };
        yield return new object[] { "Message = \"Another message\",", "Another message" };
        yield return new object[] { "1, LogLevel.Information, \"Ctor message\",", "Ctor message" };
        yield return new object[] { "1, LogLevel.Information, \"\",", "" };
        yield return new object[] { "1, LogLevel.Information, null,", null };
        yield return new object[] { "message: \"Named message\",", "Named message" };
        yield return new object[] { "LogLevel.Information, \"Ctor message 2\",", "Ctor message 2" };
        yield return new object[] { "1, LogLevel.Information, \"Ctor message 3\", Message = \"Override message\",", "Override message" };
        yield return new object[] { string.Empty, null };
    }

    [Test]
    [MethodDataSource(nameof(LoggerMessageMessageScenarios))]
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
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
    public static partial void TestMethod(ILogger logger) {{ }}
}}
";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        if (expectedMessage == null)
        {
            Assert.True(string.IsNullOrEmpty(loggerUsages.Results[0].MessageTemplate));
        }
        else
        {
            Assert.Equal(expectedMessage, loggerUsages.Results[0].MessageTemplate);
        }
    }

    public static IEnumerable<object[]> LoggerMessageParameterScenarios()
    {
        // Single parameter
        yield return new object[] { "Message = \"User {UserId} logged in\",", "ILogger logger, int userId", new List<MessageParameter> { new("UserId", "int", null) } };
        // Multiple parameters
        yield return new object[] { "Message = \"User {UserId} performed {Action} at {Time}\",", "ILogger logger, int userId, string action, System.DateTime time", new List<MessageParameter> { new("UserId", "int", null), new("Action", "string", null), new("Time", "System.DateTime", null) } };
        // Case insensitivity
        yield return new object[] { "Message = \"User {userid} did {ACTION}\",", "ILogger logger, int UserId, string Action", new List<MessageParameter> { new("userid", "int", null), new("ACTION", "string", null) } };
        // Exclude ILogger, LogLevel, Exception
        yield return new object[] { "Message = \"Error for {UserId}\",", "ILogger logger, int userId, System.Exception ex", new List<MessageParameter> { new("UserId", "int", null) } };
        // Exclude ILogger, LogLevel, custom Exception
        yield return new object[] { "Message = \"Error for {UserId}\",", "ILogger logger, int userId, System.ArgumentException ex", new List<MessageParameter> { new("UserId", "int", null) } };
        // Complex placeholder syntax
        yield return new object[] { "Message = \"User {UserId:X}\",", "ILogger logger, int userId", new List<MessageParameter> { new("UserId", "int", null) } };
        // Fully qualified type
        yield return new object[] { "Message = \"User {UserId} logged {Id} in\",", "ILogger logger, System.Int32 userId, System.String id", new List<MessageParameter> { new("UserId", "int", null), new("Id", "string", null) } };
        // Nullable types
        yield return new object[] { "Message = \"User {UserId} logged in\",", "ILogger logger, int? userId", new List<MessageParameter> { new("UserId", "int?", null) } };
        // Generic types
        yield return new object[] { "Message = \"User {Ids} logged in\",", "ILogger logger, System.Collections.Generic.List<System.Int32> ids", new List<MessageParameter> { new("Ids", "System.Collections.Generic.List<int>", null) } };
        // With [LogProperties]
        yield return new object[] { "Message = \"User {UserId} logged in\",", "ILogger logger, int userId, [LogProperties] LogData data", new List<MessageParameter> { new("UserId", "int", null) } };
        // With [LogProperties] and multiple arguments
        yield return new object[] { "Message = \"User {UserId} logged in {Action}\",", "ILogger logger, int userId, [LogProperties] LogData data, string action", new List<MessageParameter> { new("UserId", "int", null), new("Action", "string", null) } };
    }

    [Test]
    [MethodDataSource(nameof(LoggerMessageParameterScenarios))]
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
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
    public static partial void TestMethod({methodParameters.Replace("[LogProperties] ", "")}) {{ }}
}}

public class LogData
{{
    public int UserId {{ get; set; }}
    public string Action {{ get; set; }}
    public System.DateTime Time {{ get; set; }}
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

    #region LoggerMessage Invocation Tests

    [Test]
    public async Task LoggerMessageWithInvocation_ReturnsLoggerMessageUsageInfo()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User {UserId} logged in""
    )]
    public static partial void LogUserLogin(ILogger logger, int userId);
}

// Mock generated code:
partial class Log
{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
    public static partial void LogUserLogin(ILogger logger, int userId) { }
}

public class UserService
{
    private readonly ILogger<UserService> _logger;

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    public void LoginUser(int userId)
    {
        Log.LogUserLogin(_logger, userId);
    }
}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Equal("LogUserLogin", usage.MethodName);
        Assert.Equal(LoggerUsageMethodType.LoggerMessageAttribute, usage.MethodType);
        Assert.Equal("TestNamespace.Log", usage.DeclaringTypeName);
        Assert.True(usage.HasInvocations);
        Assert.Equal(1, usage.InvocationCount);

        var invocation = Assert.Single(usage.Invocations);
        Assert.Equal("TestNamespace.UserService", invocation.ContainingType);
        Assert.NotNull(invocation.InvocationLocation);
    }

    [Test]
    public async Task LoggerMessageWithMultipleInvocations_TracksAllCallSites()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User {UserId} logged in""
    )]
    public static partial void LogUserLogin(ILogger logger, int userId);
}

// Mock generated code:
partial class Log
{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
    public static partial void LogUserLogin(ILogger logger, int userId) { }
}

public class UserService
{
    private readonly ILogger<UserService> _logger;

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    public void LoginUser(int userId)
    {
        Log.LogUserLogin(_logger, userId);
    }

    public void LoginUserFromAdmin(int userId)
    {
        Log.LogUserLogin(_logger, userId);
    }
}

public class AdminService
{
    private readonly ILogger<AdminService> _logger;

    public AdminService(ILogger<AdminService> logger)
    {
        _logger = logger;
    }

    public void ProcessUserLogin(int userId)
    {
        Log.LogUserLogin(_logger, userId);
    }
}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Equal("LogUserLogin", usage.MethodName);
        Assert.True(usage.HasInvocations);
        Assert.Equal(3, usage.InvocationCount);

        // Verify invocations from different containing types
        var userServiceInvocations = usage.Invocations.Where(i => i.ContainingType == "TestNamespace.UserService").ToList();
        var adminServiceInvocations = usage.Invocations.Where(i => i.ContainingType == "TestNamespace.AdminService").ToList();

        Assert.Equal(2, userServiceInvocations.Count);
        Assert.Single(adminServiceInvocations);
    }

    [Test]
    public async Task LoggerMessageWithoutInvocations_ReturnsEmptyInvocationsList()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Unused message""
    )]
    public static partial void UnusedMethod(ILogger logger);
}

// Mock generated code:
partial class Log
{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
    public static partial void UnusedMethod(ILogger logger) { }
}

public class UserService
{
    private readonly ILogger<UserService> _logger;

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    // Method exists but LoggerMessage is never called
    public void DoSomething()
    {
        _logger.LogInformation(""Regular log message"");
    }
}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Equal(2, loggerUsages.Results.Count); // LoggerMessage + regular logger call

        var loggerMessageUsage = loggerUsages.Results
            .OfType<LoggerMessageUsageInfo>()
            .Single(u => u.MethodName == "UnusedMethod");

        Assert.False(loggerMessageUsage.HasInvocations);
        Assert.Equal(0, loggerMessageUsage.InvocationCount);
        Assert.Empty(loggerMessageUsage.Invocations);
    }

    [Test]
    public async Task LoggerMessageInvocation_ExtractsArgumentInformation()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User {UserId} performed {Action}""
    )]
    public static partial void LogUserAction(ILogger logger, int userId, string action);
}

// Mock generated code:
partial class Log
{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
    public static partial void LogUserAction(ILogger logger, int userId, string action) { }
}

public class UserService
{
    private readonly ILogger<UserService> _logger;

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    public void LogAction(int userId, string action)
    {
        Log.LogUserAction(_logger, userId, action);
    }
}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.True(usage.HasInvocations);

        var invocation = Assert.Single(usage.Invocations);
        Assert.Equal(3, invocation.Arguments.Count); // logger, userId, action

        // Verify argument details
        var loggerArg = invocation.Arguments.FirstOrDefault(a => a.Name == "logger");
        var userIdArg = invocation.Arguments.FirstOrDefault(a => a.Name == "userId");
        var actionArg = invocation.Arguments.FirstOrDefault(a => a.Name == "action");

        Assert.NotNull(loggerArg);
        Assert.NotNull(userIdArg);
        Assert.NotNull(actionArg);

        Assert.Equal("int", userIdArg.Type);
        Assert.Equal("string", actionArg.Type);
    }

    [Test]
    public async Task MultipleLoggerMessageMethods_TracksInvocationsIndependently()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User {UserId} logged in""
    )]
    public static partial void LogUserLogin(ILogger logger, int userId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = ""User {UserId} failed login""
    )]
    public static partial void LogUserLoginFailed(ILogger logger, int userId);
}

// Mock generated code:
partial class Log
{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
    public static partial void LogUserLogin(ILogger logger, int userId) { }
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
    public static partial void LogUserLoginFailed(ILogger logger, int userId) { }
}

public class UserService
{
    private readonly ILogger<UserService> _logger;

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    public void LoginUser(int userId)
    {
        Log.LogUserLogin(_logger, userId);
        Log.LogUserLogin(_logger, userId); // Called twice
    }

    public void FailedLogin(int userId)
    {
        Log.LogUserLoginFailed(_logger, userId); // Called once
    }
}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Equal(2, loggerUsages.Results.Count);

        var loginUsage = loggerUsages.Results.OfType<LoggerMessageUsageInfo>()
            .Single(u => u.MethodName == "LogUserLogin");
        var loginFailedUsage = loggerUsages.Results.OfType<LoggerMessageUsageInfo>()
            .Single(u => u.MethodName == "LogUserLoginFailed");

        Assert.Equal(2, loginUsage.InvocationCount);
        Assert.Equal(1, loginFailedUsage.InvocationCount);
    }

    [Test]
    public async Task LoggerMessageInDifferentNamespace_TracksInvocationsCorrectly()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

namespace TestNamespace.Logging
{
    public static partial class ApplicationLog
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = ""Application started""
        )]
        public static partial void LogApplicationStarted(ILogger logger);
    }
}

// Mock generated code:
namespace TestNamespace.Logging
{
    partial class ApplicationLog
    {
        [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
        public static partial void LogApplicationStarted(ILogger logger) { }
    }
}

namespace TestNamespace.Services
{
    public class StartupService
    {
        private readonly ILogger<StartupService> _logger;

        public StartupService(ILogger<StartupService> logger)
        {
            _logger = logger;
        }

        public void Start()
        {
            TestNamespace.Logging.ApplicationLog.LogApplicationStarted(_logger);
        }
    }
}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Equal("LogApplicationStarted", usage.MethodName);
        Assert.Equal("TestNamespace.Logging.ApplicationLog", usage.DeclaringTypeName);
        Assert.True(usage.HasInvocations);

        var invocation = Assert.Single(usage.Invocations);
        Assert.Equal("TestNamespace.Services.StartupService", invocation.ContainingType);
    }

    #endregion
}
