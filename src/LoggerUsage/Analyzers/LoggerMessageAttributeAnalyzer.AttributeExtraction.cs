using Microsoft.CodeAnalysis;
using LoggerUsage.Models;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Analyzers
{
    // Partial class containing attribute extraction functionality
    internal partial class LoggerMessageAttributeAnalyzer
    {
        /// <summary>
        /// Attempts to extract EventId information from a LoggerMessage attribute
        /// </summary>
        private static bool TryExtractEventId(
            AttributeData attribute,
            IMethodSymbol methodSymbol,
            LoggingTypes loggingTypes,
            out EventIdDetails eventIdDetails)
        {
            string? eventName = null;
            int? eventId = null;

            // Check named arguments for EventId and EventName
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == nameof(LoggerMessageAttribute.EventId))
                {
                    if (namedArg.Value.Value is int eventIdValue)
                    {
                        eventId = eventIdValue;
                    }
                }
                if (namedArg.Key == nameof(LoggerMessageAttribute.EventName))
                {
                    if (namedArg.Value.Value is string eventNameValue)
                    {
                        eventName = eventNameValue;
                    }
                }
            }

            // Check constructor arguments for EventId (3-parameter constructor)
            if (eventId is null && attribute.ConstructorArguments is { Length: 3 })
            {
                var eventIdArg = attribute.ConstructorArguments[0];
                if (eventIdArg.Value is int eventIdValue)
                {
                    eventId = eventIdValue;
                }
            }

            // Create EventIdDetails based on what we found
            eventIdDetails = (eventName, eventId) switch
            {
                (null, int id) => new EventIdDetails(ConstantOrReference.Constant(id), ConstantOrReference.Missing),
                (string name, null) => new EventIdDetails(ConstantOrReference.Missing, ConstantOrReference.Constant(name)),
                (string name, int id) => new EventIdDetails(ConstantOrReference.Constant(id), ConstantOrReference.Constant(name)),
                (null, null) => null!
            };

            return eventIdDetails is not null;
        }

        /// <summary>
        /// Attempts to extract LogLevel from a LoggerMessage attribute
        /// </summary>
        private static bool TryExtractLogLevel(
            AttributeData attribute,
            LoggingTypes loggingTypes,
            out LogLevel? logLevel)
        {
            // Check named argument for Level property
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == nameof(LoggerMessageAttribute.Level))
                {
                    logLevel = (LogLevel)namedArg.Value.Value!;
                    return true;
                }
            }

            // Check constructor arguments:
            // 1-parameter: (LogLevel level)
            // 2-parameter: (LogLevel level, string message)
            if (attribute.ConstructorArguments is { Length: 1 } or { Length: 2 } &&
                loggingTypes.LogLevel.Equals(attribute.ConstructorArguments[0].Type, SymbolEqualityComparer.Default))
            {
                logLevel = (LogLevel)attribute.ConstructorArguments[0].Value!;
                return true;
            }

            // 3-parameter: (int eventId, LogLevel level, string message)
            if (attribute.ConstructorArguments is { Length: 3 } &&
                loggingTypes.LogLevel.Equals(attribute.ConstructorArguments[1].Type, SymbolEqualityComparer.Default))
            {
                logLevel = (LogLevel)attribute.ConstructorArguments[1].Value!;
                return true;
            }

            logLevel = null;
            return false;
        }

        /// <summary>
        /// Attempts to extract the message template from a LoggerMessage attribute
        /// </summary>
        private static bool TryExtractMessageTemplate(
            AttributeData attribute,
            LoggingTypes loggingTypes,
            out string messageTemplate)
        {
            // Check named argument for Message property
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == nameof(LoggerMessageAttribute.Message))
                {
                    messageTemplate = (string)namedArg.Value.Value!;
                    return true;
                }
            }

            // Check constructor arguments:
            // 1-parameter: (string message)
            if (attribute.ConstructorArguments is { Length: 1 } &&
                SpecialType.System_String.Equals(attribute.ConstructorArguments[0].Type?.SpecialType))
            {
                messageTemplate = (string)attribute.ConstructorArguments[0].Value!;
                return true;
            }

            // 2-parameter: (LogLevel level, string message)
            if (attribute.ConstructorArguments is { Length: 2 } &&
                SpecialType.System_String.Equals(attribute.ConstructorArguments[1].Type?.SpecialType))
            {
                messageTemplate = (string)attribute.ConstructorArguments[1].Value!;
                return true;
            }

            // 3-parameter: (int eventId, LogLevel level, string message)
            if (attribute.ConstructorArguments is { Length: 3 } &&
                SpecialType.System_String.Equals(attribute.ConstructorArguments[2].Type?.SpecialType))
            {
                messageTemplate = (string)attribute.ConstructorArguments[2].Value!;
                return true;
            }

            messageTemplate = string.Empty;
            return false;
        }

        /// <summary>
        /// Attempts to extract message parameters from the method signature
        /// </summary>
        private static bool TryExtractMessageParameters(
            AttributeData attribute,
            LoggingTypes loggingTypes,
            IMethodSymbol methodSymbol,
            string messageTemplate,
            out List<MessageParameter> messageParameters)
        {
            // Use MethodSignatureParameterExtractor from the strategy pattern
            return ParameterExtraction.MethodSignatureParameterExtractor.TryExtractFromMethodSignature(
                methodSymbol, messageTemplate, loggingTypes, out messageParameters);
        }
    }
}
