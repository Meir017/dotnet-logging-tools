using LoggerUsage.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Tests;

public class LoggerMessageAttributeTests
{
    private static string CreateMockGeneratedCode(string className, string methodSignature) =>
        $@"
// Mock generated code:
partial class {className}
{{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
    public static partial void {methodSignature} {{ }}
}}";

    private static CSharpCompilationOptions CreateTestCompilationOptions() =>
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
            {
                ["CS0169"] = ReportDiagnostic.Suppress,
                ["CS0649"] = ReportDiagnostic.Suppress,
                ["CS0219"] = ReportDiagnostic.Suppress,
                ["CS0414"] = ReportDiagnostic.Suppress,
                ["CS8602"] = ReportDiagnostic.Suppress,
            });
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
}" + CreateMockGeneratedCode("Log", "TestMethod(ILogger logger)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

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
}}" + CreateMockGeneratedCode("Log", "TestMethod(ILogger logger)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

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
}}" + CreateMockGeneratedCode("Log", "TestMethod(ILogger logger)");
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

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
}}" + CreateMockGeneratedCode("Log", "TestMethod(ILogger logger)");
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

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
}}" + CreateMockGeneratedCode("Log", $"TestMethod({methodParameters.Replace("[LogProperties] ", "")})") + @"

public class LogData
{
    public int UserId { get; set; }
    public string Action { get; set; }
    public System.DateTime Time { get; set; }
}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

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
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

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
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

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
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

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
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

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
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

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
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

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

    [Fact]
    public async Task LoggerMessageWithInMemorySolution_TestsCrossProjectInvocationFinding()
    {
        // This test properly tests cross-project invocation finding:
        // - Project A defines LoggerMessage methods
        // - Project B references A and calls the LoggerMessage methods
        // - When analyzing Project A with solution context, should find invocations from Project B

        // Arrange - Create proper cross-project solution
        var (solution, loggerProjectId, consumerProjectId) = await CreateInMemorySolutionWithLoggerMessageProjects();

        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act - Analyze the LOGGER project but provide the full solution for cross-project analysis
        var loggerProject = solution.GetProject(loggerProjectId)!;
        var loggerCompilation = await loggerProject.GetCompilationAsync(TestContext.Current.CancellationToken);
        var resultsWithSolution = await extractor.ExtractLoggerUsagesWithSolutionAsync(loggerCompilation!, solution);

        // Also test without solution (should only find local invocations = 0)
        var resultsWithoutSolution = await extractor.ExtractLoggerUsagesWithSolutionAsync(loggerCompilation!);

        // Assert
        Assert.NotNull(resultsWithSolution);
        Assert.Single(resultsWithSolution.Results);
        Assert.NotNull(resultsWithoutSolution);
        Assert.Single(resultsWithoutSolution.Results);

        var usageWithSolution = Assert.IsType<LoggerMessageUsageInfo>(resultsWithSolution.Results[0]);
        var usageWithoutSolution = Assert.IsType<LoggerMessageUsageInfo>(resultsWithoutSolution.Results[0]);

        // Without solution: Should find 0 invocations (no local invocations in logger project)
        Assert.False(usageWithoutSolution.HasInvocations, "Without solution context, should not find cross-project invocations");
        Assert.Equal(0, usageWithoutSolution.InvocationCount);

        // With solution: Should find cross-project invocations using SymbolFinder.FindCallersAsync
        Assert.True(usageWithSolution.HasInvocations, $"With solution context, should find cross-project invocations but found {usageWithSolution.InvocationCount}");
        Assert.Equal(2, usageWithSolution.InvocationCount); // Should find 2 invocations from consumer project

        // Verify invocations are from the consumer project
        Assert.All(usageWithSolution.Invocations, invocation =>
            Assert.Contains("ConsumerProject", invocation.ContainingType));
    }

    [Fact]
    public async Task LoggerMessageWithCrossProjectInvocations_FindsInvocationsUsingInMemorySolution()
    {
        // Arrange - Create an in-memory solution with multiple projects
        var (solution, loggerProjectId, consumerProjectId) = await CreateInMemorySolutionWithLoggerMessageProjects();

        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act - Extract from logger project but provide solution for cross-project analysis
        var loggerProject = solution.GetProject(loggerProjectId)!;
        var loggerCompilation = await loggerProject.GetCompilationAsync(TestContext.Current.CancellationToken);
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(loggerCompilation!, solution);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Equal("LogUserActivity", usage.MethodName);
        Assert.Equal("LoggerProject.UserLogger", usage.DeclaringTypeName);

        Assert.True(usage.HasInvocations, $"Expected invocations to be found, but got {usage.InvocationCount} invocations");
        Assert.Equal(2, usage.InvocationCount); // Should find invocations in consumer project

        // Verify invocations from consumer project
        var consumerInvocations = usage.Invocations.Where(i => i.ContainingType.Contains("ConsumerProject")).ToList();
        Assert.Equal(2, consumerInvocations.Count);

        // Should find invocations in both services
        Assert.Contains(usage.Invocations, i => i.ContainingType == "ConsumerProject.Services.UserService");
        Assert.Contains(usage.Invocations, i => i.ContainingType == "ConsumerProject.Services.ActivityService");
    }

    [Fact]
    public async Task LoggerMessageWithCrossProjectInvocations_FallsBackToLocalAnalysisWhenNoSolution()
    {
        // Arrange - Create the same projects but analyze without solution context
        var (solution, loggerProjectId, consumerProjectId) = await CreateInMemorySolutionWithLoggerMessageProjects();

        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act - Extract from logger project WITHOUT solution (should only find local invocations)
        var loggerProject = solution.GetProject(loggerProjectId)!;
        var loggerCompilation = await loggerProject.GetCompilationAsync(TestContext.Current.CancellationToken);
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(loggerCompilation!); // No solution parameter

        // Assert - Should still find the LoggerMessage declaration but no cross-project invocations
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Equal("LogUserActivity", usage.MethodName);
        Assert.Equal("LoggerProject.UserLogger", usage.DeclaringTypeName);
        Assert.False(usage.HasInvocations); // Should not find cross-project invocations
        Assert.Equal(0, usage.InvocationCount);
    }

    private static async Task<(Solution solution, ProjectId loggerProjectId, ProjectId consumerProjectId)> CreateInMemorySolutionWithLoggerMessageProjects()
    {
        // Create base references
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, default);
        var loggerReference = MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location);
        var logPropertiesReference = MetadataReference.CreateFromFile(typeof(LogPropertiesAttribute).Assembly.Location);
        var baseReferences = references.Add(loggerReference).Add(logPropertiesReference);

        // Create solution
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;

        // Logger project code - ONLY contains LoggerMessage declarations (NO invocations)
        var loggerProjectCode = @"using Microsoft.Extensions.Logging;

