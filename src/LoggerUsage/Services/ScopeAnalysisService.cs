using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using LoggerUsage.Models;
using LoggerUsage.Analyzers;

namespace LoggerUsage.Services
{
    /// <summary>
    /// Implementation of scope analysis service that consolidates scope state extraction logic.
    /// </summary>
    public class ScopeAnalysisService : IScopeAnalysisService
    {
        private readonly IParameterExtractionService _parameterExtractionService;
        private readonly IKeyValuePairExtractionService _keyValuePairExtractionService;
        private readonly ILogger<ScopeAnalysisService> _logger;

        public ScopeAnalysisService(
            IParameterExtractionService parameterExtractionService,
            IKeyValuePairExtractionService keyValuePairExtractionService,
            ILoggerFactory loggerFactory)
        {
            _parameterExtractionService = parameterExtractionService;
            _keyValuePairExtractionService = keyValuePairExtractionService;
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
                    return ScopeAnalysisResult.Success(null, new List<MessageParameter>(), operation.TargetMethod.IsExtensionMethod);
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
            return _parameterExtractionService.GetArgumentIndex(operation);
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

            return new List<MessageParameter>();
        }

        /// <summary>
        /// Extracts parameters from extension method calls that use message templates.
        /// </summary>
        private List<MessageParameter> ExtractExtensionMethodParameters(IInvocationOperation operation, string messageTemplate)
        {
            return _parameterExtractionService.ExtractFromMessageTemplate(operation, messageTemplate);
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
                return _parameterExtractionService.ExtractFromAnonymousObject(objectCreation, loggingTypes);
            }

            return new List<MessageParameter>();
        }
    }
}
