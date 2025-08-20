using LoggerUsage.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using TUnit.Core;

namespace LoggerUsage.Tests;

public class LoggerMethodsTests
{
    [Test]
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        Assert.Equal(LoggerUsageMethodType.LoggerExtensions, loggerUsages.Results[0].MethodType);
    }

    [Test]
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        Assert.Equal("Test message", loggerUsages.Results[0].MessageTemplate);
        var details = Assert.IsType<EventIdDetails>(loggerUsages.Results[0].EventId);
        Assert.Equal(6, details.Id.Value);
        Assert.Same(ConstantOrReference.Missing, details.Name);
    }

    public static IEnumerable<object[]> LoggerLogArguments()
    {
        /*
ILogger: void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter);

Extension methods:
public static void Log(this ILogger logger, LogLevel logLevel, string? message, params object?[] args)
public static void Log(this ILogger logger, LogLevel logLevel, EventId eventId, string? message, params object?[] args)
public static void Log(this ILogger logger, LogLevel logLevel, Exception? exception, string? message, params object?[] args)
public static void Log(this ILogger logger, LogLevel logLevel, EventId eventId, Exception? exception, string? message, params object?[] args)
        
        */
        yield return new object[] { new[] { "LogLevel.Information", "new EventId(1)", "\"the-state\"", "null", "(state, ex) => state.ToString()" } };
        yield return new object[] { new[] { "LogLevel.Warning", "new EventId(2)", "\"Warning message\"" } };
        yield return new object[] { new[] { "LogLevel.Error", "new EventId(3)", "\"Error state\"", "new Exception(\"err\")", "(state, ex) => state.ToString()" } };
        yield return new object[] { new[] { "LogLevel.Debug", "default(EventId)", "\"Debug info\"", "null", "(state, ex) => state.ToString()" } };
        yield return new object[] { new[] { "LogLevel.Critical", "new EventId(4, \"CriticalEvent\")", "\"Critical!\"", "ex", "(state, ex) => $\"{state} - {ex?.Message}\"" } };
        yield return new object[] { new[] { "LogLevel.Trace", "new EventId()", "\"Trace message\"", "null", "(state, ex) => state.ToString()" } };
    }

    // Skip for now as mentioned in original test
    // [Test]
    // [MethodDataSource(nameof(LoggerLogArguments))]
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        // TODO: replace with Assert.Single when the method is fixed
        Assert.Empty(loggerUsages.Results);
    }

    public static IEnumerable<object[]> LoggerExtensionMethods()
    {
        string[] shortMessage = ["\"Test message\""];
        string[] messageWithSingleArg = ["\"Test message {Arg1}\"", "6"];
        string[] messageWithMultipleArgs = ["\"Test message {Arg1} {Arg2} {Arg3}\"", "6", "true", "DateTime.Now"];
        string[] messageWithMultipleLocalArgs = ["\"Test message {Arg1} {Arg2} {Arg3} {Arg4}\"", "strArg", "intArg", "boolArg", "dateTimeArg"];

        string[] eventIds = ["new EventId()", "1", "new EventId(1, \"Test\")", "new EventId(1)"];
        string[] exceptions = ["new Exception()", "new Exception(\"Test exception\")", "ex"];

        var matrixData = new MatrixTheoryData<string, string[]>(
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
        
        foreach (var item in matrixData)
        {
            yield return item;
        }
    }

    [Test]
    [MethodDataSource(nameof(LoggerExtensionMethods))]
    public async Task TestLoggerExtensionMethods(string methodName, string[] args)
    {
        // Arrange
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
    }

    public static IEnumerable<object[]> LoggerLogLevelScenarios()
    {
        yield return new object[] { "LogInformation(", LogLevel.Information };
        yield return new object[] { "LogWarning(", LogLevel.Warning };
        yield return new object[] { "LogError(", LogLevel.Error };
        yield return new object[] { "LogCritical(", LogLevel.Critical };
        yield return new object[] { "LogDebug(", LogLevel.Debug };
        yield return new object[] { "LogTrace(", LogLevel.Trace };
        yield return new object[] { "Log(LogLevel.Information, ", LogLevel.Information };
        yield return new object[] { "Log(LogLevel.Warning, ", LogLevel.Warning };
        yield return new object[] { "Log(LogLevel.Error, ", LogLevel.Error };
        yield return new object[] { "Log(LogLevel.Critical, ", LogLevel.Critical };
        yield return new object[] { "Log(LogLevel.Debug, ", LogLevel.Debug };
        yield return new object[] { "Log(LogLevel.Trace, ", LogLevel.Trace };
        yield return new object[] { "Log(LogLevel.None, ", LogLevel.None };
    }

    [Test]
    [MethodDataSource(nameof(LoggerLogLevelScenarios))]
    public async Task TestLoggerLogLevelScenarios(string methodName, LogLevel? expectedLogLevel)
    {
        // Arrange
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        Assert.Equal(expectedLogLevel, loggerUsages.Results[0].LogLevel);
    }

    public static IEnumerable<object[]> LoggerEventIdScenariosReference()
    {
        yield return new object[] { "LogInformation", "eidVar", new EventIdRef(nameof(OperationKind.LocalReference), "eidVar") };
        yield return new object[] { "LogInformation", "eidParam", new EventIdRef(nameof(OperationKind.ParameterReference), "eidParam") };
        yield return new object[] { "LogInformation", "_eidField", new EventIdRef(nameof(OperationKind.FieldReference), "_eidField") };
    }

    [Test]
    [MethodDataSource(nameof(LoggerEventIdScenariosReference))]
    public async Task TestLoggerEventIdScenariosReference(string methodName, string eventId, EventIdRef expectedEventIdRef)
    {
        // Arrange
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();
        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        Assert.NotNull(loggerUsages.Results[0].EventId);
        var @ref = Assert.IsType<EventIdRef>(loggerUsages.Results[0].EventId);
        Assert.Equal(expectedEventIdRef, @ref);
    }

    public static IEnumerable<object[]> LoggerEventIdScenariosValues()
    {
        yield return new object[] { "LogWarning", "6", ConstantOrReference.Constant(6), ConstantOrReference.Missing };
        yield return new object[] { "LogWarning", "new EventId(1)", ConstantOrReference.Constant(1), ConstantOrReference.Missing };
        yield return new object[] { "LogError", "new EventId(1, \"EventName\")", ConstantOrReference.Constant(1), ConstantOrReference.Constant("EventName") };
        yield return new object[] { "LogCritical", "new EventId(int.MaxValue, \"MaxValueEvent\")", ConstantOrReference.Constant(int.MaxValue), ConstantOrReference.Constant("MaxValueEvent") };
        yield return new object[] { "LogDebug", "new EventId(42, \"CustomEvent\")", ConstantOrReference.Constant(42), ConstantOrReference.Constant("CustomEvent") };
        yield return new object[] { "LogWarning", "new EventId(0, \"OnlyName\")", ConstantOrReference.Constant(0), ConstantOrReference.Constant("OnlyName") };
        yield return new object[] { "LogCritical", "new EventId(-1, \"NegativeId\")", ConstantOrReference.Constant(-1), ConstantOrReference.Constant("NegativeId") };
        yield return new object[] { "LogDebug", "new EventId(0, \"\")", ConstantOrReference.Constant(0), ConstantOrReference.Constant("") };
        yield return new object[] { "LogInformation", "new EventId(1 + 2, \"ExprName\")", ConstantOrReference.Constant(3), ConstantOrReference.Constant("ExprName") };
        yield return new object[] { "LogWarning", "new EventId(id: 7, name: \"NamedArgs\")", ConstantOrReference.Constant(7), ConstantOrReference.Constant("NamedArgs") };
        yield return new object[] { "LogInformation", "new EventId(_id, _name)", new ConstantOrReference(nameof(OperationKind.FieldReference), "_id"), new ConstantOrReference(nameof(OperationKind.FieldReference), "_name") };
        yield return new object[] { "LogInformation", "new EventId(_id, name: _name)", new ConstantOrReference(nameof(OperationKind.FieldReference), "_id"), new ConstantOrReference(nameof(OperationKind.FieldReference), "_name") };
        yield return new object[] { "LogInformation", "new EventId(id: _id, _name)", new ConstantOrReference(nameof(OperationKind.FieldReference), "_id"), new ConstantOrReference(nameof(OperationKind.FieldReference), "_name") };
        yield return new object[] { "LogInformation", "new EventId(_id, name: \"FieldName\")", new ConstantOrReference(nameof(OperationKind.FieldReference), "_id"), ConstantOrReference.Constant("FieldName") };
        yield return new object[] { "LogInformation", "new EventId(5, name: \"FieldName\")", ConstantOrReference.Constant(5), ConstantOrReference.Constant("FieldName") };
        yield return new object[] { "LogInformation", "new EventId(idVar, nameVar)", new ConstantOrReference(nameof(OperationKind.LocalReference), "idVar"), new ConstantOrReference(nameof(OperationKind.LocalReference), "nameVar") };
    }

    [Test]
    [MethodDataSource(nameof(LoggerEventIdScenariosValues))]
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        Assert.NotNull(loggerUsages.Results[0].EventId);
        var details = Assert.IsType<EventIdDetails>(loggerUsages.Results[0].EventId);
        Assert.Equal(expectedId, details.Id);
        Assert.Equal(expectedName, details.Name);
    }

    [Test]
    [Arguments("default(EventId)")]
    [Arguments("eventId: default")]
    [Arguments("new EventId()")]
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        Assert.Null(loggerUsages.Results[0].EventId);
    }

    public static IEnumerable<object[]> LoggerMessageTemplates()
    {
        yield return new object[] { "Test message", new string[0] };
        yield return new object[] { "Test message {arg1}", new[] { "\"arg1\"" } };
        yield return new object[] { "Test message {arg1} {arg2}", new[] { "\"arg1\"", "\"arg2\"" } };
        yield return new object[] { "Test message {arg1} {arg2} {arg3}", new[] { "\"arg1\"", "\"arg2\"", "\"arg3\"" } };
        yield return new object[] { "Test message with \"quotes\"", new string[0] };
        yield return new object[] { "Test message with {arg1} and \"quotes\"", new[] { "\"arg1\"" } };
        yield return new object[] { "Test message with {arg1} and {arg2} and \"quotes\"", new[] { "\"arg1\"", "\"arg2\"" } };
        yield return new object[] { "Test message with {arg1} and {arg2} and {arg3} and \"quotes\"", new[] { "\"arg1\"", "\"arg2\"", "\"arg3\"" } };

        // Placeholders with format strings
        yield return new object[] { "Test message {arg1:N2}", new[] { "\"arg1\"" } };
        yield return new object[] { "Test message {arg1:D} {arg2:X}", new[] { "\"arg1\"", "\"arg2\"" } };
        yield return new object[] { "Logged on {PlaceHolderName:MMMM dd, yyyy}", new[] { "System.DateTimeOffset.UtcNow" } };
        yield return new object[] { "Test message {arg1} and {arg2:N0} and {arg3}", new[] { "\"arg1\"", "\"arg2\"", "\"arg3\"" } };
        yield return new object[] { "Test message {arg1:}", new[] { "\"arg1\"" } }; // empty format
    }

    [Test]
    [MethodDataSource(nameof(LoggerMessageTemplates))]
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        Assert.Equal(template, loggerUsages.Results[0].MessageTemplate);
    }

    public static IEnumerable<object[]> LoggerInterpolatedTemplateCases()
    {
        yield return new object[] { $"test {nameof(System)} message with {{arg1}} template", new[] { "\"arg1\"" }, "test System message with {arg1} template" };
        yield return new object[] { $"prefix {nameof(System)} and {{arg}}", new[] { "\"arg\"" }, "prefix System and {arg}" };
        yield return new object[] { $"just {{arg}} and {nameof(System)}", new[] { "\"arg\"" }, "just {arg} and System" };
        yield return new object[] { $"no placeholders {nameof(System)}", new string[0], "no placeholders System" };
    }

    [Test]
    [MethodDataSource(nameof(LoggerInterpolatedTemplateCases))]
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var result = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Results);
        Assert.Equal(expectedTemplate, result.Results[0].MessageTemplate);
    }

    public static IEnumerable<object[]> LoggerMessageParameterCases()
    {
        yield return new object[] { "Test message {arg1}", new[] { "strArg" }, new List<MessageParameter> { 
                new MessageParameter("arg1", "string", nameof(OperationKind.ParameterReference))
        } };
        yield return new object[] { "Test message {arg1} {arg2}", new[] { "strArg", "intArg" }, new List<MessageParameter> { 
            new MessageParameter("arg1", "string", nameof(OperationKind.ParameterReference)), 
            new MessageParameter("arg2", "int", nameof(OperationKind.ParameterReference)) 
        } };
        yield return new object[] { "Test message {arg1} {arg2} {arg3}", new[] { "strArg", "intArg", "boolArg" }, new List<MessageParameter> {
            new MessageParameter("arg1", "string", nameof(OperationKind.ParameterReference)), 
            new MessageParameter("arg2", "int", nameof(OperationKind.ParameterReference)), 
            new MessageParameter("arg3", "bool", nameof(OperationKind.ParameterReference))
        } };
        yield return new object[] { "Test message {arg1} and {arg2} and {arg3}", new[] { "strArg", "intArg", "boolArg" }, new List<MessageParameter> { 
            new MessageParameter("arg1", "string", nameof(OperationKind.ParameterReference)), 
            new MessageParameter("arg2", "int", nameof(OperationKind.ParameterReference)), 
            new MessageParameter("arg3", "bool", nameof(OperationKind.ParameterReference))
         } };
        yield return new object[] { "Test message with no params", new string[0], new List<MessageParameter>() };

        // Property references
        yield return new object[] { "Test with references properties {arg1}", new[] { "strArg.Length" }, new List<MessageParameter> {
            new MessageParameter("arg1", "int", nameof(OperationKind.PropertyReference))
        } };
        yield return new object[] { "Test with references properties {arg1} and {arg2}", new[] { "strArg.Length", "intArg.ToString()" }, new List<MessageParameter> {
            new MessageParameter("arg1", "int", nameof(OperationKind.PropertyReference)),
            new MessageParameter("arg2", "string", nameof(OperationKind.Invocation))
        } };
        yield return new object[] { "Test with references properties {arg1} and {arg2} and {arg3}", new[] { "strArg.Length", "intArg.ToString()", "boolArg.ToString()" }, new List<MessageParameter> {
            new MessageParameter("arg1", "int", nameof(OperationKind.PropertyReference)),
            new MessageParameter("arg2", "string", nameof(OperationKind.Invocation)),
            new MessageParameter("arg3", "string", nameof(OperationKind.Invocation))
        } };

        // Instance member references
        yield return new object[] { "Test with references properties {arg1}", new[] { "this._strField.Length" }, new List<MessageParameter> {
            new MessageParameter("arg1", "int", nameof(OperationKind.PropertyReference))
        } };
        yield return new object[] { "Test with references properties {arg1} and {arg2}", new[] { "this._strField.Length", "this._intField.ToString()" }, new List<MessageParameter> {
            new MessageParameter("arg1", "int", nameof(OperationKind.PropertyReference)),
            new MessageParameter("arg2", "string", nameof(OperationKind.Invocation))
        } };
        yield return new object[] { "Test with references properties {arg1} and {arg2} and {arg3}", new[] { "this._strField.Length", "this._intField.ToString()", "this._boolField.ToString()" }, new List<MessageParameter> {
            new MessageParameter("arg1", "int", nameof(OperationKind.PropertyReference)),
            new MessageParameter("arg2", "string", nameof(OperationKind.Invocation)),
            new MessageParameter("arg3", "string", nameof(OperationKind.Invocation))
        } };

        // Conditional access
        yield return new object[] { "Test with nullable references properties {arg1}", new[] { "this._strField?.Length" }, new List<MessageParameter> {
            new MessageParameter("arg1", "int?", nameof(OperationKind.ConditionalAccess))
        } };
        yield return new object[] { "Test with nullable references properties {arg1} and {arg2}", new[] { "this._strField?.Length", "this._intField?.ToString()" }, new List<MessageParameter> {
            new MessageParameter("arg1", "int?", nameof(OperationKind.ConditionalAccess)),
            new MessageParameter("arg2", "string", nameof(OperationKind.ConditionalAccess))
        } };
        yield return new object[] { "Test with nullable references properties {arg1} and {arg2} and {arg3}", new[] { "this._strField?.Length", "this._intField?.ToString()", "this._boolField?.ToString()" }, new List<MessageParameter> {
            new MessageParameter("arg1", "int?", nameof(OperationKind.ConditionalAccess)),
            new MessageParameter("arg2", "string", nameof(OperationKind.ConditionalAccess)),
            new MessageParameter("arg3", "string", nameof(OperationKind.ConditionalAccess))
        } };

        // Conditional access with null coalescing
        yield return new object[] { "Test with nullable references properties {arg1}", new[] { "this._strField?.Length ?? 0" }, new List<MessageParameter> {
            new MessageParameter("arg1", "int", nameof(OperationKind.Coalesce))
        } };
        yield return new object[] { "Test with nullable references properties {arg1} and {arg2}", new[] { "this._strField?.Length ?? 0", "this._intField?.ToString() ?? \"default-value\"" }, new List<MessageParameter> {
            new MessageParameter("arg1", "int", nameof(OperationKind.Coalesce)),
            new MessageParameter("arg2", "string", nameof(OperationKind.Coalesce))
        } };
        yield return new object[] { "Test with nullable references properties {arg1} and {arg2} and {arg3}", new[] { "this._strField?.Length ?? 0", "this._intField?.ToString() ?? \"default-value\"", "this._boolField?.ToString() ?? \"default-value\"" }, new List<MessageParameter> {
            new MessageParameter("arg1", "int", nameof(OperationKind.Coalesce)),
            new MessageParameter("arg2", "string", nameof(OperationKind.Coalesce)),
            new MessageParameter("arg3", "string", nameof(OperationKind.Coalesce))
        } };

        // Constant references
        yield return new object[] { "Test message {arg1}", new[] { "constStr" }, new List<MessageParameter> {
            new MessageParameter("arg1", "string", "Constant") // const is local in Roslyn
        } };
        yield return new object[] { "Test message {arg1} {arg2}", new[] { "constStr", "constInt" }, new List<MessageParameter> {
            new MessageParameter("arg1", "string", "Constant"),
            new MessageParameter("arg2", "int", "Constant")
        } };
        yield return new object[] { "Test message {arg1} {arg2} {arg3}", new[] { "constStr", "constInt", "constBool" }, new List<MessageParameter> {
            new MessageParameter("arg1", "string", "Constant"),
            new MessageParameter("arg2", "int", "Constant"),
            new MessageParameter("arg3", "bool", "Constant")
        } };

        // Local variable references
        yield return new object[] { "Test message {arg1}", new[] { "localStr" }, new List<MessageParameter> {
            new MessageParameter("arg1", "string", nameof(OperationKind.LocalReference))
        } };
        yield return new object[] { "Test message {arg1} {arg2}", new[] { "localStr", "localInt" }, new List<MessageParameter> {
            new MessageParameter("arg1", "string", nameof(OperationKind.LocalReference)),
            new MessageParameter("arg2", "int", nameof(OperationKind.LocalReference))
        } };
        yield return new object[] { "Test message {arg1} {arg2} {arg3}", new[] { "localStr", "localInt", "localBool" }, new List<MessageParameter> {
            new MessageParameter("arg1", "string", nameof(OperationKind.LocalReference)),
            new MessageParameter("arg2", "int", nameof(OperationKind.LocalReference)),
            new MessageParameter("arg3", "bool", nameof(OperationKind.LocalReference))
        } };

        // with formatting
        yield return new object[] { "Test message {arg1:N2}", new[] { "strArg" }, new List<MessageParameter> {
            new MessageParameter("arg1", "string", nameof(OperationKind.ParameterReference))
        } };
        yield return new object[] { "Test message {arg1:D} {arg2:X}", new[] { "strArg", "intArg" }, new List<MessageParameter> {
            new MessageParameter("arg1", "string", nameof(OperationKind.ParameterReference)),
            new MessageParameter("arg2", "int", nameof(OperationKind.ParameterReference))
        } };
        yield return new object[] { "Test message {arg1} and {arg2:N0} and {arg3}", new[] { "strArg", "intArg", "boolArg" }, new List<MessageParameter> {
            new MessageParameter("arg1", "string", nameof(OperationKind.ParameterReference)),
            new MessageParameter("arg2", "int", nameof(OperationKind.ParameterReference)),
            new MessageParameter("arg3", "bool", nameof(OperationKind.ParameterReference))
        } };
    }

    [Test]
    [MethodDataSource(nameof(LoggerMessageParameterCases))]
    public async Task TestLoggerMessageParameters(string template, string[] argNames, List<MessageParameter> expectedParameters)
    {
        // Arrange
        var quotedMessage = '"' + template.Replace("\"", "\\\"") + '"';
        var methodArgs = quotedMessage + (argNames.Length > 0 ? ", " : "") + string.Join(", ", argNames);
        var code = $@"#nullable enable
using Microsoft.Extensions.Logging;

namespace TestNamespace;

public class TestClass
{{
    private readonly string? _strField = ""strFieldValue"";
    private readonly int? _intField = 42;
    private readonly bool? _boolField = true;
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
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        var parameters = loggerUsages.Results[0].MessageParameters;
        Assert.Equal(expectedParameters.Count, parameters.Count);
        Assert.Equal(expectedParameters, parameters);
    }
}
