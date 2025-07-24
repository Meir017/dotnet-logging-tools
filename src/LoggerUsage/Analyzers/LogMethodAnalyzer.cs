using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;
using Microsoft.Extensions.Logging;
using LoggerUsage.ParameterExtraction;

namespace LoggerUsage.Analyzers
{

    internal class LogMethodAnalyzer(ArrayParameterExtractor arrayParameterExtractor) : ILoggerUsageAnalyzer
    {
        public IEnumerable<LoggerUsageInfo> Analyze(LoggingTypes loggingTypes, SyntaxNode root, SemanticModel semanticModel)
        {
            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                if (semanticModel.GetOperation(invocation) is not IInvocationOperation operation)
                    continue;

                if (!loggingTypes.LoggerExtensionModeler.IsLoggerMethod(operation.TargetMethod))
                    continue;

                yield return ExtractLoggerMethodUsage(operation, loggingTypes, invocation);
            }
        }

        private LoggerUsageInfo ExtractLoggerMethodUsage(IInvocationOperation operation, LoggingTypes loggingTypes, InvocationExpressionSyntax invocation)
        {
            var usage = new LoggerUsageInfo
            {
                MethodName = operation.TargetMethod.Name,
                MethodType = LoggerUsageMethodType.LoggerExtensions,
                Location = new MethodCallLocation
                {
                    StartLineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line,
                    EndLineNumber = invocation.GetLocation().GetLineSpan().EndLinePosition.Line,
                    FilePath = invocation.GetLocation().SourceTree!.FilePath
                },
            };

            if (TryExtractEventId(operation, loggingTypes, out var eventId))
            {
                usage.EventId = eventId;
            }
            if (TryExtractLogLevel(operation, loggingTypes, out var logLevel))
            {
                usage.LogLevel = logLevel;
            }
            if (TryExtractMessageTemplate(operation, loggingTypes, out var messageTemplate))
            {
                usage.MessageTemplate = messageTemplate;
                if (arrayParameterExtractor.TryExtractParameters(operation, loggingTypes, messageTemplate, out var messageParameters))
                {
                    usage.MessageParameters = messageParameters;
                }
            }

            return usage;
        }

        private static bool TryExtractEventId(IInvocationOperation operation, LoggingTypes loggingTypes, out EventIdBase eventId)
        {
            int parameterStartIndex = operation.TargetMethod.IsExtensionMethod ? 1 : 0;
            for (var i = parameterStartIndex; i < operation.TargetMethod.Parameters.Length; i++)
            {
                if (!loggingTypes.EventId.Equals(operation.Arguments[i].Value.Type, SymbolEqualityComparer.Default))
                {
                    continue;
                }

                var argumentOperation = operation.Arguments[i].Value.UnwrapConversion();

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
                else if (argumentOperation is ILiteralOperation literalOperation)
                {
                    if (literalOperation.ConstantValue.HasValue)
                    {
                        eventId = new EventIdDetails(ConstantOrReference.Constant(literalOperation.ConstantValue.Value!), ConstantOrReference.Missing);
                        return true;
                    }
                    else
                    {
                        eventId = new EventIdRef(
                            literalOperation.Kind.ToString(),
                            literalOperation.Syntax.ToString()
                        );
                        return true;
                    }
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

            eventId = default!;
            return false;
        }

        private static bool TryExtractLogLevel(IInvocationOperation operation, LoggingTypes loggingTypes, out LogLevel logLevel)
        {
            return operation.TargetMethod.Name switch
            {
                nameof(ILogger.Log) => TryGetLogLevelFromArguments(operation, loggingTypes, out logLevel),
                nameof(LoggerExtensions.LogTrace) => ReturnLogLevel(LogLevel.Trace, out logLevel),
                nameof(LoggerExtensions.LogDebug) => ReturnLogLevel(LogLevel.Debug, out logLevel),
                nameof(LoggerExtensions.LogInformation) => ReturnLogLevel(LogLevel.Information, out logLevel),
                nameof(LoggerExtensions.LogWarning) => ReturnLogLevel(LogLevel.Warning, out logLevel),
                nameof(LoggerExtensions.LogError) => ReturnLogLevel(LogLevel.Error, out logLevel),
                nameof(LoggerExtensions.LogCritical) => ReturnLogLevel(LogLevel.Critical, out logLevel),
                _ => NotFound(out logLevel)
            };

            static bool TryGetLogLevelFromArguments(IInvocationOperation operation, LoggingTypes loggingTypes, out LogLevel logLevel)
            {
                int parameterStartIndex = operation.TargetMethod.IsExtensionMethod ? 1 : 0;
                for (var i = parameterStartIndex; i < operation.TargetMethod.Parameters.Length; i++)
                {
                    if (loggingTypes.LogLevel.Equals(operation.TargetMethod.Parameters[i].Type, SymbolEqualityComparer.Default))
                    {
                        var argumentOperation = operation.Arguments[i].Value;
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

        private static bool TryExtractMessageTemplate(IInvocationOperation operation, LoggingTypes loggingTypes, out string messageTemplate)
        {
            int parameterStartIndex = operation.TargetMethod.IsExtensionMethod ? 1 : 0;
            for (var i = parameterStartIndex; i < operation.TargetMethod.Parameters.Length; i++)
            {
                if (operation.Arguments[i].Value.Type?.SpecialType is SpecialType.System_String)
                {
                    var argumentOperation = operation.Arguments[i].Value;
                    if (argumentOperation.ConstantValue.HasValue)
                    {
                        messageTemplate = argumentOperation.ConstantValue.Value?.ToString() ?? string.Empty;
                        return true;
                    }
                }
            }

            messageTemplate = string.Empty;
            return false;
        }
    }
}
