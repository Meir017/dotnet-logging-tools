using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;

namespace LoggerUsage.Services
{
    /// <summary>
    /// Service for extracting parameters from different sources in logging operations.
    /// </summary>
    public interface IParameterExtractionService
    {
        /// <summary>
        /// Extracts parameters from extension method calls that use message templates.
        /// </summary>
        /// <param name="operation">The invocation operation</param>
        /// <param name="template">The message template</param>
        /// <returns>A list of extracted message parameters</returns>
        List<MessageParameter> ExtractFromMessageTemplate(IInvocationOperation operation, string template);

        /// <summary>
        /// Extracts parameters from anonymous object creation operations.
        /// </summary>
        /// <param name="operation">The anonymous object creation operation</param>
        /// <returns>A list of extracted message parameters</returns>
        List<MessageParameter> ExtractFromAnonymousObject(IAnonymousObjectCreationOperation operation);

        /// <summary>
        /// Gets the correct argument index based on whether the method is an extension method.
        /// </summary>
        /// <param name="operation">The invocation operation</param>
        /// <returns>The index of the state argument</returns>
        int GetArgumentIndex(IInvocationOperation operation);

        /// <summary>
        /// Creates a MessageParameter with consistent formatting.
        /// </summary>
        /// <param name="name">The parameter name</param>
        /// <param name="type">The parameter type</param>
        /// <param name="kind">The parameter kind</param>
        /// <returns>A new MessageParameter instance</returns>
        MessageParameter CreateMessageParameter(string name, string type, string kind);
    }
}
