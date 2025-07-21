using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;

namespace LoggerUsage.Analyzers
{
    /// <summary>
    /// Handles analysis of scope state and determines the appropriate parameter extraction strategy.
    /// </summary>
    internal static class ScopeStateAnalyzer
    {
        /// <summary>
        /// Extracts scope state information from the operation and populates the usage object.
        /// </summary>
        public static void ExtractScopeState(IInvocationOperation operation, LoggerUsageInfo usage, LoggingTypes loggingTypes)
        {
            var argumentIndex = ScopeParameterExtractor.GetArgumentIndex(operation);

            if (operation.Arguments.Length <= argumentIndex)
                return;

            var stateArgument = operation.Arguments[argumentIndex];

            // Extract message template from the state argument
            ExtractMessageTemplate(stateArgument, usage);

            // Extract message parameters based on the argument type and method type
            ExtractParameters(operation, stateArgument, usage, loggingTypes);
        }

        private static void ExtractMessageTemplate(IArgumentOperation stateArgument, LoggerUsageInfo usage)
        {
            if (stateArgument.Value is ILiteralOperation literal && literal.ConstantValue.HasValue)
            {
                usage.MessageTemplate = literal.ConstantValue.Value?.ToString();
            }
        }

        private static void ExtractParameters(IInvocationOperation operation, IArgumentOperation stateArgument, LoggerUsageInfo usage, LoggingTypes loggingTypes)
        {
            if (operation.TargetMethod.IsExtensionMethod && usage.MessageTemplate != null)
            {
                // Handle extension methods with message templates
                ScopeParameterExtractor.ExtractMessageParameters(operation, usage);
            }
            else if (!operation.TargetMethod.IsExtensionMethod)
            {
                // Handle core ILogger.BeginScope method
                ExtractCoreMethodParameters(stateArgument, usage, loggingTypes);
            }
        }

        private static void ExtractCoreMethodParameters(IArgumentOperation stateArgument, LoggerUsageInfo usage, LoggingTypes loggingTypes)
        {
            // Try to extract key-value pairs first
            if (KeyValuePairHandler.TryExtractKeyValuePairParameters(stateArgument, usage, loggingTypes))
            {
                return; // Successfully extracted key-value pairs
            }

            // Fallback to anonymous object extraction
            if (stateArgument.Value is IAnonymousObjectCreationOperation objectCreation)
            {
                ScopeParameterExtractor.ExtractAnonymousObjectProperties(objectCreation, usage);
            }
        }
    }
}
