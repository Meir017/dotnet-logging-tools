using AwesomeAssertions;
using LoggerUsage.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Tests;

public class DirectLoggerMethodTests
{
    [Fact]
    public async Task DirectLog_NamedArgumentsInReorderedSourceOrder_ExtractsCompleteUsage()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger)
                {
                    logger.Log<string>(
                        formatter: static (state, exception) => state,
                        state: "Named state",
                        exception: null,
                        eventId: new EventId(12, "NamedEvent"),
                        logLevel: LogLevel.Error);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Error,
            new EventIdDetails(ConstantOrReference.Constant(12), ConstantOrReference.Constant("NamedEvent")),
            "Named state");
    }

    [Fact]
    public async Task DirectLog_GenericILogger_ExtractsCompleteUsage()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            using Microsoft.Extensions.Logging;

            public class Category;

            public class TestClass
            {
                public void Test(ILogger<Category> logger)
                {
                    logger.Log(LogLevel.Information, new EventId(21), "Generic logger", null,
                        static (state, exception) => state);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Information,
            new EventIdDetails(ConstantOrReference.Constant(21), ConstantOrReference.Missing),
            "Generic logger");
    }

    [Fact]
    public async Task DirectLog_ExplicitGenericTypeArgument_ExtractsCompleteUsage()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger)
                {
                    logger.Log<string>(
                        LogLevel.Debug,
                        new EventId(22, "Explicit"),
                        "Explicit state",
                        null,
                        static (state, exception) => state);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Debug,
            new EventIdDetails(ConstantOrReference.Constant(22), ConstantOrReference.Constant("Explicit")),
            "Explicit state");
    }

    public static TheoryData<string, EventIdRef> EventIdReferences() => new()
    {
        { "localEventId", new EventIdRef(nameof(OperationKind.LocalReference), "localEventId") },
        { "eventId", new EventIdRef(nameof(OperationKind.ParameterReference), "eventId") },
        { "_eventId", new EventIdRef(nameof(OperationKind.FieldReference), "_eventId") },
    };

    [Theory]
    [MemberData(nameof(EventIdReferences))]
    public async Task DirectLog_EventIdReference_PreservesReference(string eventIdExpression, EventIdRef expectedEventId)
    {
        var compilation = await TestUtils.CreateCompilationAsync($$"""
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                private readonly EventId _eventId = new(31, "Field");

                public void Test(ILogger logger, EventId eventId)
                {
                    var localEventId = new EventId(32, "Local");
                    logger.Log(LogLevel.Warning, {{eventIdExpression}}, "Referenced event", null,
                        static (state, exception) => state);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Warning,
            expectedEventId,
            "Referenced event");
    }

    [Fact]
    public async Task DirectLog_ConstantLocalLogLevel_ExtractsLevel()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger)
                {
                    const LogLevel level = LogLevel.Critical;
                    logger.Log(level, new EventId(41), "Constant level", null,
                        static (state, exception) => state);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Critical,
            new EventIdDetails(ConstantOrReference.Constant(41), ConstantOrReference.Missing),
            "Constant level");
    }

    [Fact]
    public async Task DirectLog_RuntimeLogLevel_RemainsUnknownWithoutCrashing()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger, LogLevel level)
                {
                    logger.Log(level, new EventId(42), "Runtime level", null,
                        static (state, exception) => state);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            null,
            new EventIdDetails(ConstantOrReference.Constant(42), ConstantOrReference.Missing),
            "Runtime level");
    }

    [Fact]
    public async Task DirectLog_InlineStructuredState_ExtractsTemplateAndParametersExcludingOriginalFormat()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System;
            using System.Collections.Generic;
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger, string userId)
                {
                    var count = 3;
                    logger.Log(
                        LogLevel.Information,
                        new EventId(51, "StructuredArray"),
                        new KeyValuePair<string, object?>[]
                        {
                            new("UserId", userId),
                            new("Count", count),
                            new("{OriginalFormat}", "User {UserId} count {Count}")
                        },
                        null,
                        static (state, exception) => throw new InvalidOperationException("Formatter must not execute"));
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Information,
            new EventIdDetails(ConstantOrReference.Constant(51), ConstantOrReference.Constant("StructuredArray")),
            "User {UserId} count {Count}",
            [
                new MessageParameter("UserId", "string", nameof(OperationKind.ParameterReference)),
                new MessageParameter("Count", "int", nameof(OperationKind.LocalReference)),
            ]);
    }

    [Fact]
    public async Task DirectLog_ListStructuredState_ExtractsTemplateAndParametersExcludingOriginalFormat()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger, int orderId)
                {
                    logger.Log(
                        LogLevel.Information,
                        new EventId(52, "StructuredList"),
                        new List<KeyValuePair<string, object?>>
                        {
                            new("OrderId", orderId),
                            new("Status", "Created"),
                            new("{OriginalFormat}", "Order {OrderId} is {Status}")
                        },
                        null,
                        static (state, exception) => state.ToString()!);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Information,
            new EventIdDetails(ConstantOrReference.Constant(52), ConstantOrReference.Constant("StructuredList")),
            "Order {OrderId} is {Status}",
            [
                new MessageParameter("OrderId", "int", nameof(OperationKind.ParameterReference)),
                new MessageParameter("Status", "string", "Constant"),
            ]);
    }

    [Fact]
    public async Task DirectLog_CollectionExpressionState_ExtractsTemplateAndParameters()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger, int itemId)
                {
                    logger.Log<KeyValuePair<string, object?>[]>(LogLevel.Information, new EventId(53), [
                        new("ItemId", itemId),
                        new("{OriginalFormat}", "Item {ItemId}")
                    ], null, static (values, exception) => values.ToString()!);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Information,
            new EventIdDetails(ConstantOrReference.Constant(53), ConstantOrReference.Missing),
            "Item {ItemId}",
            [new MessageParameter("ItemId", "int", nameof(OperationKind.ParameterReference))]);
    }

    [Fact]
    public async Task DirectLog_NamedPairArguments_UsesParameterOrdinals()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger, int userId)
                {
                    logger.Log(LogLevel.Information, new EventId(54), new KeyValuePair<string, object?>[]
                    {
                        new(value: userId, key: "UserId"),
                        new(value: "User {UserId}", key: "{OriginalFormat}")
                    }, null, static (values, exception) => values.ToString()!);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Information,
            new EventIdDetails(ConstantOrReference.Constant(54), ConstantOrReference.Missing),
            "User {UserId}",
            [new MessageParameter("UserId", "int", nameof(OperationKind.ParameterReference))]);
    }

    [Fact]
    public async Task DirectLog_KeyValueStateWithoutOriginalFormat_RemainsPartial()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger, int value)
                {
                    logger.Log(LogLevel.Information, new EventId(55), new KeyValuePair<string, object?>[]
                    {
                        new("Value", value)
                    }, null, static (values, exception) => $"Value: {values[0].Value}");
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Information,
            new EventIdDetails(ConstantOrReference.Constant(55), ConstantOrReference.Missing),
            null);
    }

    [Fact]
    public async Task DirectLog_IncompatibleKeyValuePairState_RemainsPartial()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger)
                {
                    logger.Log(LogLevel.Information, new EventId(56), new KeyValuePair<string, string>[]
                    {
                        new("{OriginalFormat}", "Not structured logger state")
                    }, null, static (values, exception) => values[0].Value);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Information,
            new EventIdDetails(ConstantOrReference.Constant(56), ConstantOrReference.Missing),
            null);
    }

    [Fact]
    public async Task DirectLog_ExplicitValueConversion_PreservesConvertedType()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger, int count)
                {
                    logger.Log(LogLevel.Information, new EventId(57), new KeyValuePair<string, object?>[]
                    {
                        new("Count", (long)count),
                        new("{OriginalFormat}", "Count {Count}")
                    }, null, static (values, exception) => values.ToString()!);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Information,
            new EventIdDetails(ConstantOrReference.Constant(57), ConstantOrReference.Missing),
            "Count {Count}",
            [new MessageParameter("Count", "long", nameof(OperationKind.Conversion))]);
    }

    [Fact]
    public async Task DirectLog_StructuredState_UsesTemplateOrderAndIgnoresUnreferencedMetadata()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger, int first, string second)
                {
                    logger.Log(LogLevel.Information, new EventId(58), new KeyValuePair<string, object?>[]
                    {
                        new("Second", second),
                        new("Metadata", 42),
                        new("First", first),
                        new("{OriginalFormat}", "{First} then {Second}")
                    }, null, static (values, exception) => values.ToString()!);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Information,
            new EventIdDetails(ConstantOrReference.Constant(58), ConstantOrReference.Missing),
            "{First} then {Second}",
            [
                new MessageParameter("First", "int", nameof(OperationKind.ParameterReference)),
                new MessageParameter("Second", "string", nameof(OperationKind.ParameterReference)),
            ]);
    }

    [Fact]
    public async Task DirectLog_StructuredStateMissingTemplateValue_RemainsPartial()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger)
                {
                    logger.Log(LogLevel.Information, new EventId(59), new KeyValuePair<string, object?>[]
                    {
                        new("{OriginalFormat}", "Missing {Value}")
                    }, null, static (values, exception) => values.ToString()!);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Information,
            new EventIdDetails(ConstantOrReference.Constant(59), ConstantOrReference.Missing),
            null);
    }

    [Fact]
    public async Task DirectLog_StructuredStateKeyCaseMismatch_RemainsPartial()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger, int userId)
                {
                    logger.Log(LogLevel.Information, new EventId(60), new KeyValuePair<string, object?>[]
                    {
                        new("userid", userId),
                        new("{OriginalFormat}", "User {UserId}")
                    }, null, static (values, exception) => values.ToString()!);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Information,
            new EventIdDetails(ConstantOrReference.Constant(60), ConstantOrReference.Missing),
            null);
    }

    [Fact]
    public async Task DirectLog_DuplicateOriginalFormatEntries_RemainsPartial()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger, string runtimeFormat)
                {
                    logger.Log(LogLevel.Information, new EventId(62), new KeyValuePair<string, object?>[]
                    {
                        new("{OriginalFormat}", "Constant format"),
                        new("{OriginalFormat}", runtimeFormat)
                    }, null, static (values, exception) => values.ToString()!);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Information,
            new EventIdDetails(ConstantOrReference.Constant(62), ConstantOrReference.Missing),
            null);
    }

    [Fact]
    public async Task DirectLog_InterfaceTargetedCollectionExpression_ExtractsStructuredState()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger, string userId)
                {
                    logger.Log<IEnumerable<KeyValuePair<string, object?>>>(LogLevel.Information, new EventId(63), [
                        new("UserId", userId),
                        new("{OriginalFormat}", "User {UserId}")
                    ], null, static (values, exception) => values.ToString()!);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Information,
            new EventIdDetails(ConstantOrReference.Constant(63), ConstantOrReference.Missing),
            "User {UserId}",
            [new MessageParameter("UserId", "string", nameof(OperationKind.ParameterReference))]);
    }

    [Fact]
    public async Task DirectLog_NullableReferenceConversion_PreservesNullableType()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                public void Test(ILogger logger, string value)
                {
                    logger.Log(LogLevel.Information, new EventId(64), new KeyValuePair<string, object?>[]
                    {
                        new("Value", (string?)value),
                        new("{OriginalFormat}", "Value {Value}")
                    }, null, static (values, exception) => values.ToString()!);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Information,
            new EventIdDetails(ConstantOrReference.Constant(64), ConstantOrReference.Missing),
            "Value {Value}",
            [new MessageParameter("Value", "string?", nameof(OperationKind.Conversion))]);
    }

    [Fact]
    public async Task DirectLog_CustomStateWithFormatter_ReturnsPartialUsageWithoutExecutingFormatter()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System;
            using Microsoft.Extensions.Logging;

            public sealed record CustomState(int Value);

            public class TestClass
            {
                public void Test(ILogger logger, CustomState state)
                {
                    logger.Log(
                        LogLevel.Trace,
                        new EventId(61, "Custom"),
                        state,
                        null,
                        static (value, exception) => throw new InvalidOperationException("Formatter must not execute"));
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Trace,
            new EventIdDetails(ConstantOrReference.Constant(61), ConstantOrReference.Constant("Custom")),
            null);
    }

    [Fact]
    public async Task DirectLog_ConcreteILoggerImplementation_IsRecognized()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System;
            using Microsoft.Extensions.Logging;

            public sealed class ConcreteLogger : ILogger
            {
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
                public bool IsEnabled(LogLevel logLevel) => true;
                public void Log<TState>(
                    LogLevel logLevel,
                    EventId eventId,
                    TState state,
                    Exception? exception,
                    Func<TState, Exception?, string> formatter)
                {
                }
            }

            public class TestClass
            {
                public void Test(ConcreteLogger logger)
                {
                    logger.Log(LogLevel.Error, new EventId(71, "Concrete"), "Concrete logger", null,
                        static (state, exception) => state);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        AssertCompleteUsage(
            result.Results.Should().ContainSingle().Which,
            LogLevel.Error,
            new EventIdDetails(ConstantOrReference.Constant(71), ConstantOrReference.Constant("Concrete")),
            "Concrete logger");
    }

    [Fact]
    public async Task DirectLog_SameSignatureClassNotImplementingILogger_IsIgnored()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            #nullable enable
            using System;
            using Microsoft.Extensions.Logging;

            public sealed class LoggerLookalike
            {
                public void Log<TState>(
                    LogLevel logLevel,
                    EventId eventId,
                    TState state,
                    Exception? exception,
                    Func<TState, Exception?, string> formatter)
                {
                }
            }

            public class TestClass
            {
                public void Test(LoggerLookalike logger)
                {
                    logger.Log(LogLevel.Error, new EventId(72), "Not an ILogger", null,
                        static (state, exception) => state);
                }
            }
            """);
        AssertNoCompilationDiagnostics(compilation);

        var result = await ExtractAsync(compilation);

        result.Results.Should().BeEmpty();
    }

    private static async Task<LoggerUsageExtractionResult> ExtractAsync(Compilation compilation) =>
        await TestUtils.CreateLoggerUsageExtractor().ExtractLoggerUsagesWithSolutionAsync(compilation);

    private static void AssertNoCompilationDiagnostics(Compilation compilation) =>
        compilation.GetDiagnostics().Should().BeEmpty();

    private static void AssertCompleteUsage(
        LoggerUsageInfo usage,
        LogLevel? expectedLogLevel,
        EventIdBase? expectedEventId,
        string? expectedTemplate,
        IReadOnlyList<MessageParameter>? expectedParameters = null)
    {
        usage.MethodName.Should().Be(nameof(ILogger.Log));
        usage.MethodType.Should().Be(LoggerUsageMethodType.LoggerMethod);
        usage.LogLevel.Should().Be(expectedLogLevel);
        usage.EventId.Should().Be(expectedEventId);
        usage.MessageTemplate.Should().Be(expectedTemplate);
        usage.MessageParameters.Should().Equal(expectedParameters ?? []);
        usage.Location.FilePath.Should().BeEmpty();
        usage.Location.StartLineNumber.Should().BeGreaterThan(0);
        usage.Location.EndLineNumber.Should().BeGreaterThanOrEqualTo(usage.Location.StartLineNumber);
    }
}
