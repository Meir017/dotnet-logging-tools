using FluentAssertions;
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
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().ContainSingle()
            .Which.MethodType.Should().Be(LoggerUsageMethodType.LoggerMessageAttribute);
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
        loggerUsages.Should().NotBeNull();
        var usage = loggerUsages.Results.Should().ContainSingle().Which;
        if (expectedId == null && expectedName == null)
        {
            usage.EventId.Should().BeNull();
            return;
        }

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
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().ContainSingle()
            .Which.LogLevel.Should().Be(expectedLogLevel);
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
        loggerUsages.Should().NotBeNull();
        var result = loggerUsages.Results.Should().ContainSingle().Which;
        if (expectedMessage == null)
        {
            string.IsNullOrEmpty(result.MessageTemplate).Should().BeTrue();
        }
        else
        {
            result.MessageTemplate.Should().Be(expectedMessage);
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
        loggerUsages.Should().NotBeNull();
        var usage = loggerUsages.Results.Should().ContainSingle().Which;
        if (expectedParameters.Count == 0)
        {
            usage.MessageParameters.Should().BeEmpty();
        }
        else
        {
            usage.MessageParameters.Should().HaveCount(expectedParameters.Count);
            usage.MessageParameters.Should().Equal(expectedParameters);
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
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.MethodName.Should().Be("LogUserLogin");
        usage.MethodType.Should().Be(LoggerUsageMethodType.LoggerMessageAttribute);
        usage.DeclaringTypeName.Should().Be("TestNamespace.Log");
        usage.HasInvocations.Should().BeTrue();
        usage.InvocationCount.Should().Be(1);

        var invocation = usage.Invocations.Should().ContainSingle().Which;
        invocation.ContainingType.Should().Be("TestNamespace.UserService");
        invocation.InvocationLocation.Should().NotBeNull();
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
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.MethodName.Should().Be("LogUserLogin");
        usage.HasInvocations.Should().BeTrue();
        usage.InvocationCount.Should().Be(3);

        // Verify invocations from different containing types
        var userServiceInvocations = usage.Invocations.Where(i => i.ContainingType == "TestNamespace.UserService").ToList();
        var adminServiceInvocations = usage.Invocations.Where(i => i.ContainingType == "TestNamespace.AdminService").ToList();

        userServiceInvocations.Should().HaveCount(2);
        adminServiceInvocations.Should().ContainSingle();
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
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().HaveCount(2); // LoggerMessage + regular logger call

        var loggerMessageUsage = loggerUsages.Results
            .OfType<LoggerMessageUsageInfo>()
            .Single(u => u.MethodName == "UnusedMethod");

        loggerMessageUsage.HasInvocations.Should().BeFalse();
        loggerMessageUsage.InvocationCount.Should().Be(0);
        loggerMessageUsage.Invocations.Should().BeEmpty();
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
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.HasInvocations.Should().BeTrue();

        var invocation = usage.Invocations.Should().ContainSingle().Which;
        invocation.Arguments.Should().HaveCount(3); // logger, userId, action

        // Verify argument details
        var loggerArg = invocation.Arguments.FirstOrDefault(a => a.Name == "logger");
        var userIdArg = invocation.Arguments.FirstOrDefault(a => a.Name == "userId");
        var actionArg = invocation.Arguments.FirstOrDefault(a => a.Name == "action");

        loggerArg.Should().NotBeNull();
        userIdArg.Should().NotBeNull();
        actionArg.Should().NotBeNull();

        userIdArg!.Type.Should().Be("int");
        actionArg!.Type.Should().Be("string");
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
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().HaveCount(2);

        var loginUsage = loggerUsages.Results.OfType<LoggerMessageUsageInfo>()
            .Single(u => u.MethodName == "LogUserLogin");
        var loginFailedUsage = loggerUsages.Results.OfType<LoggerMessageUsageInfo>()
            .Single(u => u.MethodName == "LogUserLoginFailed");

        loginUsage.InvocationCount.Should().Be(2);
        loginFailedUsage.InvocationCount.Should().Be(1);
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
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.MethodName.Should().Be("LogApplicationStarted");
        usage.DeclaringTypeName.Should().Be("TestNamespace.Logging.ApplicationLog");
        usage.HasInvocations.Should().BeTrue();

        var invocation = usage.Invocations.Should().ContainSingle().Which;
        invocation.ContainingType.Should().Be("TestNamespace.Services.StartupService");
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
        resultsWithSolution.Should().NotBeNull();
        resultsWithSolution.Results.Should().ContainSingle();
        resultsWithoutSolution.Should().NotBeNull();
        resultsWithoutSolution.Results.Should().ContainSingle();

        var usageWithSolution = resultsWithSolution.Results[0].Should().BeOfType<LoggerMessageUsageInfo>().Which;
        var usageWithoutSolution = resultsWithoutSolution.Results[0].Should().BeOfType<LoggerMessageUsageInfo>().Which;

        // Without solution: Should find 0 invocations (no local invocations in logger project)
        usageWithoutSolution.HasInvocations.Should().BeFalse("Without solution context, should not find cross-project invocations");
        usageWithoutSolution.InvocationCount.Should().Be(0);

        // With solution: Should find cross-project invocations using SymbolFinder.FindCallersAsync
        usageWithSolution.HasInvocations.Should().BeTrue($"With solution context, should find cross-project invocations but found {usageWithSolution.InvocationCount}");
        usageWithSolution.InvocationCount.Should().Be(2); // Should find 2 invocations from consumer project

        // Verify invocations are from the consumer project
        usageWithSolution.Invocations.Should().AllSatisfy(invocation =>
            invocation.ContainingType.Should().Contain("ConsumerProject"));
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
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.MethodName.Should().Be("LogUserActivity");
        usage.DeclaringTypeName.Should().Be("LoggerProject.UserLogger");

        usage.HasInvocations.Should().BeTrue($"Expected invocations to be found, but got {usage.InvocationCount} invocations");
        usage.InvocationCount.Should().Be(2); // Should find invocations in consumer project

        // Verify invocations from consumer project
        var consumerInvocations = usage.Invocations.Where(i => i.ContainingType.Contains("ConsumerProject")).ToList();
        consumerInvocations.Should().HaveCount(2);

        // Should find invocations in both services
        usage.Invocations.Should().Contain(i => i.ContainingType == "ConsumerProject.Services.UserService");
        usage.Invocations.Should().Contain(i => i.ContainingType == "ConsumerProject.Services.ActivityService");
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
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.MethodName.Should().Be("LogUserActivity");
        usage.DeclaringTypeName.Should().Be("LoggerProject.UserLogger");
        usage.HasInvocations.Should().BeFalse(); // Should not find cross-project invocations
        usage.InvocationCount.Should().Be(0);
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

        loggerErrors.Should().BeEmpty();
        consumerErrors.Should().BeEmpty();

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
        partialMethodSymbol.Should().NotBeNull();
        partialMethodSymbol!.IsPartialDefinition.Should().BeTrue();

        // Test 1: Search for callers using the partial method symbol
        var callersFromPartial = await SymbolFinder.FindCallersAsync(partialMethodSymbol, solution, TestContext.Current.CancellationToken);

        // Find the generated implementation (if it exists)
        var generatedMethodSymbol = FindGeneratedLoggerMessageMethod(loggerCompilation!, "LogUserAction");
        if (generatedMethodSymbol != null)
        {
            // Test 2: Search for callers using the generated method symbol
            var callersFromGenerated = await SymbolFinder.FindCallersAsync(generatedMethodSymbol, solution, TestContext.Current.CancellationToken);

            // Analysis: Which symbol finds the actual invocations?
            (callersFromPartial.Any() || callersFromGenerated.Any()).Should().BeTrue(
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
        totalLocations.Should().BeGreaterOrEqualTo(2, $"Expected at least 2 invocations, found {totalLocations}");
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
        callersByProject.Should().HaveCountGreaterOrEqualTo(2,
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
        totalLocations.Should().BeGreaterOrEqualTo(2,
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
        totalLocations.Should().Be(1); // Only one correct invocation exists in the test setup
    }

    #endregion

    #region LogProperties Tests

    [Fact]
    public async Task LoggerMessage_WithBasicLogProperties_ExtractsPropertiesCorrectly()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;


namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User logged in""
    )]
    public static partial void LogUserLogin(ILogger logger, [LogProperties] UserInfo user);
}

public class UserInfo
{
    public int UserId { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
}" + CreateMockGeneratedCode("Log", "LogUserLogin(ILogger logger, UserInfo user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.MethodName.Should().Be("LogUserLogin");
        usage.HasLogProperties.Should().BeTrue();
        usage.LogPropertiesParameters.Should().ContainSingle();

        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.ParameterName.Should().Be("user");
        logPropsParam.ParameterType.Should().Be("UserInfo");
        logPropsParam.Properties.Should().HaveCount(3);
        usage.TotalLogPropertiesCount.Should().Be(3);

        // Verify individual properties
        logPropsParam.Properties.Should().Contain(p => p.Name == "UserId" && p.Type == "int");
        logPropsParam.Properties.Should().Contain(p => p.Name == "UserName" && p.Type == "string");
        logPropsParam.Properties.Should().Contain(p => p.Name == "Email" && p.Type == "string");
    }

    [Fact]
    public async Task LoggerMessage_WithMultipleLogPropertiesParameters_ExtractsAllParameters()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;


namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = ""Order processing failed""
    )]
    public static partial void LogOrderFailure(ILogger logger, [LogProperties] UserInfo user, [LogProperties] OrderInfo order);
}

