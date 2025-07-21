using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;

namespace LoggerUsage.ParameterExtraction;

/// <summary>
/// Strategy interface for extracting message parameters from different operation types.
/// </summary>
internal interface IParameterExtractor
{
    /// <summary>
    /// Attempts to extract message parameters from the given operation.
    /// </summary>
    /// <param name="operation">The operation to extract parameters from</param>
    /// <param name="loggingTypes">Type information for logging-related types</param>
    /// <param name="messageTemplate">Optional message template to guide parameter extraction</param>
    /// <param name="parameters">The extracted parameters if successful</param>
    /// <returns>True if parameters were successfully extracted, false otherwise</returns>
    bool TryExtractParameters(
        IOperation operation, 
        LoggingTypes loggingTypes, 
        string? messageTemplate, 
        out List<MessageParameter> parameters);
}