namespace LoggerProject
{
    public static partial class UserLogger
    {
        [LoggerMessage(
            EventId = 100,
            Level = LogLevel.Information,
            Message = ""User {UserId} performed activity {ActivityType} at {Timestamp}""
        )]
        public static partial void LogUserActivity(ILogger logger, int userId, string activityType, System.DateTime timestamp);
    }
}

// Mock generated code:
namespace LoggerProject
{
    partial class UserLogger
    {
        [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
        public static partial void LogUserActivity(ILogger logger, int userId, string activityType, System.DateTime timestamp) { }
    }
}";

        // Consumer project code - Contains invocations of LoggerMessage methods
        var consumerProjectCode = @"using Microsoft.Extensions.Logging;
using LoggerProject;

namespace ConsumerProject.Services
{
    public class UserService
    {
        private readonly ILogger<UserService> _logger;

        public UserService(ILogger<UserService> logger)
        {
            _logger = logger;
        }

        public void ProcessUserLogin(int userId)
        {
            UserLogger.LogUserActivity(_logger, userId, ""Login"", System.DateTime.Now);
        }
    }

    public class ActivityService
    {
        private readonly ILogger<ActivityService> _logger;

        public ActivityService(ILogger<ActivityService> logger)
        {
            _logger = logger;
        }

        public void TrackUserActivity(int userId, string activity)
        {
            UserLogger.LogUserActivity(_logger, userId, activity, System.DateTime.UtcNow);
        }
    }
}";

