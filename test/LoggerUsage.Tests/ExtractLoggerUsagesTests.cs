using LoggerUsage.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Tests;

public class ExtractLoggerUsagesTests
{
    [Fact]
    public async Task BasicTest()
    {
        // Arrange
        var compilation = await CreateCompilationAsync(@"using Microsoft.Extensions.Logging;
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
        var compilation = await CreateCompilationAsync(code);
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

        var compilation = await CreateCompilationAsync(code);
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

        var compilation = await CreateCompilationAsync(code);
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
        var compilation = await CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();
        var result = extractor.ExtractLoggerUsages(compilation);
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.NotNull(result[0].EventId);
        var @ref = Assert.IsType<EventIdRef>(result[0].EventId);
        Assert.Equal(expectedEventIdRef, @ref);
    }

    public static TheoryData<string, string, ConstantOrReference, ConstantOrReference> LoggerEventIdScenariosConstructor() => new()
    {
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
    [MemberData(nameof(LoggerEventIdScenariosConstructor))]
    public async Task TestLoggerEventIdScenariosConstructor(string methodName, string eventId, ConstantOrReference expectedId, ConstantOrReference expectedName)
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
        var compilation = await CreateCompilationAsync(code);
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

        var compilation = await CreateCompilationAsync(code);
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

        var compilation = await CreateCompilationAsync(code);
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

        var compilation = await CreateCompilationAsync(code);
        var extractor = new LoggerUsageExtractor();

        // Act
        var result = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(expectedTemplate, result[0].MessageTemplate);
    }

    private static async Task<CSharpCompilation> CreateCompilationAsync(string sourceCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, default);
        references = references.Add(MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithSpecificDiagnosticOptions(
                new Dictionary<string, ReportDiagnostic>
                {
                    ["CS0169"] = ReportDiagnostic.Suppress, // Suppress unused field warning
                    ["CS0649"] = ReportDiagnostic.Suppress, // Suppress unassigned field warning
                    ["CS0219"] = ReportDiagnostic.Suppress, // Suppress assigned but unused variable warning
                }
            )
        );
        Assert.Empty(compilation.GetDiagnostics());
        return compilation;
    }
}
