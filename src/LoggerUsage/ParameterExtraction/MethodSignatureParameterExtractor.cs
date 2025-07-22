using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;

namespace LoggerUsage.ParameterExtraction;

/// <summary>
/// Extracts parameters from method signatures for LoggerMessage attributes.
/// Note: This extractor requires additional context (IMethodSymbol) that's not available 
/// in the standard IParameterExtractor interface. This is a placeholder implementation.
/// </summary>
internal class MethodSignatureParameterExtractor : IParameterExtractor
{
    public bool TryExtractParameters(
        IOperation operation, 
        LoggingTypes loggingTypes, 
        string? messageTemplate, 
        out List<MessageParameter> parameters)
    {
        parameters = new List<MessageParameter>();

        // This extractor requires additional context that's not available in the operation
        // The actual implementation would need access to the IMethodSymbol
        // This is handled directly in LoggerMessageAttributeAnalyzer for now
        return false;
    }

    /// <summary>
    /// Extracts parameters from method signature using method symbol and message template.
    /// This is the actual implementation used by LoggerMessageAttributeAnalyzer.
    /// </summary>
    public static bool TryExtractFromMethodSignature(
        IMethodSymbol methodSymbol, 
        string messageTemplate, 
        LoggingTypes loggingTypes, 
        out List<MessageParameter> messageParameters)
    {
        messageParameters = new List<MessageParameter>();
        
        if (string.IsNullOrEmpty(messageTemplate))
            return false;

        // 1. Extract placeholders from the message template
        var formatter = new LogValuesFormatter(messageTemplate);
        if (formatter.ValueNames.Count == 0)
            return false;
        
        // 2. Get method parameters, excluding ILogger, LogLevel, and Exception (by type)
        var parameters = methodSymbol.Parameters
            .Where(p =>
                !loggingTypes.LogLevel.Equals(p.Type, SymbolEqualityComparer.Default) &&
                !p.Type.IsLoggerInterface(loggingTypes) &&
                !p.Type.IsException(loggingTypes) &&
                !p.GetAttributes().Any(attr => loggingTypes.LogPropertiesAttribute.Equals(attr.AttributeClass, SymbolEqualityComparer.Default)))
            .ToList();

        for (int i = 0; i < parameters.Count && i < formatter.ValueNames.Count; i++)
        {
            var parameterName = formatter.ValueNames[i];
            messageParameters.Add(new MessageParameter(parameterName, parameters[i].Type.ToPrettyDisplayString(), null));
        }

        return messageParameters.Count > 0;
    }
}
