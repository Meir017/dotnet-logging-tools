using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;

namespace LoggerUsage
{
    public class LoggerUsageInfo
    {
        public required string MethodName { get; set; }
        public string? MessageTemplate { get; set; }
        public LogLevel? LogLevel { get; set; }
        public EventIdBase? EventId { get; set; }
        public List<MessageParameter> MessageParameters { get; set; } = new();
        public required MethodCallLocation Location { get; set; }
    }

    public class MethodCallLocation
    {
        public required int LineNumber { get; set; }
        public required int ColumnNumber { get; set; }
    }

    [JsonPolymorphic]
    [JsonDerivedType(typeof(EventIdDetails))]
    [JsonDerivedType(typeof(EventIdRef))]
    public abstract record class EventIdBase;

    public record class EventIdDetails(ConstantOrReference Id, ConstantOrReference Name) : EventIdBase;
    public record class EventIdRef(string Kind, string Name) : EventIdBase;

    public record ConstantOrReference(string Kind, object? Value)
    {
        public static ConstantOrReference Missing => new("Missing", null);
        public static ConstantOrReference Constant(object value) => new("Constant", value);
    }

    public class MessageParameter
    {
        public required string Name { get; set; }
        public string? Type { get; set; }
        public string? Value { get; set; }
    }

    public class LoggerUsageExtractor
    {
        public List<LoggerUsageInfo> ExtractLoggerUsages(CSharpCompilation compilation)
        {
            var loggerInterface = compilation.GetTypeByMetadataName(typeof(ILogger).FullName!)!;
            if (loggerInterface == null) return [];

            var loggingTypes = new LoggingTypes(compilation, loggerInterface);
            if (loggingTypes.ILogger == null) return [];

            var results = new List<LoggerUsageInfo>();
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var root = syntaxTree.GetRoot();
                var semanticModel = compilation.GetSemanticModel(syntaxTree);

                if (root == null || semanticModel == null) continue;

                var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var invocation in invocations)
                {
                    IMethodSymbol? method;
                    Optional<object?>[] constantValues;
                    if (semanticModel.GetOperation(invocation) is IInvocationOperation operation)
                    {
                        method = operation.TargetMethod;
                        constantValues = [.. operation.Arguments.Select(arg => arg.ConstantValue)];
                    }
                    else
                    {
                        (method, constantValues) = MethodSymbolHelper.GetMethodSymbol(invocation, semanticModel);
                    }

                    if (method == null) continue;

                    if (!loggingTypes.LoggerExtensionModeler.IsLoggerMethod(method)) continue;

                    var usage = new LoggerUsageInfo
                    {
                        MethodName = method.Name,
                        Location = new MethodCallLocation
                        {
                            LineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line,
                            ColumnNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Character,
                        },
                    };

                    if (TryExtractEventId(invocation, method, semanticModel, out var eventId))
                    {
                        usage.EventId = eventId;
                    }
                    if (TryExtractLogLevel(invocation, method, semanticModel, out var logLevel))
                    {
                        usage.LogLevel = logLevel;
                    }

                    // usage.MessageTemplate = ExtractMessageTemplate(operation);
                    // usage.MessageParameters = ExtractArguments(invocation, method, constantValues);

                    results.Add(usage);
                }
            }