        // Create logger project
        var loggerProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("LoggerProject"),
            VersionStamp.Create(),
            "LoggerProject",
            "LoggerProject",
            LanguageNames.CSharp,
            compilationOptions: CreateTestCompilationOptions(),
            metadataReferences: baseReferences);

        solution = solution.AddProject(loggerProjectInfo);
        var loggerProjectId = loggerProjectInfo.Id;

        // Add source file to logger project
        solution = solution.AddDocument(DocumentId.CreateNewId(loggerProjectId), "UserLogger.cs", loggerProjectCode);

        // Create consumer project with reference to logger project
        var consumerProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("ConsumerProject"),
            VersionStamp.Create(),
            "ConsumerProject",
            "ConsumerProject",
            LanguageNames.CSharp,
            compilationOptions: CreateTestCompilationOptions(),
            metadataReferences: baseReferences,
            projectReferences: [new ProjectReference(loggerProjectId)]);

        solution = solution.AddProject(consumerProjectInfo);
        var consumerProjectId = consumerProjectInfo.Id;

        // Add source file to consumer project
        solution = solution.AddDocument(DocumentId.CreateNewId(consumerProjectId), "Services.cs", consumerProjectCode);

        // Verify compilations are valid
        var loggerCompilation = await solution.GetProject(loggerProjectId)!.GetCompilationAsync();
        var consumerCompilation = await solution.GetProject(consumerProjectId)!.GetCompilationAsync();

        var loggerErrors = loggerCompilation!.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var consumerErrors = consumerCompilation!.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        Assert.Empty(loggerErrors);
        Assert.Empty(consumerErrors);

        return (solution, loggerProjectId, consumerProjectId);
    }

    #endregion

    #region SymbolFinder Logic Validation Tests

    [Fact]
    public async Task LoggerMessage_SymbolFinderWithGeneratedMethods_UnderstandsPartialVsGenerated()
    {
        // This test verifies if SymbolFinder can find callers when we search for
        // the partial method declaration vs the generated implementation

        var (solution, loggerProjectId, consumerProjectId) = await CreateSimpleCrossProjectSolution();
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        var loggerProject = solution.GetProject(loggerProjectId)!;
        var loggerCompilation = await loggerProject.GetCompilationAsync(TestContext.Current.CancellationToken);

        // Find the LoggerMessage partial method symbol
        var partialMethodSymbol = FindLoggerMessageMethodSymbol(loggerCompilation!, "LogUserAction");
        Assert.NotNull(partialMethodSymbol);
        Assert.True(partialMethodSymbol.IsPartialDefinition);

        // Test 1: Search for callers using the partial method symbol
        var callersFromPartial = await SymbolFinder.FindCallersAsync(partialMethodSymbol, solution, TestContext.Current.CancellationToken);

        // Find the generated implementation (if it exists)
        var generatedMethodSymbol = FindGeneratedLoggerMessageMethod(loggerCompilation!, "LogUserAction");
        if (generatedMethodSymbol != null)
        {
            // Test 2: Search for callers using the generated method symbol
            var callersFromGenerated = await SymbolFinder.FindCallersAsync(generatedMethodSymbol, solution, TestContext.Current.CancellationToken);

            // Analysis: Which symbol finds the actual invocations?
            Assert.True(callersFromPartial.Any() || callersFromGenerated.Any(),
                "Either partial or generated method should find invocations");
        }
    }

    [Fact]
    public async Task LoggerMessage_SymbolFinderWithMultipleInvocationsInSameProject_FindsAllCallSites()
    {
        // Test: One consumer project with multiple calls to the same LoggerMessage method

        var (solution, loggerProjectId, consumerProjectId) = await CreateCrossProjectWithMultipleInvocations();

        var loggerProject = solution.GetProject(loggerProjectId)!;
        var loggerCompilation = await loggerProject.GetCompilationAsync(TestContext.Current.CancellationToken);

        var methodSymbol = FindLoggerMessageMethodSymbol(loggerCompilation!, "LogUserAction");
        var callers = await SymbolFinder.FindCallersAsync(methodSymbol!, solution, TestContext.Current.CancellationToken);

        var totalLocations = callers.SelectMany(c => c.Locations).Count();

        // Should find multiple invocations from the consumer project
        Assert.True(totalLocations >= 2, $"Expected at least 2 invocations, found {totalLocations}");
    }

    [Fact]
    public async Task LoggerMessage_SymbolFinderWithTwoConsumerProjects_FindsInvocationsFromBoth()
    {
        // Test: Two different projects both invoking the same LoggerMessage method

        var (solution, loggerProjectId, consumer1Id, consumer2Id) = await CreateCrossProjectWithTwoConsumers();

        var loggerProject = solution.GetProject(loggerProjectId)!;
        var loggerCompilation = await loggerProject.GetCompilationAsync(TestContext.Current.CancellationToken);

        var methodSymbol = FindLoggerMessageMethodSymbol(loggerCompilation!, "LogUserAction");
        var callers = await SymbolFinder.FindCallersAsync(methodSymbol!, solution, TestContext.Current.CancellationToken);

        // Group by project to see distribution
        var callersByProject = callers.GroupBy(c =>
        {
            var firstLocation = c.Locations.FirstOrDefault();
            if (firstLocation?.SourceTree != null)
            {
                var docId = solution.GetDocumentId(firstLocation.SourceTree);
                return solution.GetProject(docId!.ProjectId)?.Name ?? "Unknown";
            }
            return "Unknown";
        }).ToList();

        // Should find invocations from both consumer projects
        Assert.True(callersByProject.Count >= 2,
            $"Expected invocations from 2+ projects, found from {callersByProject.Count} projects");
    }

    [Fact]
    public async Task LoggerMessage_SymbolFinderWithStaticUsing_FindsInvocationsRegardlessOfSyntax()
    {
        // Test: Different ways of calling the LoggerMessage method (static using vs qualified)

        var (solution, loggerProjectId, consumerProjectId) = await CreateCrossProjectWithDifferentInvocationStyles();

        var loggerProject = solution.GetProject(loggerProjectId)!;
        var loggerCompilation = await loggerProject.GetCompilationAsync(TestContext.Current.CancellationToken);

        var methodSymbol = FindLoggerMessageMethodSymbol(loggerCompilation!, "LogUserAction");
        var callers = await SymbolFinder.FindCallersAsync(methodSymbol!, solution, TestContext.Current.CancellationToken);

        var totalLocations = callers.SelectMany(c => c.Locations).Count();

        // Should find invocations regardless of syntax style
        Assert.True(totalLocations >= 2,
            $"Expected invocations with different syntax styles, found {totalLocations}");
    }

    [Fact]
    public async Task LoggerMessage_SymbolFinderNegativeTest_DoesNotFindWrongMethods()
    {
        // Test: Ensure SymbolFinder doesn't find calls to similarly named methods

        var (solution, loggerProjectId, consumerProjectId) = await CreateCrossProjectWithSimilarMethodNames();

        var loggerProject = solution.GetProject(loggerProjectId)!;
        var loggerCompilation = await loggerProject.GetCompilationAsync(TestContext.Current.CancellationToken);

        var correctMethodSymbol = FindLoggerMessageMethodSymbol(loggerCompilation!, "LogUserAction");
        var callers = await SymbolFinder.FindCallersAsync(correctMethodSymbol!, solution, TestContext.Current.CancellationToken);

        // Should only find calls to the correct method, not similar methods
        var totalLocations = callers.SelectMany(c => c.Locations).Count();

        // This test validates that SymbolFinder is precise in its matching
        Assert.Equal(1, totalLocations); // Only one correct invocation exists in the test setup
    }

    #endregion

    #region Test Helper Methods for SymbolFinder Validation

    private static IMethodSymbol? FindLoggerMessageMethodSymbol(Compilation compilation, string methodName)
    {
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(m => m.Identifier.ValueText == methodName);

            foreach (var methodDecl in methodDeclarations)
            {
                if (semanticModel.GetDeclaredSymbol(methodDecl) is IMethodSymbol methodSymbol
                    && methodSymbol.IsPartialDefinition)
                {
                    // Check if it has LoggerMessage attribute
                    if (methodSymbol.GetAttributes().Any(attr =>
                        attr.AttributeClass?.Name == "LoggerMessageAttribute"))
                    {
                        return methodSymbol;
                    }
                }
            }
        }
        return null;
    }

    private static IMethodSymbol? FindGeneratedLoggerMessageMethod(Compilation compilation, string methodName)
    {
        // Look for the generated implementation of the LoggerMessage method
        var allTypes = compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<INamedTypeSymbol>();

        foreach (var type in allTypes)
        {
            var methods = type.GetMembers(methodName).OfType<IMethodSymbol>();
            foreach (var method in methods)
            {
                // Look for methods with GeneratedCodeAttribute
                if (method.GetAttributes().Any(attr =>
                    attr.AttributeClass?.Name == "GeneratedCodeAttribute"))
                {
                    return method;
                }
            }
        }
        return null;
    }

    private static async Task<(Solution solution, ProjectId loggerProjectId, ProjectId consumerProjectId)> CreateSimpleCrossProjectSolution()
    {
        // Simplified version of existing helper for focused testing
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, default);
        var loggerReference = MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location);
        var baseReferences = references.Add(loggerReference);

        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;

        // Logger project - minimal LoggerMessage
        var loggerCode = @"using Microsoft.Extensions.Logging;

