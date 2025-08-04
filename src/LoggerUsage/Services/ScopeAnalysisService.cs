using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using LoggerUsage.Models;
using LoggerUsage.ParameterExtraction;
using LoggerUsage.Utilities;

namespace LoggerUsage.Services
{
    /// <summary>
    /// Implementation of scope analysis service that consolidates scope state extraction logic.
    /// </summary>
    internal class ScopeAnalysisService : IScopeAnalysisService
    {
        private readonly IKeyValuePairExtractionService _keyValuePairExtractionService;
        private readonly AnonymousObjectParameterExtractor _anonymousObjectParameterExtractor;
        private readonly ILogger<ScopeAnalysisService> _logger;

        public ScopeAnalysisService(
            IKeyValuePairExtractionService keyValuePairExtractionService,
            AnonymousObjectParameterExtractor anonymousObjectParameterExtractor,
            ILoggerFactory loggerFactory)
        {
            _keyValuePairExtractionService = keyValuePairExtractionService;
            _anonymousObjectParameterExtractor = anonymousObjectParameterExtractor;
            _logger = loggerFactory.CreateLogger<ScopeAnalysisService>();
        }

        public ScopeAnalysisResult AnalyzeScopeState(IInvocationOperation operation, LoggingTypes loggingTypes)
        {
            try
            {
                _logger.LogDebug("Analyzing scope state for method: {Method}", operation.TargetMethod.Name);

                var argumentIndex = GetArgumentIndex(operation);

                if (operation.Arguments.Length <= argumentIndex)
                {
                    _logger.LogDebug("No arguments found at index {Index} for method {Method}", argumentIndex, operation.TargetMethod.Name);
                    return ScopeAnalysisResult.Success(null, [], operation.TargetMethod.IsExtensionMethod);
                }

                var stateArgument = operation.Arguments[argumentIndex];

                // Extract message template from the state argument
                var messageTemplate = ExtractMessageTemplate(stateArgument);

                // Extract message parameters based on the argument type and method type
                var parameters = ExtractParameters(operation, stateArgument, messageTemplate, loggingTypes);

                _logger.LogDebug("Successfully analyzed scope state. Template: {Template}, Parameters: {Count}",
                    messageTemplate, parameters.Count);

                return ScopeAnalysisResult.Success(messageTemplate, parameters, operation.TargetMethod.IsExtensionMethod);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze scope state for method: {Method}", operation.TargetMethod.Name);
                return ScopeAnalysisResult.Failure($"Analysis failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the correct argument index based on whether the method is an extension method.
        /// </summary>
        private int GetArgumentIndex(IInvocationOperation operation)
        {
            return operation.TargetMethod.IsExtensionMethod ? 1 : 0;
        }

        /// <summary>
        /// Extracts the message template from the state argument if it's a literal value.
        /// </summary>
        private string? ExtractMessageTemplate(IArgumentOperation stateArgument)
        {
            if (stateArgument.Value is ILiteralOperation literal && literal.ConstantValue.HasValue)
            {
                return literal.ConstantValue.Value?.ToString();
            }

            return null;
        }

        /// <summary>
        /// Determines the parameter extraction strategy based on method type and argument content.
        /// </summary>
        private List<MessageParameter> ExtractParameters(IInvocationOperation operation, IArgumentOperation stateArgument, string? messageTemplate, LoggingTypes loggingTypes)
        {
            if (operation.TargetMethod.IsExtensionMethod && messageTemplate != null)
            {
                // Handle extension methods with message templates
                return ExtractExtensionMethodParameters(operation, messageTemplate);
            }
            else if (!operation.TargetMethod.IsExtensionMethod)
            {
                // Handle core ILogger.BeginScope method
                return ExtractCoreMethodParameters(stateArgument, loggingTypes);
            }

            return [];
        }

        /// <summary>
        /// Extracts parameters from extension method calls that use message templates.
        /// </summary>
        private List<MessageParameter> ExtractExtensionMethodParameters(IInvocationOperation operation, string messageTemplate)
        {
            try
            {
                _logger.LogDebug("Extracting parameters from template: {Template} in {Method}",
                    messageTemplate, operation.TargetMethod.Name);

                if (string.IsNullOrEmpty(messageTemplate))
                {
                    return [];
                }

                var formatter = new LogValuesFormatter(messageTemplate);
                if (formatter.ValueNames.Count == 0)
                {
                    return [];
                }

                var messageParameters = new List<MessageParameter>();

                // For extension methods, the params array is in argument index 2 (after 'this' and messageFormat)
                const int PARAMS_ARGUMENT_INDEX = 2;
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
                    messageTemplate, operation.TargetMethod.Name);
                return [];
            }
        }

        /// <summary>
        /// Extracts parameters from core ILogger.BeginScope method calls.
        /// </summary>
        private List<MessageParameter> ExtractCoreMethodParameters(IArgumentOperation stateArgument, LoggingTypes loggingTypes)
        {
            // Try to extract key-value pairs first
            var messageParameters = _keyValuePairExtractionService.TryExtractParameters(stateArgument, loggingTypes);

            // If we got parameters from KeyValuePair extraction, return them
            if (messageParameters.Count > 0)
            {
                return messageParameters;
            }

            // If the type is a KeyValuePair enumerable but we couldn't extract values,
            // still don't fall back to anonymous object extraction
            if (_keyValuePairExtractionService.IsKeyValuePairEnumerable(stateArgument.Value?.Type, loggingTypes))
            {
                return messageParameters; // Empty list but valid KeyValuePair type
            }

            // Fallback to anonymous object extraction
            if (stateArgument.Value is IAnonymousObjectCreationOperation objectCreation)
            {
                try
                {
                    _logger.LogDebug("Extracting parameters from anonymous object");

                    if (_anonymousObjectParameterExtractor.TryExtractParameters(objectCreation, loggingTypes, null, out var parameters))
                    {
                        _logger.LogDebug("Successfully extracted {Count} parameters from anonymous object", parameters.Count);
                        return parameters;
                    }

                    _logger.LogDebug("No parameters extracted from anonymous object");
                    return [];
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract parameters from anonymous object");
                    return [];
                }
            }

            return [];
        }

        /// <summary>
        /// Extracts parameters from params argument array.
        /// </summary>
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
                    var parameter = MessageParameterFactory.CreateFromOperation(
                        formatter.ValueNames[0],
                        paramsArgument
                    );
                    messageParameters.Add(parameter);
                }
            }
        }

        /// <summary>
        /// Extracts parameters from array elements.
        /// </summary>
        private void ExtractFromArrayElements(System.Collections.Immutable.ImmutableArray<IOperation> elementValues, LogValuesFormatter formatter, List<MessageParameter> messageParameters)
        {
            for (int i = 0; i < elementValues.Length && i < formatter.ValueNames.Count; i++)
            {
                var element = elementValues[i].UnwrapConversion();
                var parameterName = formatter.ValueNames[i];

                var parameter = MessageParameterFactory.CreateFromOperation(
                    parameterName,
                    element
                );

                messageParameters.Add(parameter);
            }
        }
    }
}