            return results;
        }

        private static string? ExtractMessageTemplate(IInvocationOperation operation)
        {
            foreach (var arg in operation.Arguments)
            {
                if (arg.Parameter?.Name?.Equals("message") == true)
                {
                    return arg.Value.ConstantValue.HasValue ? arg.Value.ConstantValue.Value?.ToString() : null;
                }
            }
            return null;
        }

        private static bool TryExtractEventId(InvocationExpressionSyntax invocation, IMethodSymbol method, SemanticModel semanticModel, out EventIdBase eventId)
        {
            int parameterStartIndex = method.IsExtensionMethod ? 1 : 0;
            for (var i = parameterStartIndex; i < method.Parameters.Length; i++)
            {
                if (method.Parameters[i].Type?.Name == nameof(EventId))
                {
                    var argument = invocation.ArgumentList.Arguments[i - parameterStartIndex];
                    var argumentOperation = semanticModel.GetOperation(argument.Expression);
                    if (argumentOperation is null) continue;

                    // Attempt to create EventId from constructor arguments
                    if (argumentOperation is IObjectCreationOperation objectCreationOperation &&
                        objectCreationOperation.Type?.Name == nameof(EventId))
                    {
                        if (objectCreationOperation.Arguments.Length is 0) continue;

                        ConstantOrReference id = ConstantOrReference.Missing;
                        ConstantOrReference name = ConstantOrReference.Missing;

                        if (objectCreationOperation.Arguments.Length > 0)
                        {
                            if (objectCreationOperation.Arguments[0].Value.ConstantValue.Value is int idValue)
                            {
                                id = ConstantOrReference.Constant(idValue);
                            }
                            else
                            {
                                id = new ConstantOrReference(
                                    objectCreationOperation.Arguments[0].Value.Kind.ToString(),
                                    objectCreationOperation.Arguments[0].Value.Syntax.ToString()
                                );
                            }
                        }

                        if (objectCreationOperation.Arguments.Length > 1)
                        {
                            if (objectCreationOperation.Arguments[1].Value.ConstantValue.HasValue)
                            {
                                name = ConstantOrReference.Constant(objectCreationOperation.Arguments[1].Value.ConstantValue.Value!);
                            }
                            else
                            {
                                name = new ConstantOrReference(
                                    objectCreationOperation.Arguments[1].Value.Kind.ToString(),
                                    objectCreationOperation.Arguments[1].Value.Syntax.ToString()
                                );
                            }
                        }

                        eventId = new EventIdDetails(id, name);
                        return true;
                    }
                    else if (argumentOperation.Kind is OperationKind.DefaultValue)
                    {
                        continue;
                    }
                    else
                    {
                        eventId = new EventIdRef(
                            argumentOperation.Kind.ToString(),
                            argumentOperation.Syntax.ToString()
                        );
                        return true;
                    }
                }
            }

            eventId = default!;
            return false;
        }

        private static bool TryExtractLogLevel(InvocationExpressionSyntax invocation, IMethodSymbol method, SemanticModel semanticModel, out LogLevel logLevel)
        {
            return method.Name switch
            {
                nameof(ILogger.Log) => TryGetLogLevelFromArguments(invocation, method, semanticModel, out logLevel),
                nameof(LoggerExtensions.LogTrace) => ReturnLogLevel(LogLevel.Trace, out logLevel),
                nameof(LoggerExtensions.LogDebug) => ReturnLogLevel(LogLevel.Debug, out logLevel),
                nameof(LoggerExtensions.LogInformation) => ReturnLogLevel(LogLevel.Information, out logLevel),
                nameof(LoggerExtensions.LogWarning) => ReturnLogLevel(LogLevel.Warning, out logLevel),
                nameof(LoggerExtensions.LogError) => ReturnLogLevel(LogLevel.Error, out logLevel),
                nameof(LoggerExtensions.LogCritical) => ReturnLogLevel(LogLevel.Critical, out logLevel),
                _ => NotFound(out logLevel)
            };

            static bool TryGetLogLevelFromArguments(InvocationExpressionSyntax invocation, IMethodSymbol method, SemanticModel semanticModel, out LogLevel logLevel)
            {
                int parameterStartIndex = method.IsExtensionMethod ? 1 : 0;
                for (var i = parameterStartIndex; i < method.Parameters.Length; i++)
                {
                    if (method.Parameters[i].Type?.Name == nameof(LogLevel))
                    {
                        var argumentOperation = semanticModel.GetOperation(invocation.ArgumentList.Arguments[i - parameterStartIndex].Expression);
                        if (argumentOperation is not IFieldReferenceOperation fieldReferenceOperation) continue;

                        if (fieldReferenceOperation.ConstantValue.HasValue)
                        {
                            logLevel = (LogLevel)fieldReferenceOperation.ConstantValue.Value!;
                            return true;
                        }
                    }
                }

                logLevel = default;
                return false;
            }

            static bool ReturnLogLevel(LogLevel level, out LogLevel logLevel)
            {
                logLevel = level;
                return true;
            }

            static bool NotFound(out LogLevel logLevel)
            {
                logLevel = default;
                return false;
            }
        }

        private static List<MessageParameter> ExtractArguments(InvocationExpressionSyntax invocation, IMethodSymbol method, Optional<object?>[] constantValues)
        {
            var args = new List<MessageParameter>();
            var i = method.IsExtensionMethod ? 1 : 0;
            for (; i < method.Parameters.Length; i++)
            {
                var param = method.Parameters[i];
                if (param.Name != null && !param.Name.Equals("message") && param.Type?.Name != "EventId")
                {
                    args.Add(new MessageParameter
                    {
                        Name = param.Name,
                        Type = param.Type?.Name,
                        Value = constantValues.Length >= i && constantValues[i].HasValue ? constantValues[i].Value?.ToString() : null
                    });
                }
            }
            return args;
        }
    }
}