public class UserInfo
{
    public int UserId { get; set; }
    public string UserName { get; set; }
}

public class OrderInfo
{
    public int OrderId { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; }
}" + CreateMockGeneratedCode("Log", "LogOrderFailure(ILogger logger, UserInfo user, OrderInfo order)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.HasLogProperties.Should().BeTrue();
        usage.LogPropertiesParameters.Should().HaveCount(2);
        usage.TotalLogPropertiesCount.Should().Be(5); // 2 user + 3 order properties

        var userParam = usage.LogPropertiesParameters.FirstOrDefault(p => p.ParameterName == "user");
        var orderParam = usage.LogPropertiesParameters.FirstOrDefault(p => p.ParameterName == "order");

        userParam.Should().NotBeNull();
        orderParam.Should().NotBeNull();
        userParam!.Properties.Should().HaveCount(2);
        orderParam!.Properties.Should().HaveCount(3);
    }

    [Fact]
    public async Task LoggerMessage_WithLogPropertiesConfiguration_ExtractsConfiguration()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;


namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = ""Configuration test""
    )]
    public static partial void LogWithConfig(
        ILogger logger,
        [LogProperties(OmitReferenceName = true, SkipNullProperties = true)] UserInfo user);
}

public class UserInfo
{
    public int UserId { get; set; }
    public string UserName { get; set; }
}" + CreateMockGeneratedCode("Log", "LogWithConfig(ILogger logger, UserInfo user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.HasLogProperties.Should().BeTrue();
        usage.LogPropertiesParameters.Should().ContainSingle();

        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.Configuration.OmitReferenceName.Should().BeTrue();
        logPropsParam.Configuration.SkipNullProperties.Should().BeTrue();
        logPropsParam.Configuration.Transitive.Should().BeFalse(); // Default value
    }

    [Fact]
    public async Task LoggerMessage_WithLogPropertiesAndRegularParameters_BothAreExtracted()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;
