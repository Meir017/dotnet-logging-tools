using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using LoggerUsage.Models;
using LoggerUsage.ParameterExtraction;
using LoggerUsage.MessageTemplate;
using LoggerUsage.Services;
using LoggerUsage.Utilities;

namespace LoggerUsage.Analyzers
{

    internal class LogMethodAnalyzer(
        ArrayParameterExtractor arrayParameterExtractor,
        IMessageTemplateExtractor messageTemplateExtractor,
        IDirectLogStateExtractor directLogStateExtractor,
        ILoggerFactory loggerFactory) : ILoggerUsageAnalyzer
    {
        private readonly ILogger<LogMethodAnalyzer> _logger = loggerFactory.CreateLogger<LogMethodAnalyzer>();
        
        public Task<IEnumerable<LoggerUsageInfo>> AnalyzeAsync(LoggingAnalysisContext context)
        {
            var results = new List<LoggerUsageInfo>();

            foreach (var operation in context.InvocationIndex.Invocations)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var isDirectLoggerMethod = IsDirectLoggerMethod(operation, context.LoggingTypes);
                if (!isDirectLoggerMethod &&
                    !context.LoggingTypes.LoggerExtensionModeler.IsLoggerMethod(operation.TargetMethod))
                {
                    continue;
                }

                results.Add(ExtractLoggerMethodUsage(
                    operation,
                    context.LoggingTypes,
                    (Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax)operation.Syntax,
                    isDirectLoggerMethod));
            }

            return Task.FromResult<IEnumerable<LoggerUsageInfo>>(results);
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
                    ? TryExtractDirectEventId(operation, loggingTypes, out var eventId)
                    : EventIdExtractor.TryExtractFromInvocation(operation, loggingTypes, out eventId)))
            {
                usage.EventId = eventId;
            }
            if (TryExtractLogLevel(operation, loggingTypes, out var logLevel))
            {
                usage.LogLevel = logLevel;
            }
            if (isDirectLoggerMethod)
            {
                ExtractDirectState(operation, loggingTypes, usage);
            }
            else if (TryExtractMessageTemplateFromArguments(operation, out var messageTemplate))
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

        private void ExtractDirectState(
            IInvocationOperation operation,
            LoggingTypes loggingTypes,
            LoggerUsageInfo usage)
        {
            var stateArgument = FindArgument(operation, loggingTypes.ILoggerLogStateParameter);
            if (stateArgument is null)
            {
                return;
            }

            if (messageTemplateExtractor.TryExtract(stateArgument.Value, out var constantState))
            {
                usage.MessageTemplate = constantState;
                return;
            }

            if (directLogStateExtractor.TryExtract(
                stateArgument.Value,
                loggingTypes,
                out var messageTemplate,
                out var messageParameters))
            {
                usage.MessageTemplate = messageTemplate;
                usage.MessageParameters = messageParameters;
            }
        }

        private bool TryExtractMessageTemplateFromArguments(
            IInvocationOperation operation,
            out string? messageTemplate)
        {
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

        private static bool IsDirectLoggerMethod(
            IInvocationOperation operation,
            LoggingTypes loggingTypes)
        {
            var method = operation.TargetMethod;
            if (method.Name != nameof(ILogger.Log))
            {
                return false;
            }

            var loggerMethod = loggingTypes.ILoggerLogMethod;
            if (SymbolEqualityComparer.Default.Equals(
                method.OriginalDefinition,
                loggerMethod.OriginalDefinition))
            {
                return true;
            }

            if (operation.Instance?.Type is not INamedTypeSymbol receiverType ||
                !receiverType.AllInterfaces.Any(interfaceType =>
                    SymbolEqualityComparer.Default.Equals(interfaceType, loggingTypes.ILogger)))
            {
                return false;
            }

            var implementation = receiverType.FindImplementationForInterfaceMember(loggerMethod) as IMethodSymbol;
            return implementation is not null &&
                SymbolEqualityComparer.Default.Equals(
                    method.OriginalDefinition,
                    implementation.OriginalDefinition);
        }

        private static bool TryExtractDirectEventId(
            IInvocationOperation operation,
            LoggingTypes loggingTypes,
            out EventIdBase eventId)
        {
            var eventIdArgument = FindArgument(operation, loggingTypes.ILoggerLogEventIdParameter);
            if (eventIdArgument != null)
            {
                return EventIdExtractor.TryExtractFromArgument(
                    eventIdArgument.Value,
                    loggingTypes,
                    out eventId);
            }

            eventId = default!;
            return false;
        }

        private static IArgumentOperation? FindArgument(
            IInvocationOperation operation,
            IParameterSymbol interfaceParameter) =>
            operation.Arguments.FirstOrDefault(
                argument => argument.Parameter?.Ordinal == interfaceParameter.Ordinal);

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
