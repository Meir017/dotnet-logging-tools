using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Tests;

public class ExtractLoggerUsagesTests
{
    private class LoggerUsageAnalyzerTest : AnalyzerTest<DefaultVerifier>
    {
        public override string Language => LanguageNames.CSharp;

        protected override string DefaultFileExt => "cs";

        protected override CompilationOptions CreateCompilationOptions()
            => new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);

        protected override ParseOptions CreateParseOptions()
            => new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Diagnose);

        protected override IEnumerable<DiagnosticAnalyzer> GetDiagnosticAnalyzers() => [];
    }

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

    private static async Task<CSharpCompilation> CreateCompilationAsync(string sourceCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, default);
        references = references.Add(MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location));
        var compilation = CSharpCompilation.Create("TestAssembly", [syntaxTree], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics());
        return compilation;
    }
}
