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

            if (TryExtractEventId(operation, loggingTypes, out var eventId))
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
        private static bool TryExtractEventId(IInvocationOperation operation, LoggingTypes loggingTypes, out EventIdBase eventId)
        {
            // EventId is typically the second parameter in LoggerMessage.Define
            if (operation.Arguments.Length > 1)
            {
                var eventIdArg = operation.Arguments[1].Value.UnwrapConversion();

                // Handle EventId constructor
                if (eventIdArg is IObjectCreationOperation objectCreation &&
                    objectCreation.Type?.Name == nameof(EventId))
                {
                    ConstantOrReference id = ConstantOrReference.Missing;
                    ConstantOrReference name = ConstantOrReference.Missing;

                    if (objectCreation.Arguments.Length > 0)
                    {
                        if (objectCreation.Arguments[0].Value.ConstantValue.Value is int idValue)
                        {
                            id = ConstantOrReference.Constant(idValue);
                        }
                        else
                        {
                            id = new ConstantOrReference(
                                objectCreation.Arguments[0].Value.Kind.ToString(),
                                objectCreation.Arguments[0].Value.Syntax.ToString()
                            );
                        }
                    }
                    if (objectCreation.Arguments.Length > 1)
                    {
                        if (objectCreation.Arguments[1].Value.ConstantValue.HasValue)
                        {
                            var nameValue = objectCreation.Arguments[1].Value.ConstantValue.Value;
                            if (nameValue != null)
                            {
                                name = ConstantOrReference.Constant(nameValue);
                            }
                        }
                        else
                        {
                            name = new ConstantOrReference(
                                objectCreation.Arguments[1].Value.Kind.ToString(),
                                objectCreation.Arguments[1].Value.Syntax.ToString()
                            );
                        }
                    }

                    eventId = new EventIdDetails(id, name);
                    return true;
                }
                else if (eventIdArg.Kind is OperationKind.DefaultValue)
                {
                    eventId = default!;
                    return false;
                }
                else if (eventIdArg is ILiteralOperation literalOperation)
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
                // Handle direct EventId value or reference
                else if (eventIdArg.ConstantValue.HasValue)
                {
                    eventId = new EventIdDetails(ConstantOrReference.Constant(eventIdArg.ConstantValue.Value!), ConstantOrReference.Missing);
                    return true;
                }
                else
                {
                    eventId = new EventIdRef(
                        eventIdArg.Kind.ToString(),
                        eventIdArg.Syntax.ToString()
                    );
                    return true;
                }
            }

            eventId = default!;
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
