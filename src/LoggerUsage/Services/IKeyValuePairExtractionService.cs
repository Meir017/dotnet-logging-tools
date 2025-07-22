using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;

namespace LoggerUsage.Services
{
    /// <summary>
    /// Service for extracting and validating KeyValuePair collections for logger scope parameters.
    /// </summary>
    public interface IKeyValuePairExtractionService
    {
        /// <summary>
        /// Attempts to extract parameters from KeyValuePair collections.
        /// </summary>
        /// <param name="argument">The argument operation containing the KeyValuePair collection</param>
        /// <param name="loggingTypes">The logging types context</param>
        /// <returns>A list of extracted message parameters, or empty list if extraction failed</returns>
        List<MessageParameter> TryExtractParameters(IArgumentOperation argument, LoggingTypes loggingTypes);

        /// <summary>
        /// Checks if a type implements IEnumerable&lt;KeyValuePair&lt;string, object?&gt;&gt;.
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <param name="loggingTypes">The logging types context</param>
        /// <returns>True if the type is an enumerable of KeyValuePair, false otherwise</returns>
        bool IsKeyValuePairEnumerable(ITypeSymbol? type, LoggingTypes loggingTypes);

        /// <summary>
        /// Checks if a type is KeyValuePair&lt;string, object?&gt;.
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <param name="loggingTypes">The logging types context</param>
        /// <returns>True if the type is a KeyValuePair, false otherwise</returns>
        bool IsKeyValuePairType(ITypeSymbol? type, LoggingTypes loggingTypes);
    }
}
