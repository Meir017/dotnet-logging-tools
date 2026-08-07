using LoggerUsage.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Tests;

public class ModernCompatibilityTests
{
    [Fact]
    public async Task PrimaryConstructorLoggerMessage_IsExtracted()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            using Microsoft.Extensions.Logging;

            namespace TestNamespace;

            public partial class Worker(ILogger<Worker> logger)
            {
                [LoggerMessage(EventId = 9, Level = LogLevel.Information, Message = "Worker started")]
                private partial void LogStarted();

                private partial void LogStarted()
                {
                    _ = logger;
                }
            }
            """);
        AssertNoDiagnostics(compilation);

        var result = await TestUtils.CreateLoggerUsageExtractor()
            .ExtractLoggerUsagesWithSolutionAsync(compilation);

        var usage = Assert.Single(result.Results);
        Assert.Equal(LoggerUsageMethodType.LoggerMessageAttribute, usage.MethodType);
        Assert.Equal("LogStarted", usage.MethodName);
        Assert.Equal("Worker started", usage.MessageTemplate);
        Assert.Equal(LogLevel.Information, usage.LogLevel);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData(", Message = \"\"", "")]
    public async Task LoggerMessage_OmittedOrEmptyTemplate_IsExtracted(
        string messageArgument,
        string? expectedTemplate)
    {
        var compilation = await TestUtils.CreateCompilationAsync($$"""
            using Microsoft.Extensions.Logging;

            namespace TestNamespace;

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Warning{{messageArgument}})]
                public static partial void Write(ILogger logger);

                public static partial void Write(ILogger logger)
                {
                }
            }
            """);
        AssertNoDiagnostics(compilation);

        var result = await TestUtils.CreateLoggerUsageExtractor()
            .ExtractLoggerUsagesWithSolutionAsync(compilation);

        var usage = Assert.Single(result.Results);
        Assert.Equal(LoggerUsageMethodType.LoggerMessageAttribute, usage.MethodType);
        Assert.Equal(expectedTemplate, usage.MessageTemplate);
        Assert.Equal(LogLevel.Warning, usage.LogLevel);
    }

    [Fact]
    public async Task FrameworkLoggerCallInsideExtensionBlock_IsExtracted()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            using Microsoft.Extensions.Logging;

            namespace TestNamespace;

            public static class ModernLoggerExtensions
            {
                extension(ILogger logger)
                {
                    public void WriteModernLog()
                    {
                        logger.LogInformation("Modern extension block");
                    }
                }
            }
            """);
        AssertNoDiagnostics(compilation);

        var result = await TestUtils.CreateLoggerUsageExtractor()
            .ExtractLoggerUsagesWithSolutionAsync(compilation);

        var usage = Assert.Single(result.Results);
        Assert.Equal(LoggerUsageMethodType.LoggerExtensions, usage.MethodType);
        Assert.Equal("Modern extension block", usage.MessageTemplate);
        Assert.Equal(LogLevel.Information, usage.LogLevel);
    }

    [Fact]
    public async Task CustomReadOnlySpanParamsLoggingHelper_IsNotExtracted()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            using System;
            using Microsoft.Extensions.Logging;

            namespace TestNamespace;

            public static class CustomLoggingExtensions
            {
                public static void LogCustom(
                    this ILogger logger,
                    string message,
                    params ReadOnlySpan<object?> arguments)
                {
                }
            }

            public static class Consumer
            {
                public static void Run(ILogger logger)
                {
                    logger.LogCustom("Not a framework log {Value}", 42);
                }
            }
            """);
        AssertNoDiagnostics(compilation);

        var result = await TestUtils.CreateLoggerUsageExtractor()
            .ExtractLoggerUsagesWithSolutionAsync(compilation);

        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task UnboundGenericNameofLoggerMessageEventName_IsExtracted()
    {
        var compilation = await TestUtils.CreateCompilationAsync("""
            using System.Collections.Generic;
            using Microsoft.Extensions.Logging;

            namespace TestNamespace;

            public static partial class Log
            {
                private const string CollectionEvent = nameof(Dictionary<,>);

                [LoggerMessage(
                    EventId = 14,
                    EventName = CollectionEvent,
                    Level = LogLevel.Debug,
                    Message = "Collection event")]
                public static partial void Collection(ILogger logger);

                public static partial void Collection(ILogger logger)
                {
                }
            }
            """);
        AssertNoDiagnostics(compilation);

        var result = await TestUtils.CreateLoggerUsageExtractor()
            .ExtractLoggerUsagesWithSolutionAsync(compilation);

        var usage = Assert.Single(result.Results);
        var eventId = Assert.IsType<EventIdDetails>(usage.EventId);
        Assert.Equal(ConstantOrReference.Constant(14), eventId.Id);
        Assert.Equal(ConstantOrReference.Constant("Dictionary"), eventId.Name);
    }

    private static void AssertNoDiagnostics(Compilation compilation) =>
        Assert.Empty(compilation.GetDiagnostics());
}
