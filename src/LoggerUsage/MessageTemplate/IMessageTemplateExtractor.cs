using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace LoggerUsage.MessageTemplate;

/// <summary>
/// Interface for extracting message templates from different argument types.
/// </summary>
internal interface IMessageTemplateExtractor
{
    /// <summary>
    /// Attempts to extract a message template from the given argument operation.
    /// </summary>
    /// <param name="argument">The argument operation to extract the template from</param>
    /// <param name="template">The extracted template if successful</param>
    /// <returns>True if a template was successfully extracted, false otherwise</returns>
    bool TryExtract(IArgumentOperation argument, out string template);
    
    /// <summary>
    /// Attempts to extract a message template from any operation.
    /// </summary>
    /// <param name="operation">The operation to extract the template from</param>
    /// <param name="template">The extracted template if successful</param>
    /// <returns>True if a template was successfully extracted, false otherwise</returns>
    bool TryExtract(IOperation operation, out string template);
}