namespace LoggerLib
{
    public static partial class UserLogger
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = ""User {UserId} action"")]
        public static partial void LogUserAction(ILogger logger, int userId);
    }
}

// Generated implementation
namespace LoggerLib
{
    partial class UserLogger
    {
        [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
        public static partial void LogUserAction(ILogger logger, int userId) { }
    }
}";

        // Consumer project - calls the LoggerMessage
        var consumerCode = @"using Microsoft.Extensions.Logging;
using LoggerLib;

namespace Consumer
{
    public class Service
    {
        private readonly ILogger<Service> _logger;
        public Service(ILogger<Service> logger) => _logger = logger;

        public void DoWork() => UserLogger.LogUserAction(_logger, 123);
    }
}";

        // Create projects
        var loggerProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("LoggerLib"), VersionStamp.Create(), "LoggerLib", "LoggerLib",
            LanguageNames.CSharp, compilationOptions: CreateTestCompilationOptions(),
            metadataReferences: baseReferences);
        solution = solution.AddProject(loggerProjectInfo);

        var consumerProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("Consumer"), VersionStamp.Create(), "Consumer", "Consumer",
            LanguageNames.CSharp, compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            metadataReferences: baseReferences, projectReferences: [new ProjectReference(loggerProjectInfo.Id)]);
        solution = solution.AddProject(consumerProjectInfo);

        // Add documents
        solution = solution.AddDocument(DocumentId.CreateNewId(loggerProjectInfo.Id), "Logger.cs", loggerCode);
        solution = solution.AddDocument(DocumentId.CreateNewId(consumerProjectInfo.Id), "Service.cs", consumerCode);

        return (solution, loggerProjectInfo.Id, consumerProjectInfo.Id);
    }

    private static async Task<(Solution solution, ProjectId loggerProjectId, ProjectId consumerProjectId)> CreateCrossProjectWithMultipleInvocations()
    {
        // Consumer project with multiple invocations of the same LoggerMessage method
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, default);
        var loggerReference = MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location);
        var baseReferences = references.Add(loggerReference);

        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;

        var loggerCode = @"using Microsoft.Extensions.Logging;
