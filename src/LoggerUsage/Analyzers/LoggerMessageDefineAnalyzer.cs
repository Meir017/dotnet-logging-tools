using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;
using Microsoft.Extensions.Logging;
using LoggerUsage.MessageTemplate;
using LoggerUsage.ParameterExtraction;

namespace LoggerUsage.Analyzers
{
    internal class LoggerMessageDefineAnalyzer(
        ILoggerFactory loggerFactory, 
        IMessageTemplateExtractor messageTemplateExtractor,
        GenericTypeParameterExtractor genericTypeParameterExtractor) : ILoggerUsageAnalyzer
    {
        private readonly ILogger<LoggerMessageDefineAnalyzer> _logger = loggerFactory.CreateLogger<LoggerMessageDefineAnalyzer>();

        public IEnumerable<LoggerUsageInfo> Analyze(LoggingTypes loggingTypes, SyntaxNode root, SemanticModel semanticModel)
        {
            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                if (semanticModel.GetOperation(invocation) is not IInvocationOperation operation)
                    continue;

                if (!operation.TargetMethod.ContainingType.Equals(loggingTypes.LoggerMessage, SymbolEqualityComparer.Default)
                    || !operation.TargetMethod.Name.Equals(nameof(LoggerMessage.Define)))
                    continue;

                yield return ExtractLoggerMessageDefineUsage(operation, loggingTypes, invocation);
            }
        }

        private LoggerUsageInfo ExtractLoggerMessageDefineUsage(IInvocationOperation operation, LoggingTypes loggingTypes, InvocationExpressionSyntax invocation)
        {
            var usage = new LoggerUsageInfo
            {
                MethodName = operation.TargetMethod.Name,
                MethodType = LoggerUsageMethodType.LoggerMessageDefine,
                Location = LocationHelper.CreateFromInvocation(invocation),
            };

            if (TryExtractLogLevel(operation, loggingTypes, out var logLevel))
            {
                usage.LogLevel = logLevel;
            }

            if (operation.Arguments.Length > 1 && 
                EventIdExtractor.TryExtractFromArgument(operation.Arguments[1].Value, out var eventId))
            {
                usage.EventId = eventId;
            }

            if (TryExtractMessageTemplateFromLoggerMessageDefine(operation, out var messageTemplate))
            {
                usage.MessageTemplate = messageTemplate;
                usage.MessageParameters = ExtractMessageParametersFromGenericTypes(operation, loggingTypes, messageTemplate);
            }

            return usage;
        }

        private static bool TryExtractLogLevel(IInvocationOperation operation, LoggingTypes loggingTypes, out LogLevel logLevel)
        {
            // LogLevel is typically the first parameter in LoggerMessage.Define
            if (operation.Arguments.Length > 0)
            {
                var logLevelArg = operation.Arguments[0].Value;
                if (logLevelArg is IFieldReferenceOperation fieldRef && fieldRef.ConstantValue.HasValue)
                {
                    logLevel = (LogLevel)fieldRef.ConstantValue.Value!;
                    return true;
                }
            }

            logLevel = default;
            return false;
        }
        private bool TryExtractMessageTemplateFromLoggerMessageDefine(IInvocationOperation operation, out string messageTemplate)
        {
            // Message template is typically the third parameter in LoggerMessage.Define
            if (operation.Arguments.Length > 2)
            {
                if (messageTemplateExtractor.TryExtract(operation.Arguments[2], out messageTemplate))
                {
                    return true;
                }
            }

            messageTemplate = string.Empty;
            return false;
        }

        private List<MessageParameter> ExtractMessageParametersFromGenericTypes(IInvocationOperation operation, LoggingTypes loggingTypes, string messageTemplate)
        {
            // Use injected GenericTypeParameterExtractor from the strategy pattern
            if (genericTypeParameterExtractor.TryExtractParameters(operation, loggingTypes, messageTemplate, out var parameters))
            {
                return parameters;
            }
            return new List<MessageParameter>();
        }
    }
}
