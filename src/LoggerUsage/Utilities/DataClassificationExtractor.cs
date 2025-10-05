using Microsoft.CodeAnalysis;
using LoggerUsage.Models;

namespace LoggerUsage.Utilities;

/// <summary>
/// Utility class for extracting data classification information from symbols.
/// </summary>
internal static class DataClassificationExtractor
{
    /// <summary>
    /// Extracts data classification information from a symbol's attributes.
    /// </summary>
    /// <param name="symbol">The symbol (parameter or property) to analyze.</param>
    /// <param name="loggingTypes">The logging types context.</param>
    /// <returns>Data classification information if found, otherwise null.</returns>
    public static DataClassificationInfo? ExtractDataClassification(ISymbol symbol, LoggingTypes loggingTypes)
    {
        if (loggingTypes.DataClassificationAttribute == null)
        {
            // Package not referenced, skip classification detection
            return null;
        }

        // Look for attributes that inherit from DataClassificationAttribute
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass == null)
            {
                continue;
            }

            // Check if this attribute inherits from DataClassificationAttribute
            if (InheritsFromDataClassificationAttribute(attribute.AttributeClass, loggingTypes.DataClassificationAttribute))
            {
                return ExtractClassificationFromAttribute(attribute);
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a type inherits from DataClassificationAttribute.
    /// </summary>
    private static bool InheritsFromDataClassificationAttribute(
        INamedTypeSymbol attributeClass,
        INamedTypeSymbol dataClassificationAttribute)
    {
        // Check direct match
        if (SymbolEqualityComparer.Default.Equals(attributeClass, dataClassificationAttribute))
        {
            return true;
        }

        // Check base types
        var baseType = attributeClass.BaseType;
        while (baseType != null)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType, dataClassificationAttribute))
            {
                return true;
            }
            baseType = baseType.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Extracts classification information from an attribute.
    /// </summary>
    private static DataClassificationInfo ExtractClassificationFromAttribute(AttributeData attribute)
    {
        var attributeTypeName = attribute.AttributeClass?.ToDisplayString() ?? "Unknown";
        var isCustomAttribute = true; // Assume custom unless we identify it as a built-in type

        // Try to extract the classification value from the constructor argument
        string classificationValue = "Unknown";
        if (attribute.ConstructorArguments.Length > 0)
        {
            var arg = attribute.ConstructorArguments[0];

            // The argument might be an enum value or other type
            if (arg.Value != null)
            {
                // If it's an enum, we can get the constant name
                if (arg.Type?.TypeKind == TypeKind.Enum)
                {
                    classificationValue = arg.Value.ToString() ?? "Unknown";
                }
                else
                {
                    classificationValue = arg.Value.ToString() ?? "Unknown";
                }
            }
        }

        return new DataClassificationInfo(
            attributeTypeName,
            classificationValue,
            isCustomAttribute);
    }
}
