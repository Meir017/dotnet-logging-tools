using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using LoggerUsage.Models;

namespace LoggerUsage.Services
{
    /// <summary>
    /// Implementation of parameter extraction service that consolidates parameter extraction logic.
    /// </summary>
    public class ParameterExtractionService : IParameterExtractionService
    {
        private const int EXTENSION_METHOD_ARGUMENT_OFFSET = 1;
        private const int CORE_METHOD_ARGUMENT_OFFSET = 0;
        private const int PARAMS_ARGUMENT_INDEX = 2;

        private readonly ILogger<ParameterExtractionService> _logger;

        public ParameterExtractionService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ParameterExtractionService>();
        }

        public List<MessageParameter> ExtractFromMessageTemplate(IInvocationOperation operation, string template)
        {
            try
            {
                _logger.LogDebug("Extracting parameters from template: {Template} in {Method}", 
                    template, operation.TargetMethod.Name);

                if (string.IsNullOrEmpty(template))
                    return new List<MessageParameter>();

                var formatter = new LogValuesFormatter(template);
                if (formatter.ValueNames.Count == 0)
                    return new List<MessageParameter>();

                var messageParameters = new List<MessageParameter>();

                // For extension methods, the params array is in argument index 2 (after 'this' and messageFormat)
                if (operation.Arguments.Length > PARAMS_ARGUMENT_INDEX)
                {
                    var paramsArgument = operation.Arguments[PARAMS_ARGUMENT_INDEX].Value.UnwrapConversion();
                    ExtractFromParamsArgument(paramsArgument, formatter, messageParameters);
                }

                _logger.LogDebug("Successfully extracted {Count} parameters from template", messageParameters.Count);
                return messageParameters;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract parameters from template: {Template} in {Method}", 
                    template, operation.TargetMethod.Name);
                return new List<MessageParameter>();
            }
        }

        public List<MessageParameter> ExtractFromAnonymousObject(IAnonymousObjectCreationOperation operation)
        {
            try
            {
                _logger.LogDebug("Extracting parameters from anonymous object");

                var messageParameters = new List<MessageParameter>();

                foreach (var property in operation.Initializers)
                {
                    if (property is not ISimpleAssignmentOperation assignment)
                        continue;

                    var propertyName = GetPropertyName(assignment.Target.Syntax);
                    if (propertyName == null)
                        continue;

                    var parameter = CreateMessageParameter(
                        propertyName,
                        assignment.Value.Type?.ToPrettyDisplayString() ?? "object",
                        assignment.Value.ConstantValue.HasValue ? "Constant" : assignment.Value.Kind.ToString()
                    );

                    messageParameters.Add(parameter);
                }

                _logger.LogDebug("Successfully extracted {Count} parameters from anonymous object", messageParameters.Count);
                return messageParameters;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract parameters from anonymous object");
                return new List<MessageParameter>();
            }
        }

        public int GetArgumentIndex(IInvocationOperation operation)
        {
            return operation.TargetMethod.IsExtensionMethod ? EXTENSION_METHOD_ARGUMENT_OFFSET : CORE_METHOD_ARGUMENT_OFFSET;
        }

        public MessageParameter CreateMessageParameter(string name, string type, string kind)
        {
            return new MessageParameter(
                Name: name,
                Type: type ?? "object",
                Kind: kind
            );
        }

        private void ExtractFromParamsArgument(IOperation paramsArgument, LogValuesFormatter formatter, List<MessageParameter> messageParameters)
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

        private void ExtractFromArrayElements(System.Collections.Immutable.ImmutableArray<IOperation> elementValues, LogValuesFormatter formatter, List<MessageParameter> messageParameters)
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
