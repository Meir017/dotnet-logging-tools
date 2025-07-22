using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;

namespace LoggerUsage.Analyzers
{
    /// <summary>
    /// Handles extraction of parameters from different scope state types.
    /// </summary>
    internal static class ScopeParameterExtractor
    {
        private const int EXTENSION_METHOD_ARGUMENT_OFFSET = 1;
        private const int CORE_METHOD_ARGUMENT_OFFSET = 0;
        private const int PARAMS_ARGUMENT_INDEX = 2;

        /// <summary>
        /// Extracts parameters from extension method calls that use message templates.
        /// </summary>
        public static void ExtractMessageParameters(IInvocationOperation operation, LoggerUsageInfo usage)
        {
            var messageTemplate = usage.MessageTemplate;
            if (string.IsNullOrEmpty(messageTemplate))
                return;

            var formatter = new LogValuesFormatter(messageTemplate);
            if (formatter.ValueNames.Count == 0)
                return;

            var messageParameters = new List<MessageParameter>();

            // For extension methods, the params array is in argument index 2 (after 'this' and messageFormat)
            if (operation.Arguments.Length > PARAMS_ARGUMENT_INDEX)
            {
                var paramsArgument = operation.Arguments[PARAMS_ARGUMENT_INDEX].Value.UnwrapConversion();

                ExtractFromParamsArgument(paramsArgument, formatter, messageParameters);
            }

            usage.MessageParameters = messageParameters;
        }

        /// <summary>
        /// Extracts parameters from anonymous object creation operations.
        /// </summary>
        public static void ExtractAnonymousObjectProperties(IAnonymousObjectCreationOperation objectCreation, LoggerUsageInfo usage)
        {
            // Use AnonymousObjectParameterExtractor from the strategy pattern
            var extractor = new LoggerUsage.ParameterExtraction.AnonymousObjectParameterExtractor();
            if (extractor.TryExtractParameters(objectCreation, null!, null, out var parameters))
            {
                usage.MessageParameters ??= new List<MessageParameter>();
                usage.MessageParameters.AddRange(parameters);
            }
        }

        /// <summary>
        /// Gets the correct argument index based on whether the method is an extension method.
        /// </summary>
        public static int GetArgumentIndex(IInvocationOperation operation)
        {
            return operation.TargetMethod.IsExtensionMethod ? EXTENSION_METHOD_ARGUMENT_OFFSET : CORE_METHOD_ARGUMENT_OFFSET;
        }

        /// <summary>
        /// Creates a MessageParameter with consistent formatting.
        /// </summary>
        public static MessageParameter CreateMessageParameter(string name, string type, string kind)
        {
            return new MessageParameter(
                Name: name,
                Type: type ?? "object",
                Kind: kind
            );
        }

        private static void ExtractFromParamsArgument(IOperation paramsArgument, LogValuesFormatter formatter, List<MessageParameter> messageParameters)
        {
            // Check if this is an array creation with elements
            if (paramsArgument is IArrayCreationOperation arrayCreation && arrayCreation.Initializer != null)
            {
                ExtractFromArrayElements(arrayCreation.Initializer.ElementValues, formatter, messageParameters);
            }
            else
            {
                // Fallback: if not an array creation, treat as single parameter
                if (formatter.ValueNames.Count > 0)
                {
                    var parameter = CreateMessageParameter(
                        formatter.ValueNames[0],
                        paramsArgument.Type?.ToPrettyDisplayString() ?? "object",
                        paramsArgument.ConstantValue.HasValue ? "Constant" : paramsArgument.Kind.ToString()
                    );
                    messageParameters.Add(parameter);
                }
            }
        }

        private static void ExtractFromArrayElements(System.Collections.Immutable.ImmutableArray<IOperation> elementValues, LogValuesFormatter formatter, List<MessageParameter> messageParameters)
        {
            for (int i = 0; i < elementValues.Length && i < formatter.ValueNames.Count; i++)
            {
                var element = elementValues[i].UnwrapConversion();
                var parameterName = formatter.ValueNames[i];

                var parameter = CreateMessageParameter(
                    parameterName,
                    element.Type?.ToPrettyDisplayString() ?? "object",
                    element.ConstantValue.HasValue ? "Constant" : element.Kind.ToString()
                );

                messageParameters.Add(parameter);
            }
        }

        private static string? GetPropertyName(SyntaxNode syntax)
        {
            return syntax switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.Text,
                _ => null
            };
        }
    }
}
