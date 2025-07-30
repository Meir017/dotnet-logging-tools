using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using LoggerUsage.Models;
using LoggerUsage.ParameterExtraction;
using LoggerUsage.MessageTemplate;

namespace LoggerUsage.Analyzers
{

    internal class LogMethodAnalyzer(
        ArrayParameterExtractor arrayParameterExtractor,
        IMessageTemplateExtractor messageTemplateExtractor,
        ILoggerFactory loggerFactory) : ILoggerUsageAnalyzer
    {
        private readonly ILogger<LogMethodAnalyzer> _logger = loggerFactory.CreateLogger<LogMethodAnalyzer>();
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
                Location = LocationHelper.CreateFromInvocation(invocation),
            };

            if (EventIdExtractor.TryExtractFromInvocation(operation, loggingTypes, out var eventId))
            {
                usage.EventId = eventId;
            }
            if (TryExtractLogLevel(operation, loggingTypes, out var logLevel))
            {
                usage.LogLevel = logLevel;
            }
            if (TryExtractMessageTemplateFromArguments(operation, loggingTypes, out var messageTemplate))
            {
                usage.MessageTemplate = messageTemplate;
                if (arrayParameterExtractor.TryExtractParameters(operation, loggingTypes, messageTemplate, out var messageParameters))
                {
                    usage.MessageParameters = messageParameters;
                    _logger.LogDebug("Successfully analyzed logger method usage {MethodName} with {Count} parameters", 
                        usage.MethodName, messageParameters.Count);
                }
            }

            return usage;
        }

        private bool TryExtractMessageTemplateFromArguments(IInvocationOperation operation, LoggingTypes loggingTypes, out string messageTemplate)
        {
            int parameterStartIndex = operation.TargetMethod.IsExtensionMethod ? 1 : 0;
            for (var i = parameterStartIndex; i < operation.TargetMethod.Parameters.Length; i++)
            {
                if (messageTemplateExtractor.TryExtract(operation.Arguments[i], out messageTemplate))
                {
                    return true;
                }
            }

            messageTemplate = string.Empty;
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
    }
}
