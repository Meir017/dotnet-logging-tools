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

    public static TheoryData<string, string, List<MessageParameter>> LoggerMessageParameterScenarios() => new()
    {
        // Single parameter
        { "Message = \"User {UserId} logged in\",", "ILogger logger, int userId", [new("UserId", "int", null)] },
        // Multiple parameters
        { "Message = \"User {UserId} performed {Action} at {Time}\",", "ILogger logger, int userId, string action, System.DateTime time", [new("UserId", "int", null), new("Action", "string", null), new("Time", "System.DateTime", null)] },
        // Case insensitivity
        { "Message = \"User {userid} did {ACTION}\",", "ILogger logger, int UserId, string Action", [new("userid", "int", null), new("ACTION", "string", null)] },
        // Exclude ILogger, LogLevel, Exception
        { "Message = \"Error for {UserId}\",", "ILogger logger, int userId, System.Exception ex", [new("UserId", "int", null)] },
        // Exclude ILogger, LogLevel, custom Exception
        { "Message = \"Error for {UserId}\",", "ILogger logger, int userId, System.ArgumentException ex", [new("UserId", "int", null)] },
        // Complex placeholder syntax
        { "Message = \"User {UserId:X}\",", "ILogger logger, int userId", [new("UserId", "int", null)] },
        // Fully qualified type
        { "Message = \"User {UserId} logged {Id} in\",", "ILogger logger, System.Int32 userId, System.String id", [new("UserId", "int", null), new("Id", "string", null)] },
        // Nullable types
        { "Message = \"User {UserId} logged in\",", "ILogger logger, int? userId", [new("UserId", "int?", null)] },
        // Generic types
        { "Message = \"User {Ids} logged in\",", "ILogger logger, System.Collections.Generic.List<System.Int32> ids", [new("Ids", "System.Collections.Generic.List<int>", null)] },
        // With [LogProperties]
        { "Message = \"User {UserId} logged in\",", "ILogger logger, int userId, [LogProperties] LogData data", [new("UserId", "int", null)] },
        // With [LogProperties] and multiple arguments
        { "Message = \"User {UserId} logged in {Action}\",", "ILogger logger, int userId, [LogProperties] LogData data, string action", [new("UserId", "int", null), new("Action", "string", null)] },
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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