using System;

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = ""User {userId} performed {action}""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        int userId,
        string action,
        [LogProperties] UserInfo user);
}

public class UserInfo
{
    public string Email { get; set; }
    public DateTime LastLogin { get; set; }
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, int userId, string action, UserInfo user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;

        // Regular message parameters should still be extracted
        usage.MessageParameters.Should().HaveCount(2);
        usage.MessageParameters.Should().Contain(p => p.Name == "userId" && p.Type == "int");
        usage.MessageParameters.Should().Contain(p => p.Name == "action" && p.Type == "string");

        // LogProperties should also be extracted
        usage.HasLogProperties.Should().BeTrue();
        usage.LogPropertiesParameters.Should().ContainSingle();
        usage.TotalLogPropertiesCount.Should().Be(2);

        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.ParameterName.Should().Be("user");
        logPropsParam.Properties.Should().Contain(p => p.Name == "Email" && p.Type == "string");
        logPropsParam.Properties.Should().Contain(p => p.Name == "LastLogin" && p.Type == "DateTime");
    }

    [Fact]
    public async Task LoggerMessage_WithEmptyClass_HandlesNoProperties()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;


namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Debug,
        Message = ""Empty class test""
    )]
    public static partial void LogEmptyClass(ILogger logger, [LogProperties] EmptyClass empty);
}

public class EmptyClass
{
    // No public properties
    private int privateField;
    internal string InternalProperty { get; set; }
}" + CreateMockGeneratedCode("Log", "LogEmptyClass(ILogger logger, EmptyClass empty)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.HasLogProperties.Should().BeTrue(); // Has LogProperties parameter
        usage.LogPropertiesParameters.Should().ContainSingle();
        usage.TotalLogPropertiesCount.Should().Be(0); // But no actual properties

        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.ParameterName.Should().Be("empty");
        logPropsParam.Properties.Should().BeEmpty(); // No public properties to extract
    }

    [Fact]
    public async Task LoggerMessage_WithNullableProperties_CorrectlyIdentifiesNullability()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;


namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = ""Nullable test""
    )]
    public static partial void LogNullableTest(ILogger logger, [LogProperties] NullableClass data);
}

public class NullableClass
{
    public int NonNullableInt { get; set; }
    public int? NullableInt { get; set; }
    public string NonNullableString { get; set; }
    public string? NullableString { get; set; }
}" + CreateMockGeneratedCode("Log", "LogNullableTest(ILogger logger, NullableClass data)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.HasLogProperties.Should().BeTrue();
        usage.TotalLogPropertiesCount.Should().Be(4);

        var properties = usage.LogPropertiesParameters[0].Properties;

        var nonNullableInt = properties.FirstOrDefault(p => p.Name == "NonNullableInt");
        var nullableInt = properties.FirstOrDefault(p => p.Name == "NullableInt");
        var nonNullableString = properties.FirstOrDefault(p => p.Name == "NonNullableString");
        var nullableString = properties.FirstOrDefault(p => p.Name == "NullableString");

        nonNullableInt.Should().NotBeNull();
        nullableInt.Should().NotBeNull();
        nonNullableString.Should().NotBeNull();
        nullableString.Should().NotBeNull();

        nonNullableInt!.IsNullable.Should().BeFalse();
        nullableInt!.IsNullable.Should().BeTrue();
        nonNullableString!.IsNullable.Should().BeFalse(); // Non-nullable reference type
        nullableString!.IsNullable.Should().BeTrue();     // Nullable reference type
    }

    [Fact]
    public async Task LoggerMessage_WithoutLogProperties_HasLogPropertiesReturnsFalse()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = ""No LogProperties here""
    )]
    public static partial void LogWithoutProperties(ILogger logger, int userId, string action);
}" + CreateMockGeneratedCode("Log", "LogWithoutProperties(ILogger logger, int userId, string action)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.HasLogProperties.Should().BeFalse();
        usage.LogPropertiesParameters.Should().BeEmpty();
        usage.TotalLogPropertiesCount.Should().Be(0);

        // No message template parameters since template has no placeholders
        usage.MessageParameters.Should().BeEmpty();
    }

    public static TheoryData<string, bool, bool, bool> LogPropertiesConfigurationScenarios() => new()
    {
        { "", false, false, false }, // Default values
        { "OmitReferenceName = true", true, false, false },
        { "SkipNullProperties = true", false, true, false },
        { "Transitive = true", false, false, true },
        { "OmitReferenceName = true, SkipNullProperties = true", true, true, false },
        { "OmitReferenceName = true, Transitive = true", true, false, true },
        { "SkipNullProperties = true, Transitive = true", false, true, true },
        { "OmitReferenceName = true, SkipNullProperties = true, Transitive = true", true, true, true },
    };

    [Theory]
    [MemberData(nameof(LogPropertiesConfigurationScenarios))]
    public async Task LoggerMessage_LogPropertiesConfiguration_Scenarios(string configArgs, bool expectedOmitRef, bool expectedSkipNull, bool expectedTransitive)
    {
        // Arrange
        var code = $@"using Microsoft.Extensions.Logging;


namespace TestNamespace;

public static partial class Log
{{
    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Information,
        Message = ""Config test""
    )]
    public static partial void LogConfigTest(ILogger logger, [LogProperties({configArgs})] UserInfo user);
}}

