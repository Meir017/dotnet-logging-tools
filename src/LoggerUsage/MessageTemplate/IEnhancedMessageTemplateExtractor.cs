using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;

namespace LoggerUsage.MessageTemplate;

/// <summary>
/// Enhanced interface for extracting message templates with comprehensive error handling.
/// </summary>
internal interface IEnhancedMessageTemplateExtractor : IMessageTemplateExtractor
{
    /// <summary>
    /// Extracts a message template from the given argument operation with detailed result information.
    /// </summary>
    /// <param name="argument">The argument operation to extract the template from</param>
    /// <returns>Extraction result containing the template or error information</returns>
    ExtractionResult<string> ExtractWithResult(IArgumentOperation argument);
    
    /// <summary>
    /// Extracts a message template from any operation with detailed result information.
    /// </summary>
    /// <param name="operation">The operation to extract the template from</param>
    /// <returns>Extraction result containing the template or error information</returns>
    ExtractionResult<string> ExtractWithResult(IOperation operation);
}