namespace LoggerLib
{
    public static partial class UserLogger
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = ""User {UserId} action"")]
        public static partial void LogUserAction(ILogger logger, int userId);
    }
}
namespace LoggerLib
{
    partial class UserLogger
    {
        [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
        public static partial void LogUserAction(ILogger logger, int userId) { }
    }
}";

        var consumerCode = @"using Microsoft.Extensions.Logging;
using LoggerLib;

namespace Consumer
{
    public class Service1
    {
        private readonly ILogger<Service1> _logger;
        public Service1(ILogger<Service1> logger) => _logger = logger;
        public void DoWork() => UserLogger.LogUserAction(_logger, 1);
    }

    public class Service2
    {
        private readonly ILogger<Service2> _logger;
        public Service2(ILogger<Service2> logger) => _logger = logger;
        public void DoWork() => UserLogger.LogUserAction(_logger, 2);
        public void DoMoreWork() => UserLogger.LogUserAction(_logger, 3);
    }
}";

        var loggerProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("LoggerLib"), VersionStamp.Create(), "LoggerLib", "LoggerLib",
            LanguageNames.CSharp, compilationOptions: CreateTestCompilationOptions(),
            metadataReferences: baseReferences);
        solution = solution.AddProject(loggerProjectInfo);

        var consumerProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("Consumer"), VersionStamp.Create(), "Consumer", "Consumer",
            LanguageNames.CSharp, compilationOptions: CreateTestCompilationOptions(),
            metadataReferences: baseReferences, projectReferences: [new ProjectReference(loggerProjectInfo.Id)]);
        solution = solution.AddProject(consumerProjectInfo);

        solution = solution.AddDocument(DocumentId.CreateNewId(loggerProjectInfo.Id), "Logger.cs", loggerCode);
        solution = solution.AddDocument(DocumentId.CreateNewId(consumerProjectInfo.Id), "Services.cs", consumerCode);

        return (solution, loggerProjectInfo.Id, consumerProjectInfo.Id);
    }

    private static async Task<(Solution solution, ProjectId loggerProjectId, ProjectId consumer1Id, ProjectId consumer2Id)> CreateCrossProjectWithTwoConsumers()
    {
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, default);
        var loggerReference = MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location);
        var baseReferences = references.Add(loggerReference);

        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;

        var loggerCode = @"using Microsoft.Extensions.Logging;