public class UserInfo
{{
    public int UserId {{ get; set; }}
}}" + CreateMockGeneratedCode("Log", "LogConfigTest(ILogger logger, UserInfo user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.HasLogProperties.Should().BeTrue();

        var config = usage.LogPropertiesParameters[0].Configuration;
        config.OmitReferenceName.Should().Be(expectedOmitRef);
        config.SkipNullProperties.Should().Be(expectedSkipNull);
        config.Transitive.Should().Be(expectedTransitive);
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

    #region Transitive LogProperties Tests

    [Fact]
    public async Task LoggerMessage_WithTransitiveLogProperties_ExtractsNestedProperties()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User details logged""
    )]
    public static partial void LogUserDetails(ILogger logger, [LogProperties(Transitive = true)] UserDetails user);
}

public class UserDetails
{
    public string Name { get; set; }
    public int Age { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string ZipCode { get; set; }
}" + CreateMockGeneratedCode("Log", "LogUserDetails(ILogger logger, UserDetails user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.HasLogProperties.Should().BeTrue();
        usage.LogPropertiesParameters.Should().ContainSingle();

        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.Configuration.Transitive.Should().BeTrue();
        logPropsParam.Properties.Should().HaveCount(3);

        // Verify top-level properties
        logPropsParam.Properties.Should().Contain(p => p.Name == "Name" && p.Type == "string");
        logPropsParam.Properties.Should().Contain(p => p.Name == "Age" && p.Type == "int");
        
        // Verify nested Address property
        var addressProp = logPropsParam.Properties.FirstOrDefault(p => p.Name == "Address");
        addressProp.Should().NotBeNull();
        addressProp!.Type.Should().Be("Address");
        addressProp.NestedProperties.Should().NotBeNull();
        addressProp.NestedProperties.Should().HaveCount(3);
        
        // Verify nested Address properties
        addressProp.NestedProperties.Should().Contain(p => p.Name == "Street" && p.Type == "string");
        addressProp.NestedProperties.Should().Contain(p => p.Name == "City" && p.Type == "string");
        addressProp.NestedProperties.Should().Contain(p => p.Name == "ZipCode" && p.Type == "string");
    }

    [Fact]
    public async Task LoggerMessage_WithTransitiveFalse_DoesNotExtractNestedProperties()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User details logged""
    )]
    public static partial void LogUserDetails(ILogger logger, [LogProperties(Transitive = false)] UserDetails user);
}

public class UserDetails
{
    public string Name { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}" + CreateMockGeneratedCode("Log", "LogUserDetails(ILogger logger, UserDetails user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.HasLogProperties.Should().BeTrue();
        usage.LogPropertiesParameters.Should().ContainSingle();

        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.Configuration.Transitive.Should().BeFalse();
        logPropsParam.Properties.Should().HaveCount(2);

        // Verify Address property has no nested properties
        var addressProp = logPropsParam.Properties.FirstOrDefault(p => p.Name == "Address");
        addressProp.Should().NotBeNull();
        addressProp!.NestedProperties.Should().BeNull();
    }

    [Fact]
    public async Task LoggerMessage_WithMultiLevelNesting_ExtractsAllLevels()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Organization logged""
    )]
    public static partial void LogOrganization(ILogger logger, [LogProperties(Transitive = true)] Organization org);
}

public class Organization
{
    public string Name { get; set; }
    public Department Department { get; set; }
}

public class Department
{
    public string DepartmentName { get; set; }
    public Employee Manager { get; set; }
}

public class Employee
{
    public string EmployeeName { get; set; }
    public int EmployeeId { get; set; }
}" + CreateMockGeneratedCode("Log", "LogOrganization(ILogger logger, Organization org)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.Configuration.Transitive.Should().BeTrue();

        // Level 1: Organization
        logPropsParam.Properties.Should().HaveCount(2);
        var deptProp = logPropsParam.Properties.FirstOrDefault(p => p.Name == "Department");
        deptProp.Should().NotBeNull();
        deptProp!.NestedProperties.Should().NotBeNull();

        // Level 2: Department
        deptProp.NestedProperties.Should().HaveCount(2);
        var managerProp = deptProp.NestedProperties.FirstOrDefault(p => p.Name == "Manager");
        managerProp.Should().NotBeNull();
        managerProp!.NestedProperties.Should().NotBeNull();

        // Level 3: Employee
        managerProp.NestedProperties.Should().HaveCount(2);
        managerProp.NestedProperties.Should().Contain(p => p.Name == "EmployeeName" && p.Type == "string");
        managerProp.NestedProperties.Should().Contain(p => p.Name == "EmployeeId" && p.Type == "int");
    }

    [Fact]
    public async Task LoggerMessage_WithCircularReference_PreventInfiniteLoop()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Node logged""
    )]
    public static partial void LogNode(ILogger logger, [LogProperties(Transitive = true)] Node node);
}

public class Node
{
    public string Name { get; set; }
    public Node Parent { get; set; }
    public Node Child { get; set; }
}" + CreateMockGeneratedCode("Log", "LogNode(ILogger logger, Node node)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert - Should complete without infinite loop
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.Configuration.Transitive.Should().BeTrue();
        
        // Should have 3 properties: Name, Parent, Child
        logPropsParam.Properties.Should().HaveCount(3);
        
        // Parent and Child should not have nested properties (circular reference detected)
        var parentProp = logPropsParam.Properties.FirstOrDefault(p => p.Name == "Parent");
        var childProp = logPropsParam.Properties.FirstOrDefault(p => p.Name == "Child");
        parentProp.Should().NotBeNull();
        childProp.Should().NotBeNull();
        
        // The circular reference detection should prevent nested properties
        parentProp!.NestedProperties.Should().BeNull();
        childProp!.NestedProperties.Should().BeNull();
    }

    [Fact]
    public async Task LoggerMessage_WithArrayCollection_ExtractsElementType()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Team logged""
    )]
    public static partial void LogTeam(ILogger logger, [LogProperties(Transitive = true)] Team team);
}

