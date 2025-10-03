using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;
using LoggerUsage.Utilities;

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
        parameters = [];

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
        messageParameters = [];

        if (string.IsNullOrEmpty(messageTemplate))
        {
            return false;
        }

        // 1. Extract placeholders from the message template
        var formatter = new LogValuesFormatter(messageTemplate);
        if (formatter.ValueNames.Count == 0)
        {
            return false;
        }

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
            var parameter = parameters[i];
            var parameterName = formatter.ValueNames[i];
            
            // Check for TagName attribute on the parameter
            string? customTagName = null;
            if (loggingTypes.TagNameAttribute != null)
            {
                var tagNameAttribute = parameter.GetAttributes()
                    .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, loggingTypes.TagNameAttribute));
                
                if (tagNameAttribute != null && 
                    tagNameAttribute.ConstructorArguments.Length > 0 &&
                    tagNameAttribute.ConstructorArguments[0].Value is string tagName)
                {
                    customTagName = tagName;
                }
            }
            
            // Extract data classification information
            var dataClassification = Utilities.DataClassificationExtractor.ExtractDataClassification(parameter, loggingTypes);
            
            messageParameters.Add(new MessageParameter(parameterName, parameter.Type.ToPrettyDisplayString(), null, customTagName, dataClassification));
        }

        return messageParameters.Count > 0;
    }
}