namespace LoggerLib
{
    public static partial class UserLogger
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = ""User {UserId} action"")]
        public static partial void LogUserAction(ILogger logger, int userId);
    }
}
namespace LoggerLib
{
    partial class UserLogger
    {
        [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
        public static partial void LogUserAction(ILogger logger, int userId) { }
    }
}";

        var consumer1Code = @"using Microsoft.Extensions.Logging;
using LoggerLib;
namespace Consumer1
{
    public class Service1
    {
        private readonly ILogger<Service1> _logger;
        public Service1(ILogger<Service1> logger) => _logger = logger;
        public void DoWork() => UserLogger.LogUserAction(_logger, 100);
    }
}";

        var consumer2Code = @"using Microsoft.Extensions.Logging;
using LoggerLib;
namespace Consumer2
{
    public class Service2
    {
        private readonly ILogger<Service2> _logger;
        public Service2(ILogger<Service2> logger) => _logger = logger;
        public void ProcessData() => UserLogger.LogUserAction(_logger, 200);
    }
}";

        var loggerProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("LoggerLib"), VersionStamp.Create(), "LoggerLib", "LoggerLib",
            LanguageNames.CSharp, compilationOptions: CreateTestCompilationOptions(),
            metadataReferences: baseReferences);
        solution = solution.AddProject(loggerProjectInfo);

        var consumer1ProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("Consumer1"), VersionStamp.Create(), "Consumer1", "Consumer1",
            LanguageNames.CSharp, compilationOptions: CreateTestCompilationOptions(),
            metadataReferences: baseReferences, projectReferences: [new ProjectReference(loggerProjectInfo.Id)]);
        solution = solution.AddProject(consumer1ProjectInfo);

        var consumer2ProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("Consumer2"), VersionStamp.Create(), "Consumer2", "Consumer2",
            LanguageNames.CSharp, compilationOptions: CreateTestCompilationOptions(),
            metadataReferences: baseReferences, projectReferences: [new ProjectReference(loggerProjectInfo.Id)]);
        solution = solution.AddProject(consumer2ProjectInfo);

        solution = solution.AddDocument(DocumentId.CreateNewId(loggerProjectInfo.Id), "Logger.cs", loggerCode);
        solution = solution.AddDocument(DocumentId.CreateNewId(consumer1ProjectInfo.Id), "Service1.cs", consumer1Code);
        solution = solution.AddDocument(DocumentId.CreateNewId(consumer2ProjectInfo.Id), "Service2.cs", consumer2Code);

        return (solution, loggerProjectInfo.Id, consumer1ProjectInfo.Id, consumer2ProjectInfo.Id);
    }

    private static async Task<(Solution solution, ProjectId loggerProjectId, ProjectId consumerProjectId)> CreateCrossProjectWithDifferentInvocationStyles()
    {
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, default);
        var loggerReference = MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location);
        var baseReferences = references.Add(loggerReference);

        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;

        var loggerCode = @"using Microsoft.Extensions.Logging;
namespace LoggerLib
{
    public static partial class UserLogger
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = ""User {UserId} action"")]
        public static partial void LogUserAction(ILogger logger, int userId);
    }
}
namespace LoggerLib
{
    partial class UserLogger
    {
        [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
        public static partial void LogUserAction(ILogger logger, int userId) { }
    }
}";