public class Team
{
    public string TeamName { get; set; }
    public Member[] Members { get; set; }
}

public class Member
{
    public string Name { get; set; }
    public string Role { get; set; }
}" + CreateMockGeneratedCode("Log", "LogTeam(ILogger logger, Team team)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        var logPropsParam = usage.LogPropertiesParameters[0];
        
        logPropsParam.Properties.Should().HaveCount(2);
        
        var membersProp = logPropsParam.Properties.FirstOrDefault(p => p.Name == "Members");
        membersProp.Should().NotBeNull();
        membersProp!.Type.Should().Be("Member[]");
        
        // Should extract properties from the element type
        membersProp.NestedProperties.Should().NotBeNull();
        membersProp.NestedProperties.Should().HaveCount(2);
        membersProp.NestedProperties.Should().Contain(p => p.Name == "Name" && p.Type == "string");
        membersProp.NestedProperties.Should().Contain(p => p.Name == "Role" && p.Type == "string");
    }

    [Fact]
    public async Task LoggerMessage_WithListCollection_ExtractsElementType()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Team logged""
    )]
    public static partial void LogTeam(ILogger logger, [LogProperties(Transitive = true)] Team team);
}

public class Team
{
    public string TeamName { get; set; }
    public List<Member> Members { get; set; }
}

public class Member
{
    public string Name { get; set; }
    public int MemberId { get; set; }
}" + CreateMockGeneratedCode("Log", "LogTeam(ILogger logger, Team team)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        var logPropsParam = usage.LogPropertiesParameters[0];
        
        var membersProp = logPropsParam.Properties.FirstOrDefault(p => p.Name == "Members");
        membersProp.Should().NotBeNull();
        membersProp!.Type.Should().Be("List");
        
        // Should extract properties from the element type
        membersProp.NestedProperties.Should().NotBeNull();
        membersProp.NestedProperties.Should().HaveCount(2);
        membersProp.NestedProperties.Should().Contain(p => p.Name == "Name" && p.Type == "string");
        membersProp.NestedProperties.Should().Contain(p => p.Name == "MemberId" && p.Type == "int");
    }

    [Fact]
    public async Task LoggerMessage_WithIEnumerableCollection_ExtractsElementType()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Items logged""
    )]
    public static partial void LogItems(ILogger logger, [LogProperties(Transitive = true)] Container container);
}

public class Container
{
    public string ContainerName { get; set; }
    public IEnumerable<Item> Items { get; set; }
}

public class Item
{
    public string ItemName { get; set; }
    public decimal Price { get; set; }
}" + CreateMockGeneratedCode("Log", "LogItems(ILogger logger, Container container)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        var logPropsParam = usage.LogPropertiesParameters[0];
        
        var itemsProp = logPropsParam.Properties.FirstOrDefault(p => p.Name == "Items");
        itemsProp.Should().NotBeNull();
        itemsProp!.Type.Should().Be("IEnumerable");
        
        // Should extract properties from the element type
        itemsProp.NestedProperties.Should().NotBeNull();
        itemsProp.NestedProperties.Should().HaveCount(2);
        itemsProp.NestedProperties.Should().Contain(p => p.Name == "ItemName" && p.Type == "string");
        itemsProp.NestedProperties.Should().Contain(p => p.Name == "Price" && p.Type == "decimal");
    }

    [Fact]
    public async Task LoggerMessage_WithInterfaceProperty_HandledGracefully()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Service logged""
    )]
    public static partial void LogService(ILogger logger, [LogProperties(Transitive = true)] Service service);
}

public class Service
{
    public string ServiceName { get; set; }
    public IConfiguration Config { get; set; }
}

public interface IConfiguration
{
    string Setting { get; set; }
}" + CreateMockGeneratedCode("Log", "LogService(ILogger logger, Service service)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        var logPropsParam = usage.LogPropertiesParameters[0];
        
        logPropsParam.Properties.Should().HaveCount(2);
        
        var configProp = logPropsParam.Properties.FirstOrDefault(p => p.Name == "Config");
        configProp.Should().NotBeNull();
        configProp!.Type.Should().Be("IConfiguration");
        
        // Interface properties should have nested properties extracted
        configProp.NestedProperties.Should().NotBeNull();
        configProp.NestedProperties.Should().ContainSingle();
        configProp.NestedProperties.Should().Contain(p => p.Name == "Setting" && p.Type == "string");
    }

    [Fact]
    public async Task LoggerMessage_WithAbstractTypeProperty_HandledGracefully()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Entity logged""
    )]
    public static partial void LogEntity(ILogger logger, [LogProperties(Transitive = true)] Container container);
}

public class Container
{
    public string ContainerName { get; set; }
    public BaseEntity Entity { get; set; }
}

public abstract class BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
}" + CreateMockGeneratedCode("Log", "LogEntity(ILogger logger, Container container)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        var logPropsParam = usage.LogPropertiesParameters[0];
        
        logPropsParam.Properties.Should().HaveCount(2);
        
        var entityProp = logPropsParam.Properties.FirstOrDefault(p => p.Name == "Entity");
        entityProp.Should().NotBeNull();
        entityProp!.Type.Should().Be("BaseEntity");
        
        // Abstract type properties should have nested properties extracted
        entityProp.NestedProperties.Should().NotBeNull();
        entityProp.NestedProperties.Should().HaveCount(2);
        entityProp.NestedProperties.Should().Contain(p => p.Name == "Id" && p.Type == "int");
        entityProp.NestedProperties.Should().Contain(p => p.Name == "Name" && p.Type == "string");
    }

    [Fact]
    public async Task LoggerMessage_WithPrimitiveCollections_NoNestedProperties()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Data logged""
    )]
    public static partial void LogData(ILogger logger, [LogProperties(Transitive = true)] DataContainer data);
}

