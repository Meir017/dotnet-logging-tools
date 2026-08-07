using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using LoggerUsage.Models;
using LoggerUsage.ParameterExtraction;
using LoggerUsage.MessageTemplate;
using LoggerUsage.Utilities;

namespace LoggerUsage.Analyzers
{

    internal class LogMethodAnalyzer(
        ArrayParameterExtractor arrayParameterExtractor,
        IMessageTemplateExtractor messageTemplateExtractor,
        ILoggerFactory loggerFactory) : ILoggerUsageAnalyzer
    {
        private readonly ILogger<LogMethodAnalyzer> _logger = loggerFactory.CreateLogger<LogMethodAnalyzer>();
        
        public async Task<IEnumerable<LoggerUsageInfo>> AnalyzeAsync(LoggingAnalysisContext context)
        {
            var results = new List<LoggerUsageInfo>();
            var invocations = context.Root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            
            foreach (var invocation in invocations)
            {
                if (context.SemanticModel.GetOperation(invocation) is not IInvocationOperation operation)
                {
                    continue;
                }

                var isDirectLoggerMethod = IsDirectLoggerMethod(operation.TargetMethod, context.LoggingTypes);
                if (!isDirectLoggerMethod &&
                    !context.LoggingTypes.LoggerExtensionModeler.IsLoggerMethod(operation.TargetMethod))
                {
                    continue;
                }

                results.Add(ExtractLoggerMethodUsage(
                    operation,
                    context.LoggingTypes,
                    invocation,
                    isDirectLoggerMethod));
            }
            
            // Ensure this is truly async
            await Task.Yield();
            return results;
        }

        private LoggerUsageInfo ExtractLoggerMethodUsage(
            IInvocationOperation operation,
            LoggingTypes loggingTypes,
            InvocationExpressionSyntax invocation,
            bool isDirectLoggerMethod)
        {
            var usage = new LoggerUsageInfo
            {
                MethodName = operation.TargetMethod.Name,
                MethodType = isDirectLoggerMethod
                    ? LoggerUsageMethodType.LoggerMethod
                    : LoggerUsageMethodType.LoggerExtensions,
                Location = LocationHelper.CreateFromInvocation(invocation),
            };

            if ((isDirectLoggerMethod
                    ? TryExtractDirectEventId(operation, out var eventId)
                    : EventIdExtractor.TryExtractFromInvocation(operation, loggingTypes, out eventId)))
            {
                usage.EventId = eventId;
            }
            if (TryExtractLogLevel(operation, loggingTypes, out var logLevel))
            {
                usage.LogLevel = logLevel;
            }
            if (TryExtractMessageTemplateFromArguments(operation, isDirectLoggerMethod, out var messageTemplate))
            {
                usage.MessageTemplate = messageTemplate;
                if (!isDirectLoggerMethod &&
                    arrayParameterExtractor.TryExtractParameters(operation, loggingTypes, messageTemplate, out var messageParameters))
                {
                    usage.MessageParameters = messageParameters;
                    _logger.LogDebug("Successfully analyzed logger method usage {MethodName} with {Count} parameters", 
                        usage.MethodName, messageParameters.Count);
                }
            }

            return usage;
        }

        private bool TryExtractMessageTemplateFromArguments(
            IInvocationOperation operation,
            bool isDirectLoggerMethod,
            out string? messageTemplate)
        {
            if (isDirectLoggerMethod)
            {
                var stateArgument = operation.Arguments.FirstOrDefault(
                    argument => argument.Parameter?.Name == "state");
                if (stateArgument != null &&
                    messageTemplateExtractor.TryExtract(stateArgument.Value, out messageTemplate))
                {
                    return true;
                }

                messageTemplate = null;
                return false;
            }

            int parameterStartIndex = operation.TargetMethod.IsExtensionMethod ? 1 : 0;
            for (var i = parameterStartIndex; i < operation.TargetMethod.Parameters.Length; i++)
            {
                if (messageTemplateExtractor.TryExtract(operation.Arguments[i], out messageTemplate))
                {
                    return true;
                }
            }

            messageTemplate = null;
            return false;
        }

        private static bool IsDirectLoggerMethod(IMethodSymbol method, LoggingTypes loggingTypes)
        {
            if (method.Name != nameof(ILogger.Log))
            {
                return false;
            }

            return loggingTypes.ILogger.GetMembers(nameof(ILogger.Log))
                .OfType<IMethodSymbol>()
                .Any(loggerMethod => SymbolEqualityComparer.Default.Equals(
                    method.OriginalDefinition,
                    loggerMethod.OriginalDefinition));
        }

        private static bool TryExtractDirectEventId(
            IInvocationOperation operation,
            out EventIdBase eventId)
        {
            var eventIdArgument = operation.Arguments.FirstOrDefault(
                argument => argument.Parameter?.Name == "eventId");
            if (eventIdArgument != null)
            {
                return EventIdExtractor.TryExtractFromArgument(eventIdArgument.Value, out eventId);
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
                foreach (var argument in operation.Arguments)
                {
                    if (argument.Parameter != null &&
                        loggingTypes.LogLevel.Equals(argument.Parameter.Type, SymbolEqualityComparer.Default))
                    {
                        var argumentOperation = argument.Value.UnwrapConversion();
                        if (argumentOperation.ConstantValue.HasValue)
                        {
                            logLevel = (LogLevel)argumentOperation.ConstantValue.Value!;
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
