using LoggerUsage.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Tests;

public class LoggerMethodsTests
{
    [Fact]
    public async Task BasicTest()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger)
    {
        logger.LogInformation(""Test message"");
    }
}");
        var extractor = new LoggerUsageExtractor();

        // Act
        var result = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(LoggerUsageMethodType.LoggerExtensions, result[0].MethodType);
    }

    [Fact]
    public async Task BasicTestNamedArguments()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger)
    {
        logger.LogInformation(message: ""Test message"", eventId: 6);
    }
}");
        var extractor = new LoggerUsageExtractor();

        // Act
        var result = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Test message", result[0].MessageTemplate);
        var details = Assert.IsType<EventIdDetails>(result[0].EventId);
        Assert.Equal(6, details.Id.Value);
        Assert.Same(ConstantOrReference.Missing, details.Name);
    }

    public static TheoryData<string[]> LoggerLogArguments() =>
    [
        /*
ILogger: void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter);

Extension methods:
public static void Log(this ILogger logger, LogLevel logLevel, string? message, params object?[] args)
public static void Log(this ILogger logger, LogLevel logLevel, EventId eventId, string? message, params object?[] args)
public static void Log(this ILogger logger, LogLevel logLevel, Exception? exception, string? message, params object?[] args)
public static void Log(this ILogger logger, LogLevel logLevel, EventId eventId, Exception? exception, string? message, params object?[] args)
        
        */
        new[] { "LogLevel.Information", "new EventId(1)", "\"the-state\"", "null", "(state, ex) => state.ToString()" },
        new[] { "LogLevel.Warning", "new EventId(2)", "\"Warning message\"" },
        new[] { "LogLevel.Error", "new EventId(3)", "\"Error state\"", "new Exception(\"err\")", "(state, ex) => state.ToString()" },
        new[] { "LogLevel.Debug", "default(EventId)", "\"Debug info\"", "null", "(state, ex) => state.ToString()" },
        new[] { "LogLevel.Critical", "new EventId(4, \"CriticalEvent\")", "\"Critical!\"", "ex", "(state, ex) => $\"{state} - {ex?.Message}\"" },
        new[] { "LogLevel.Trace", "new EventId()", "\"Trace message\"", "null", "(state, ex) => state.ToString()" }
    ];

    [Theory(Skip = "Not implemented yet")]
    [MemberData(nameof(LoggerLogArguments))]
    public async Task TestLoggerLogMethod(string[] logArgs)
    {
        // Arrange
        var code = $@"using Microsoft.Extensions.Logging;
using System;
namespace TestNamespace;

public class TestClass
{{
    public void TestMethod(ILogger logger, string strArg, int intArg, bool boolArg, DateTime dateTimeArg, Exception ex)
    {{
        logger.Log({string.Join(", ", logArgs)});
    }}
}}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();

        // Act
        var result = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(result);
        // TODO: replace with Assert.Single when the method is fixed
        Assert.Empty(result);
    }

    public static TheoryData<string, string[]> LoggerExtensionMethods()
    {
        string[] shortMessage = ["\"Test message\""];
        string[] messageWithSingleArg = ["\"Test message {Arg1}\"", "6"];
        string[] messageWithMultipleArgs = ["\"Test message {Arg1} {Arg2} {Arg3}\"", "6", "true", "DateTime.Now"];
        string[] messageWithMultipleLocalArgs = ["\"Test message {Arg1} {Arg2} {Arg3} {Arg4}\"", "strArg", "intArg", "boolArg", "dateTimeArg"];

        string[] eventIds = ["new EventId()", "1", "new EventId(1, \"Test\")", "new EventId(1)"];
        string[] exceptions = ["new Exception()", "new Exception(\"Test exception\")", "ex"];

        return new MatrixTheoryData<string, string[]>(
            Enum.GetNames(typeof(LogLevel)).Except([nameof(LogLevel.None)]).Select(logLevel => "Log" + logLevel),
            [
                shortMessage,
                messageWithSingleArg,
                messageWithMultipleArgs,
                messageWithMultipleLocalArgs,

                ..exceptions.SelectMany(ex => new string[][]
                {
                    [ ex, ..shortMessage ],
                    [ ex, ..messageWithSingleArg ],
                    [ ex, ..messageWithMultipleArgs ],
                    [ ex, ..messageWithMultipleLocalArgs ],
                }),
                ..eventIds.SelectMany(eventId => new string[][]
                {
                    [ eventId, ..shortMessage ],
                    [ eventId, ..messageWithSingleArg ],
                    [ eventId, ..messageWithMultipleArgs ],
                    [ eventId, ..messageWithMultipleLocalArgs ],
                }),
                ..eventIds.SelectMany(eventId => exceptions.SelectMany(ex => new string[][]
                {
                    [ eventId, ex, ..shortMessage ],
                    [ eventId, ex, ..messageWithSingleArg ],
                    [ eventId, ex, ..messageWithMultipleArgs ],
                    [ eventId, ex, ..messageWithMultipleLocalArgs ],
                })),
            ]
        );
    }

    [Theory]
    [MemberData(nameof(LoggerExtensionMethods))]
    public async Task TestLoggerExtensionMethods(string methodName, string[] args)
    {
        var code = $@"using Microsoft.Extensions.Logging;
using System;

namespace TestNamespace;

public class TestClass
{{
    public void TestMethod(ILogger logger, string strArg, int intArg, bool boolArg, DateTime dateTimeArg, Exception ex)
    {{
        logger.{methodName}({string.Join(", ", args)});
    }}
}}";

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();

        var result = extractor.ExtractLoggerUsages(compilation);

        Assert.NotNull(result);
        Assert.Single(result);
    }

    public static TheoryData<string, LogLevel?> LoggerLogLevelScenarios() => new()
    {
        { "LogInformation(", LogLevel.Information },
        { "LogWarning(", LogLevel.Warning },
        { "LogError(", LogLevel.Error },
        { "LogCritical(", LogLevel.Critical },
        { "LogDebug(", LogLevel.Debug },
        { "LogTrace(", LogLevel.Trace },
        { "Log(LogLevel.Information, ", LogLevel.Information },
        { "Log(LogLevel.Warning, ", LogLevel.Warning },
        { "Log(LogLevel.Error, ", LogLevel.Error },
        { "Log(LogLevel.Critical, ", LogLevel.Critical },
        { "Log(LogLevel.Debug, ", LogLevel.Debug },
        { "Log(LogLevel.Trace, ", LogLevel.Trace },
        { "Log(LogLevel.None, ", LogLevel.None }
    };

    [Theory]
    [MemberData(nameof(LoggerLogLevelScenarios))]
    public async Task TestLoggerLogLevelScenarios(string methodName, LogLevel? expectedLogLevel)
    {
        var code = $@"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public class TestClass
{{
    public void TestMethod(ILogger logger)
    {{
        logger.{methodName}""Test message"");
    }}
}}";

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();
        var result = extractor.ExtractLoggerUsages(compilation);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(expectedLogLevel, result[0].LogLevel);
    }

    public static TheoryData<string, string, EventIdRef> LoggerEventIdScenariosReference() => new()
    {
        { "LogInformation", "eidVar", new EventIdRef(nameof(OperationKind.LocalReference), "eidVar") },
        { "LogInformation", "eidParam", new EventIdRef(nameof(OperationKind.ParameterReference), "eidParam") },
        { "LogInformation", "_eidField", new EventIdRef(nameof(OperationKind.FieldReference), "_eidField") },
    };

    [Theory]
    [MemberData(nameof(LoggerEventIdScenariosReference))]
    public async Task TestLoggerEventIdScenariosReference(string methodName, string eventId, EventIdRef expectedEventIdRef)
    {
        var code = $@"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public class TestClass
{{
    private readonly EventId _eidField = new EventId(5, ""VarEvent"");

    public void TestMethod(ILogger logger, EventId eidParam)
    {{
        var eidVar = new EventId(5, ""VarEvent"");
        logger.{methodName}({eventId}, ""Test message"");
    }}
}}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();
        var result = extractor.ExtractLoggerUsages(compilation);
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.NotNull(result[0].EventId);
        var @ref = Assert.IsType<EventIdRef>(result[0].EventId);
        Assert.Equal(expectedEventIdRef, @ref);
    }

    public static TheoryData<string, string, ConstantOrReference, ConstantOrReference> LoggerEventIdScenariosValues() => new()
    {
        { "LogWarning", "6", ConstantOrReference.Constant(6), ConstantOrReference.Missing },
        { "LogWarning", "new EventId(1)", ConstantOrReference.Constant(1), ConstantOrReference.Constant(null!) },
        { "LogError", "new EventId(1, \"EventName\")", ConstantOrReference.Constant(1), ConstantOrReference.Constant("EventName") },
        { "LogCritical", "new EventId(int.MaxValue, \"MaxValueEvent\")", ConstantOrReference.Constant(int.MaxValue), ConstantOrReference.Constant("MaxValueEvent") },
        { "LogDebug", "new EventId(42, \"CustomEvent\")", ConstantOrReference.Constant(42), ConstantOrReference.Constant("CustomEvent") },
        { "LogWarning", "new EventId(0, \"OnlyName\")", ConstantOrReference.Constant(0), ConstantOrReference.Constant("OnlyName") },
        { "LogCritical", "new EventId(-1, \"NegativeId\")", ConstantOrReference.Constant(-1), ConstantOrReference.Constant("NegativeId") },
        { "LogDebug", "new EventId(0, \"\")", ConstantOrReference.Constant(0), ConstantOrReference.Constant("") },
        { "LogInformation", "new EventId(1 + 2, \"ExprName\")", ConstantOrReference.Constant(3), ConstantOrReference.Constant("ExprName") },
        { "LogWarning", "new EventId(id: 7, name: \"NamedArgs\")", ConstantOrReference.Constant(7), ConstantOrReference.Constant("NamedArgs") },
        { "LogInformation", "new EventId(_id, _name)", new ConstantOrReference(nameof(OperationKind.FieldReference), "_id"), new ConstantOrReference(nameof(OperationKind.FieldReference), "_name") },
        { "LogInformation", "new EventId(_id, name: _name)", new ConstantOrReference(nameof(OperationKind.FieldReference), "_id"), new ConstantOrReference(nameof(OperationKind.FieldReference), "_name") },
        { "LogInformation", "new EventId(id: _id, _name)", new ConstantOrReference(nameof(OperationKind.FieldReference), "_id"), new ConstantOrReference(nameof(OperationKind.FieldReference), "_name") },
        { "LogInformation", "new EventId(_id, name: \"FieldName\")", new ConstantOrReference(nameof(OperationKind.FieldReference), "_id"), ConstantOrReference.Constant("FieldName") },
        { "LogInformation", "new EventId(5, name: \"FieldName\")", ConstantOrReference.Constant(5), ConstantOrReference.Constant("FieldName") },
        { "LogInformation", "new EventId(idVar, nameVar)", new ConstantOrReference(nameof(OperationKind.LocalReference), "idVar"), new ConstantOrReference(nameof(OperationKind.LocalReference), "nameVar") },
    };

    [Theory]
    [MemberData(nameof(LoggerEventIdScenariosValues))]
    public async Task TestLoggerEventIdScenariosValues(string methodName, string eventId, ConstantOrReference expectedId, ConstantOrReference expectedName)
    {
        var code = $@"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public class TestClass
{{
    private readonly string _name;
    private readonly int _id;

    public void TestMethod(ILogger logger, int id, string name)
    {{
        int idVar = 5;
        string nameVar = ""VarEvent"";
        logger.{methodName}({eventId}, ""Test message"");
    }}
}}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();
        var result = extractor.ExtractLoggerUsages(compilation);
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.NotNull(result[0].EventId);
        var details = Assert.IsType<EventIdDetails>(result[0].EventId);
        Assert.Equal(expectedId, details.Id);
        Assert.Equal(expectedName, details.Name);
    }

    [Theory]
    [InlineData("default(EventId)")]
    [InlineData("eventId: default")]
    [InlineData("new EventId()")]
    public async Task TestLoggerWithDefaultEventId(string eventId)
    {
        // Arrange
        var code = $@"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public class TestClass
{{
    public void TestMethod(ILogger logger, EventId eidParam, int id, string name)
    {{
        logger.LogInformation({eventId}, ""Test message"");
    }}
}}";

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();

        // Act
        var result = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Null(result[0].EventId);
    }

    public static TheoryData<string, string[]> LoggerMessageTemplates() => new()
    {
        { "Test message", [] },
        { "Test message {arg1}", [ "\"arg1\"" ] },
        { "Test message {arg1} {arg2}", [ "\"arg1\"", "\"arg2\"" ] },
        { "Test message {arg1} {arg2} {arg3}", [ "\"arg1\"", "\"arg2\"", "\"arg3\"" ] },
        { "Test message with \"quotes\"", []},
        { "Test message with {arg1} and \"quotes\"", [ "\"arg1\"" ] },
        { "Test message with {arg1} and {arg2} and \"quotes\"", [ "\"arg1\"", "\"arg2\"" ] },
        { "Test message with {arg1} and {arg2} and {arg3} and \"quotes\"", [ "\"arg1\"", "\"arg2\"", "\"arg3\"" ] },

        // Placeholders with format strings
        { "Test message {arg1:N2}", [ "\"arg1\"" ] },
        { "Test message {arg1:D} {arg2:X}", [ "\"arg1\"", "\"arg2\"" ] },
        { "Logged on {PlaceHolderName:MMMM dd, yyyy}", ["System.DateTimeOffset.UtcNow"] },
        { "Test message {arg1} and {arg2:N0} and {arg3}", [ "\"arg1\"", "\"arg2\"", "\"arg3\"" ] },
        { "Test message {arg1:}", [ "\"arg1\"" ] }, // empty format
    };

    [Theory]
    [MemberData(nameof(LoggerMessageTemplates))]
    public async Task TestLoggerMessageTemplate(string template, string[] args)
    {
        // Arrange
        var quotedMessage = '"' + template.Replace("\"", "\\\"") + '"';
        var methodArgs = quotedMessage + (args.Length > 0 ? ", " : "") + string.Join(", ", args);
        var code = $@"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public class TestClass
{{
    public void TestMethod(ILogger logger)
    {{
        logger.LogInformation({methodArgs});
    }}
}}";

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();

        // Act
        var result = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(template, result[0].MessageTemplate);
    }

    public static TheoryData<string, string[], string> LoggerInterpolatedTemplateCases() => new()
    {
        { $"test {nameof(System)} message with {{arg1}} template", ["\"arg1\""], "test System message with {arg1} template" },
        { $"prefix {nameof(System)} and {{arg}}", ["\"arg\""], "prefix System and {arg}" },
        { $"just {{arg}} and {nameof(System)}", ["\"arg\""], "just {arg} and System" },
        { $"no placeholders {nameof(System)}", [], "no placeholders System" },
    };

    [Theory]
    [MemberData(nameof(LoggerInterpolatedTemplateCases))]
    public async Task TestLoggerMessageTemplateWithInterpolatedConstant(string template, string[] args, string expectedTemplate)
    {
        // Arrange
        var quotedMessage = '"' + template.Replace("\"", "\\\"") + '"';
        var methodArgs = quotedMessage + (args.Length > 0 ? ", " : "") + string.Join(", ", args);
        var code = $@"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public class TestClass
{{
    public void TestMethod(ILogger logger)
    {{
        logger.LogInformation({methodArgs});
    }}
}}";

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();

        // Act
        var result = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(expectedTemplate, result[0].MessageTemplate);
    }

    public static TheoryData<string, string[], List<MessageParameter>> LoggerMessageParameterCases() => new()
    {
        { "Test message {arg1}", ["strArg"], [ 
                new MessageParameter("arg1", "string", nameof(OperationKind.ParameterReference))
        ] },
        { "Test message {arg1} {arg2}", ["strArg", "intArg"], [ 
            new MessageParameter("arg1", "string", nameof(OperationKind.ParameterReference)), 
            new MessageParameter("arg2", "int", nameof(OperationKind.ParameterReference)) 
        ] },
        { "Test message {arg1} {arg2} {arg3}", ["strArg", "intArg", "boolArg"], [
            new MessageParameter("arg1", "string", nameof(OperationKind.ParameterReference)), 
            new MessageParameter("arg2", "int", nameof(OperationKind.ParameterReference)), 
            new MessageParameter("arg3", "bool", nameof(OperationKind.ParameterReference))
        ] },
        { "Test message {arg1} and {arg2} and {arg3}", ["strArg", "intArg", "boolArg"], [ 
            new MessageParameter("arg1", "string", nameof(OperationKind.ParameterReference)), 
            new MessageParameter("arg2", "int", nameof(OperationKind.ParameterReference)), 
            new MessageParameter("arg3", "bool", nameof(OperationKind.ParameterReference))
         ] },
        { "Test message with no params", [], [] },

        // Constant references
        { "Test message {arg1}", ["constStr"], [
            new MessageParameter("arg1", "string", "Constant") // const is local in Roslyn
        ] },
        { "Test message {arg1} {arg2}", ["constStr", "constInt"], [
            new MessageParameter("arg1", "string", "Constant"),
            new MessageParameter("arg2", "int", "Constant")
        ] },
        { "Test message {arg1} {arg2} {arg3}", ["constStr", "constInt", "constBool"], [
            new MessageParameter("arg1", "string", "Constant"),
            new MessageParameter("arg2", "int", "Constant"),
            new MessageParameter("arg3", "bool", "Constant")
        ] },

        // Local variable references
        { "Test message {arg1}", ["localStr"], [
            new MessageParameter("arg1", "string", nameof(OperationKind.LocalReference))
        ] },
        { "Test message {arg1} {arg2}", ["localStr", "localInt"], [
            new MessageParameter("arg1", "string", nameof(OperationKind.LocalReference)),
            new MessageParameter("arg2", "int", nameof(OperationKind.LocalReference))
        ] },
        { "Test message {arg1} {arg2} {arg3}", ["localStr", "localInt", "localBool"], [
            new MessageParameter("arg1", "string", nameof(OperationKind.LocalReference)),
            new MessageParameter("arg2", "int", nameof(OperationKind.LocalReference)),
            new MessageParameter("arg3", "bool", nameof(OperationKind.LocalReference))
        ] },

        // with formatting
        { "Test message {arg1:N2}", ["strArg"], [
            new MessageParameter("arg1", "string", nameof(OperationKind.ParameterReference))
        ] },
        { "Test message {arg1:D} {arg2:X}", ["strArg", "intArg"], [
            new MessageParameter("arg1", "string", nameof(OperationKind.ParameterReference)),
            new MessageParameter("arg2", "int", nameof(OperationKind.ParameterReference))
        ] },
        { "Test message {arg1} and {arg2:N0} and {arg3}", ["strArg", "intArg", "boolArg"], [
            new MessageParameter("arg1", "string", nameof(OperationKind.ParameterReference)),
            new MessageParameter("arg2", "int", nameof(OperationKind.ParameterReference)),
            new MessageParameter("arg3", "bool", nameof(OperationKind.ParameterReference))
        ] },
    };

    [Theory]
    [MemberData(nameof(LoggerMessageParameterCases))]
    public async Task TestLoggerMessageParameters(string template, string[] argNames, List<MessageParameter> expectedParameters)
    {
        // Arrange
        var quotedMessage = '"' + template.Replace("\"", "\\\"") + '"';
        var methodArgs = quotedMessage + (argNames.Length > 0 ? ", " : "") + string.Join(", ", argNames);
        var code = $@"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public class TestClass
{{
    public void TestMethod(ILogger logger, string strArg, int intArg, bool boolArg)
    {{
        const string constStr = ""constStrValue"";
        const int constInt = 42;
        const bool constBool = true;
        string localStr = ""localStr"";
        int localInt = 42;
        bool localBool = true;

        logger.LogInformation({methodArgs});
    }}
}}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();

        // Act
        var result = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        var parameters = result[0].MessageParameters;
        Assert.Equal(expectedParameters.Count, parameters.Count);
        Assert.Equal(expectedParameters, parameters);
    }
}