public class DataContainer
{
    public string Name { get; set; }
    public List<string> Tags { get; set; }
    public int[] Numbers { get; set; }
}" + CreateMockGeneratedCode("Log", "LogData(ILogger logger, DataContainer data)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        var logPropsParam = usage.LogPropertiesParameters[0];
        
        logPropsParam.Properties.Should().HaveCount(3);
        
        // Primitive collections should not have nested properties
        var tagsProp = logPropsParam.Properties.FirstOrDefault(p => p.Name == "Tags");
        var numbersProp = logPropsParam.Properties.FirstOrDefault(p => p.Name == "Numbers");
        
        tagsProp.Should().NotBeNull();
        numbersProp.Should().NotBeNull();
        tagsProp!.NestedProperties.Should().BeNull();
        numbersProp!.NestedProperties.Should().BeNull();
    }

    #endregion

    #region TagName Attribute Tests

    [Fact]
    public async Task LoggerMessage_ParameterWithTagName_ExtractsCustomTagName()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User logged in: {userName}""
    )]
    public static partial void LogUserLogin(
        ILogger logger,
        [TagName(""user.name"")] string userName);
}" + CreateMockGeneratedCode("Log", "LogUserLogin(ILogger logger, string userName)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.MethodName.Should().Be("LogUserLogin");
        usage.MessageParameters.Should().ContainSingle();

        var parameter = usage.MessageParameters[0];
        parameter.Name.Should().Be("userName");
        parameter.Type.Should().Be("string");
        parameter.CustomTagName.Should().Be("user.name");
    }

    [Fact]
    public async Task LoggerMessage_MultipleParametersWithTagName_ExtractsAllCustomTagNames()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Request: {requestId}, User: {userId}""
    )]
    public static partial void LogRequest(
        ILogger logger,
        [TagName(""request.id"")] string requestId,
        [TagName(""user.id"")] int userId);
}" + CreateMockGeneratedCode("Log", "LogRequest(ILogger logger, string requestId, int userId)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.MethodName.Should().Be("LogRequest");
        usage.MessageParameters.Should().HaveCount(2);

        var requestIdParam = usage.MessageParameters[0];
        requestIdParam.Name.Should().Be("requestId");
        requestIdParam.CustomTagName.Should().Be("request.id");

        var userIdParam = usage.MessageParameters[1];
        userIdParam.Name.Should().Be("userId");
        userIdParam.CustomTagName.Should().Be("user.id");
    }

    [Fact]
    public async Task LoggerMessage_MixedParametersWithAndWithoutTagName_ExtractsCorrectly()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Request: {requestId}, Status: {status}""
    )]
    public static partial void LogRequest(
        ILogger logger,
        [TagName(""request.id"")] string requestId,
        string status);
}" + CreateMockGeneratedCode("Log", "LogRequest(ILogger logger, string requestId, string status)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.MessageParameters.Should().HaveCount(2);

        var requestIdParam = usage.MessageParameters[0];
        requestIdParam.Name.Should().Be("requestId");
        requestIdParam.CustomTagName.Should().Be("request.id");

        var statusParam = usage.MessageParameters[1];
        statusParam.Name.Should().Be("status");
        statusParam.CustomTagName.Should().BeNull(); // No TagName attribute
    }

    [Fact]
    public async Task LoggerMessage_PropertyWithTagName_ExtractsCustomTagName()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class UserInfo
{
    [TagName(""user.id"")]
    public string UserId { get; set; }
    
    [TagName(""user.display_name"")]
    public string DisplayName { get; set; }
    
    public string Email { get; set; }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User logged in""
    )]
    public static partial void LogUser(
        ILogger logger,
        [LogProperties] UserInfo user);
}" + CreateMockGeneratedCode("Log", "LogUser(ILogger logger, UserInfo user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.MethodName.Should().Be("LogUser");
        usage.LogPropertiesParameters.Should().ContainSingle();

        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.ParameterName.Should().Be("user");
        logPropsParam.Properties.Should().HaveCount(3);

        // Verify properties with TagName
        var userIdProp = logPropsParam.Properties.FirstOrDefault(p => p.OriginalName == "UserId");
        userIdProp.Should().NotBeNull();
        userIdProp!.CustomTagName.Should().Be("user.id");

        var displayNameProp = logPropsParam.Properties.FirstOrDefault(p => p.OriginalName == "DisplayName");
        displayNameProp.Should().NotBeNull();
        displayNameProp!.CustomTagName.Should().Be("user.display_name");

        // Verify property without TagName
        var emailProp = logPropsParam.Properties.FirstOrDefault(p => p.OriginalName == "Email");
        emailProp.Should().NotBeNull();
        emailProp!.CustomTagName.Should().BeNull();
    }

    [Fact]
    public async Task LoggerMessage_NestedPropertiesWithTagName_ExtractsCustomTagNames()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class Address
{
    [TagName(""address.street"")]
    public string Street { get; set; }
    
    public string City { get; set; }
}

public class UserInfo
{
    [TagName(""user.id"")]
    public string UserId { get; set; }
    