        var consumerCode = @"using Microsoft.Extensions.Logging;
using LoggerLib;
using static LoggerLib.UserLogger;

namespace Consumer
{
    public class ServiceWithQualified
    {
        private readonly ILogger<ServiceWithQualified> _logger;
        public ServiceWithQualified(ILogger<ServiceWithQualified> logger) => _logger = logger;
        public void DoWork() => UserLogger.LogUserAction(_logger, 1); // Qualified call
    }

    public class ServiceWithStaticUsing
    {
        private readonly ILogger<ServiceWithStaticUsing> _logger;
        public ServiceWithStaticUsing(ILogger<ServiceWithStaticUsing> logger) => _logger = logger;
        public void DoWork() => LogUserAction(_logger, 2); // Static using call
    }
}";

        var loggerProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("LoggerLib"), VersionStamp.Create(), "LoggerLib", "LoggerLib",
            LanguageNames.CSharp, compilationOptions: CreateTestCompilationOptions(),
            metadataReferences: baseReferences);
        solution = solution.AddProject(loggerProjectInfo);

        var consumerProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("Consumer"), VersionStamp.Create(), "Consumer", "Consumer",
            LanguageNames.CSharp, compilationOptions: CreateTestCompilationOptions(),
            metadataReferences: baseReferences, projectReferences: [new ProjectReference(loggerProjectInfo.Id)]);
        solution = solution.AddProject(consumerProjectInfo);

        solution = solution.AddDocument(DocumentId.CreateNewId(loggerProjectInfo.Id), "Logger.cs", loggerCode);
        solution = solution.AddDocument(DocumentId.CreateNewId(consumerProjectInfo.Id), "Services.cs", consumerCode);

        return (solution, loggerProjectInfo.Id, consumerProjectInfo.Id);
    }

    private static async Task<(Solution solution, ProjectId loggerProjectId, ProjectId consumerProjectId)> CreateCrossProjectWithSimilarMethodNames()
    {
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, default);
        var loggerReference = MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location);
        var baseReferences = references.Add(loggerReference);

        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;

        var loggerCode = @"using Microsoft.Extensions.Logging;
namespace LoggerLib
{
    public static partial class UserLogger
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = ""User {UserId} action"")]
        public static partial void LogUserAction(ILogger logger, int userId);
    }
}
namespace LoggerLib
{
    partial class UserLogger
    {
        [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
        public static partial void LogUserAction(ILogger logger, int userId) { }
    }
}";

        var consumerCode = @"using Microsoft.Extensions.Logging;
using LoggerLib;

namespace Consumer
{
    public class Service
    {
        private readonly ILogger<Service> _logger;
        public Service(ILogger<Service> logger) => _logger = logger;

        public void DoWork() => UserLogger.LogUserAction(_logger, 1); // Correct call

        // Similar methods that should NOT be found by SymbolFinder
        public void LogUserAction(string message) { } // Different signature

        public static void SomeOtherLogUserAction() { } // Different method entirely
    }

    public static class FakeLogger
    {
        public static void LogUserAction(ILogger logger, int userId) { } // Different class
    }
}";

        var loggerProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("LoggerLib"), VersionStamp.Create(), "LoggerLib", "LoggerLib",
            LanguageNames.CSharp, compilationOptions: CreateTestCompilationOptions(),
            metadataReferences: baseReferences);
        solution = solution.AddProject(loggerProjectInfo);

        var consumerProjectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("Consumer"), VersionStamp.Create(), "Consumer", "Consumer",
            LanguageNames.CSharp, compilationOptions: CreateTestCompilationOptions(),
            metadataReferences: baseReferences, projectReferences: [new ProjectReference(loggerProjectInfo.Id)]);
        solution = solution.AddProject(consumerProjectInfo);

        solution = solution.AddDocument(DocumentId.CreateNewId(loggerProjectInfo.Id), "Logger.cs", loggerCode);
        solution = solution.AddDocument(DocumentId.CreateNewId(consumerProjectInfo.Id), "Services.cs", consumerCode);

        return (solution, loggerProjectInfo.Id, consumerProjectInfo.Id);
    }

    #endregion
}
