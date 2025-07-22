using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using LoggerUsage.Models;
using LoggerUsage.ParameterExtraction;

namespace LoggerUsage.Services;

/// <summary>
/// Orchestrates parameter extraction using the strategy pattern with multiple extractors.
/// This service coordinates the use of specific parameter extractors based on operation type.
/// </summary>
public class ParameterExtractionOrchestrator
{
    private readonly ILogger<ParameterExtractionOrchestrator> _logger;
    private readonly List<IParameterExtractor> _extractors;

    public ParameterExtractionOrchestrator(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ParameterExtractionOrchestrator>();
        
        // Initialize all parameter extractors
        _extractors = new List<IParameterExtractor>
        {
            new ArrayParameterExtractor(),
            new GenericTypeParameterExtractor(),
            new AnonymousObjectParameterExtractor(),
            new KeyValuePairParameterExtractor(),
            new MethodSignatureParameterExtractor(),
            new EnhancedKeyValuePairParameterExtractor(loggerFactory)
        };
    }

    /// <summary>
    /// Extracts parameters using the first applicable extractor.
    /// </summary>
    /// <param name="operation">The operation to extract parameters from</param>
    /// <param name="loggingTypes">Type information for logging-related types</param>
    /// <param name="messageTemplate">Optional message template to guide parameter extraction</param>
    /// <returns>List of extracted message parameters</returns>
    public List<MessageParameter> ExtractParameters(
        IOperation operation, 
        LoggingTypes loggingTypes, 
        string? messageTemplate = null)
    {
        _logger.LogDebug("Attempting to extract parameters from {OperationType}", operation.GetType().Name);

        foreach (var extractor in _extractors)
        {
            try
            {
                if (extractor.TryExtractParameters(operation, loggingTypes, messageTemplate, out var parameters))
                {
                    _logger.LogDebug("Successfully extracted {Count} parameters using {ExtractorType}", 
                        parameters.Count, extractor.GetType().Name);
                    return parameters;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Extractor {ExtractorType} failed to extract parameters", 
                    extractor.GetType().Name);
            }
        }

        _logger.LogDebug("No extractor was able to extract parameters from {OperationType}", operation.GetType().Name);
        return new List<MessageParameter>();
    }

    /// <summary>
    /// Extracts parameters for array-based logger method calls.
    /// This is specifically for LogMethodAnalyzer usage.
    /// </summary>
    public List<MessageParameter> ExtractFromArrayArguments(
        IInvocationOperation operation, 
        LoggingTypes loggingTypes, 
        string messageTemplate)
    {
        var extractor = new ArrayParameterExtractor();
        if (extractor.TryExtractParameters(operation, loggingTypes, messageTemplate, out var parameters))
        {
            return parameters;
        }
        return new List<MessageParameter>();
    }

    /// <summary>
    /// Extracts parameters from generic type arguments for LoggerMessage.Define calls.
    /// This is specifically for LoggerMessageDefineAnalyzer usage.
    /// </summary>
    public List<MessageParameter> ExtractFromGenericTypes(
        IInvocationOperation operation, 
        LoggingTypes loggingTypes, 
        string messageTemplate)
    {
        var extractor = new GenericTypeParameterExtractor();
        if (extractor.TryExtractParameters(operation, loggingTypes, messageTemplate, out var parameters))
        {
            return parameters;
        }
        return new List<MessageParameter>();
    }

    /// <summary>
    /// Extracts parameters from anonymous object creation operations.
    /// This is specifically for scope analysis usage.
    /// </summary>
    public List<MessageParameter> ExtractFromAnonymousObject(
        IAnonymousObjectCreationOperation operation, 
        LoggingTypes loggingTypes)
    {
        var extractor = new AnonymousObjectParameterExtractor();
        if (extractor.TryExtractParameters(operation, loggingTypes, null, out var parameters))
        {
            return parameters;
        }
        return new List<MessageParameter>();
    }

    /// <summary>
    /// Extracts parameters from KeyValuePair collections for scope analysis.
    /// </summary>
    public List<MessageParameter> ExtractFromKeyValuePairs(
        IOperation operation, 
        LoggingTypes loggingTypes)
    {
        var extractor = new KeyValuePairParameterExtractor();
        if (extractor.TryExtractParameters(operation, loggingTypes, null, out var parameters))
        {
            return parameters;
        }
        return new List<MessageParameter>();
    }

    /// <summary>
    /// Extracts parameters from method signatures for LoggerMessage attributes.
    /// </summary>
    public List<MessageParameter> ExtractFromMethodSignature(
        IMethodSymbol methodSymbol,
        string messageTemplate,
        LoggingTypes loggingTypes)
    {
        if (MethodSignatureParameterExtractor.TryExtractFromMethodSignature(
            methodSymbol, messageTemplate, loggingTypes, out var parameters))
        {
            return parameters;
        }
        return new List<MessageParameter>();
    }
}