    public Address Address { get; set; }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User details""
    )]
    public static partial void LogUser(
        ILogger logger,
        [LogProperties(Transitive = true)] UserInfo user);
}" + CreateMockGeneratedCode("Log", "LogUser(ILogger logger, UserInfo user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.LogPropertiesParameters.Should().ContainSingle();

        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.Properties.Should().HaveCount(2);

        // Verify top-level property with TagName
        var userIdProp = logPropsParam.Properties.FirstOrDefault(p => p.OriginalName == "UserId");
        userIdProp.Should().NotBeNull();
        userIdProp!.CustomTagName.Should().Be("user.id");

        // Verify nested property with TagName
        var addressProp = logPropsParam.Properties.FirstOrDefault(p => p.OriginalName == "Address");
        addressProp.Should().NotBeNull();
        addressProp!.NestedProperties.Should().NotBeNull();
        addressProp.NestedProperties.Should().HaveCount(2);

        var streetProp = addressProp.NestedProperties.FirstOrDefault(p => p.OriginalName == "Street");
        streetProp.Should().NotBeNull();
        streetProp!.CustomTagName.Should().Be("address.street");

        var cityProp = addressProp.NestedProperties.FirstOrDefault(p => p.OriginalName == "City");
        cityProp.Should().NotBeNull();
        cityProp!.CustomTagName.Should().BeNull();
    }

    [Fact]
    public async Task LoggerMessage_CombinedParameterAndPropertyTagName_ExtractsBoth()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class RequestDetails
{
    [TagName(""request.method"")]
    public string Method { get; set; }
    
    [TagName(""request.path"")]
    public string Path { get; set; }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Request processed: {requestId}""
    )]
    public static partial void LogRequest(
        ILogger logger,
        [TagName(""request.id"")] string requestId,
        [LogProperties] RequestDetails details);
}" + CreateMockGeneratedCode("Log", "LogRequest(ILogger logger, string requestId, RequestDetails details)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        
        // Verify parameter with TagName
        usage.MessageParameters.Should().ContainSingle();
        var requestIdParam = usage.MessageParameters[0];
        requestIdParam.Name.Should().Be("requestId");
        requestIdParam.CustomTagName.Should().Be("request.id");

        // Verify properties with TagName
        usage.LogPropertiesParameters.Should().ContainSingle();
        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.Properties.Should().HaveCount(2);

        var methodProp = logPropsParam.Properties.FirstOrDefault(p => p.OriginalName == "Method");
        methodProp.Should().NotBeNull();
        methodProp!.CustomTagName.Should().Be("request.method");

        var pathProp = logPropsParam.Properties.FirstOrDefault(p => p.OriginalName == "Path");
        pathProp.Should().NotBeNull();
        pathProp!.CustomTagName.Should().Be("request.path");
    }

    [Fact]
    public async Task LoggerMessage_TagNameWithSpecialCharacters_ExtractsCorrectly()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Event: {eventType}""
    )]
    public static partial void LogEvent(
        ILogger logger,
        [TagName(""event.type-category"")] string eventType);
}" + CreateMockGeneratedCode("Log", "LogEvent(ILogger logger, string eventType)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.MessageParameters.Should().ContainSingle();

        var parameter = usage.MessageParameters[0];
        parameter.Name.Should().Be("eventType");
        parameter.CustomTagName.Should().Be("event.type-category");
    }

    #endregion

    #region TagProvider Tests

    [Fact]
    public async Task LoggerMessage_TagProvider_BasicProvider_ExtractsCorrectly()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
    public string Email { get; set; }
}

public static class UserTagProvider
{
    public static void AddTags(ITagCollector collector, User user)
    {
        collector.Add(""user.name"", user.Name);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), nameof(UserTagProvider.AddTags))]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.LogPropertiesParameters.Should().ContainSingle();

        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.ParameterName.Should().Be("user");
        logPropsParam.TagProvider.Should().NotBeNull();
        
        var tagProvider = logPropsParam.TagProvider!;
        tagProvider.ParameterName.Should().Be("user");
        tagProvider.ProviderTypeName.Should().Be("TestNamespace.UserTagProvider");
        tagProvider.ProviderMethodName.Should().Be("AddTags");
        tagProvider.OmitReferenceName.Should().BeFalse();
        tagProvider.IsValid.Should().BeTrue();
        tagProvider.ValidationMessage.Should().BeNull();
    }

    [Fact]
    public async Task LoggerMessage_TagProvider_WithOmitReferenceName_ExtractsCorrectly()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class Request
{
    public string Method { get; set; }
    public string Path { get; set; }
}

public static class RequestTagProvider
{
    public static void AddTags(ITagCollector collector, Request request)
    {
        collector.Add(""method"", request.Method);
        collector.Add(""path"", request.Path);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Request processed""
    )]
    public static partial void LogRequest(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(RequestTagProvider), ""AddTags"", OmitReferenceName = true)]
        Request request);
}" + CreateMockGeneratedCode("Log", "LogRequest(ILogger logger, Request request)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.LogPropertiesParameters.Should().ContainSingle();

        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.TagProvider.Should().NotBeNull();
        
        var tagProvider = logPropsParam.TagProvider!;
        tagProvider.ParameterName.Should().Be("request");
        tagProvider.OmitReferenceName.Should().BeTrue();
        tagProvider.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task LoggerMessage_TagProvider_NonExistentMethod_ReportsInvalid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public static class UserTagProvider
{
    public static void SomeOtherMethod(ITagCollector collector, User user)
    {
        collector.Add(""user.name"", user.Name);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""NonExistentMethod"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.LogPropertiesParameters.Should().ContainSingle();
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.TagProvider.Should().NotBeNull();
        
        var tagProvider = logPropsParam.TagProvider!;
        tagProvider.IsValid.Should().BeFalse();
        tagProvider.ValidationMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task LoggerMessage_TagProvider_NonStaticMethod_ReportsInvalid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public class UserTagProvider
{
    public void AddTags(ITagCollector collector, User user)
    {
        collector.Add(""user.name"", user.Name);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddTags"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.LogPropertiesParameters.Should().ContainSingle();
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.TagProvider.Should().NotBeNull();
        
        var tagProvider = logPropsParam.TagProvider!;
        tagProvider.IsValid.Should().BeFalse();
        tagProvider.ValidationMessage.Should().Contain("must be static");
    }

    [Fact]
    public async Task LoggerMessage_TagProvider_PrivateMethod_ReportsInvalid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public static class UserTagProvider
{
    private static void AddTags(ITagCollector collector, User user)
    {
        collector.Add(""user.name"", user.Name);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddTags"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.LogPropertiesParameters.Should().ContainSingle();
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.TagProvider.Should().NotBeNull();
        
        var tagProvider = logPropsParam.TagProvider!;
        tagProvider.IsValid.Should().BeFalse();
        tagProvider.ValidationMessage.Should().Contain("must be public or internal");
    }

    [Fact]
    public async Task LoggerMessage_TagProvider_WrongReturnType_ReportsInvalid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public static class UserTagProvider
{
    public static int AddTags(ITagCollector collector, User user)
    {
        collector.Add(""user.name"", user.Name);
        return 0;
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddTags"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.LogPropertiesParameters.Should().ContainSingle();
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.TagProvider.Should().NotBeNull();
        
        var tagProvider = logPropsParam.TagProvider!;
        tagProvider.IsValid.Should().BeFalse();
        tagProvider.ValidationMessage.Should().Contain("must return void");
    }

    [Fact]
    public async Task LoggerMessage_TagProvider_WrongParameterCount_ReportsInvalid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public static class UserTagProvider
{
    public static void AddTags(ITagCollector collector, User user, string extra)
    {
        collector.Add(""user.name"", user.Name);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddTags"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.LogPropertiesParameters.Should().ContainSingle();
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.TagProvider.Should().NotBeNull();
        
        var tagProvider = logPropsParam.TagProvider!;
        tagProvider.IsValid.Should().BeFalse();
        tagProvider.ValidationMessage.Should().Contain("must have exactly 2 parameters");
    }

    [Fact]
    public async Task LoggerMessage_TagProvider_WrongFirstParameterType_ReportsInvalid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public static class UserTagProvider
{
    public static void AddTags(string wrongType, User user)
    {
        // Wrong first parameter type
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddTags"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.LogPropertiesParameters.Should().ContainSingle();
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.TagProvider.Should().NotBeNull();
        
        var tagProvider = logPropsParam.TagProvider!;
        tagProvider.IsValid.Should().BeFalse();
        tagProvider.ValidationMessage.Should().Contain("First parameter must be ITagCollector");
    }

    [Fact]
    public async Task LoggerMessage_TagProvider_WrongSecondParameterType_ReportsInvalid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public static class UserTagProvider
{
    public static void AddTags(ITagCollector collector, string wrongType)
    {
        // Wrong second parameter type
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddTags"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.LogPropertiesParameters.Should().ContainSingle();
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.TagProvider.Should().NotBeNull();
        
        var tagProvider = logPropsParam.TagProvider!;
        tagProvider.IsValid.Should().BeFalse();
        tagProvider.ValidationMessage.Should().Contain("Second parameter must be");
    }

    [Fact]
    public async Task LoggerMessage_TagProvider_MultipleParameters_ExtractsAllCorrectly()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public class Request
{
    public string Method { get; set; }
}

public static class UserTagProvider
{
    public static void AddUserTags(ITagCollector collector, User user)
    {
        collector.Add(""user.name"", user.Name);
    }
}

public static class RequestTagProvider
{
    public static void AddRequestTags(ITagCollector collector, Request request)
    {
        collector.Add(""request.method"", request.Method);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User request processed""
    )]
    public static partial void LogUserRequest(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddUserTags"")]
        User user,
        [LogProperties]
        [TagProvider(typeof(RequestTagProvider), ""AddRequestTags"")]
        Request request);
}" + CreateMockGeneratedCode("Log", "LogUserRequest(ILogger logger, User user, Request request)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.LogPropertiesParameters.Should().HaveCount(2);

        var userParam = usage.LogPropertiesParameters.FirstOrDefault(p => p.ParameterName == "user");
        userParam.Should().NotBeNull();
        userParam!.TagProvider.Should().NotBeNull();
        userParam.TagProvider!.ProviderMethodName.Should().Be("AddUserTags");
        userParam.TagProvider.IsValid.Should().BeTrue();

        var requestParam = usage.LogPropertiesParameters.FirstOrDefault(p => p.ParameterName == "request");
        requestParam.Should().NotBeNull();
        requestParam!.TagProvider.Should().NotBeNull();
        requestParam.TagProvider!.ProviderMethodName.Should().Be("AddRequestTags");
        requestParam.TagProvider.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task LoggerMessage_TagProvider_InternalProviderMethod_IsValid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public static class UserTagProvider
{
    internal static void AddTags(ITagCollector collector, User user)
    {
        collector.Add(""user.name"", user.Name);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddTags"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.LogPropertiesParameters.Should().ContainSingle();
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.TagProvider.Should().NotBeNull();
        logPropsParam.TagProvider!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task LoggerMessage_LogProperties_WithoutTagProvider_NoTagProviderInfo()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
    public string Email { get; set; }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        
        var usage = loggerUsages.Results.Should().ContainSingle()
            .Which.Should().BeOfType<LoggerMessageUsageInfo>().Which;
        usage.LogPropertiesParameters.Should().ContainSingle();
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        logPropsParam.TagProvider.Should().BeNull();
    }

    #endregion
}