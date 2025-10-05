using Microsoft.CodeAnalysis;
using LoggerUsage.Models;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Analyzers
{
    // Partial class containing LogProperties-related functionality
    internal partial class LoggerMessageAttributeAnalyzer
    {
        /// <summary>
        /// Extracts LogProperties parameters from a method's parameter list
        /// </summary>
        private List<LogPropertiesParameterInfo> ExtractLogPropertiesParameters(IMethodSymbol methodSymbol, LoggingTypes loggingTypes)
        {
            var logPropertiesParameters = new List<LogPropertiesParameterInfo>();

            foreach (var parameter in methodSymbol.Parameters)
            {
                // Skip ILogger parameters
                if (SymbolEqualityComparer.Default.Equals(parameter.Type, loggingTypes.ILogger))
                {
                    continue;
                }

                // Look for LogProperties attribute on this parameter
                var logPropertiesAttribute = parameter.GetAttributes()
                    .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, loggingTypes.LogPropertiesAttribute));

                if (logPropertiesAttribute != null)
                {
                    // Extract LogProperties configuration
                    var configuration = ExtractLogPropertiesConfiguration(logPropertiesAttribute);

                    // Extract properties from the parameter type with transitive support
                    var properties = ExtractPropertiesFromType(parameter.Type, configuration, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), loggingTypes);

                    // Check for TagProvider attribute on the same parameter
                    TagProviderInfo? tagProviderInfo = null;
                    if (loggingTypes.TagProviderAttribute != null)
                    {
                        var tagProviderAttribute = parameter.GetAttributes()
                            .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, loggingTypes.TagProviderAttribute));

                        if (tagProviderAttribute != null)
                        {
                            tagProviderInfo = ExtractTagProviderInfo(parameter, tagProviderAttribute, loggingTypes);
                        }
                    }

                    var logPropertiesParameter = new LogPropertiesParameterInfo(
                        parameter.Name,
                        parameter.Type.Name,
                        configuration,
                        properties,
                        tagProviderInfo);

                    logPropertiesParameters.Add(logPropertiesParameter);

                    logger.LogTrace("Found LogProperties parameter {ParameterName} of type {ParameterType} with {PropertyCount} properties",
                        parameter.Name, parameter.Type.ToDisplayString(), properties.Count);
                }
            }

            return logPropertiesParameters;
        }

        /// <summary>
        /// Extracts LogProperties configuration from the attribute
        /// </summary>
        private static LogPropertiesConfiguration ExtractLogPropertiesConfiguration(AttributeData logPropertiesAttribute)
        {
            bool omitReferenceName = false;
            bool skipNullProperties = false;
            bool transitive = false;

            // Extract named arguments (properties of the attribute)
            foreach (var namedArg in logPropertiesAttribute.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case nameof(LogPropertiesConfiguration.OmitReferenceName):
                        if (namedArg.Value.Value is bool omitRefValue)
                        {
                            omitReferenceName = omitRefValue;
                        }
                        break;
                    case nameof(LogPropertiesConfiguration.SkipNullProperties):
                        if (namedArg.Value.Value is bool skipNullValue)
                        {
                            skipNullProperties = skipNullValue;
                        }
                        break;
                    case nameof(LogPropertiesConfiguration.Transitive):
                        if (namedArg.Value.Value is bool transitiveValue)
                        {
                            transitive = transitiveValue;
                        }
                        break;
                }
            }

            return new LogPropertiesConfiguration
            {
                OmitReferenceName = omitReferenceName,
                SkipNullProperties = skipNullProperties,
                Transitive = transitive
            };
        }

        /// <summary>
        /// Extracts properties from a type for LogProperties analysis
        /// </summary>
        /// <param name="typeSymbol">The type to extract properties from</param>
        /// <param name="configuration">LogProperties configuration including Transitive setting</param>
        /// <param name="visitedTypes">Set of already visited types to prevent circular references</param>
        /// <param name="loggingTypes">Logging types for symbol comparison</param>
        /// <param name="depth">Current depth level for nested analysis</param>
        private static List<LogPropertyInfo> ExtractPropertiesFromType(
            ITypeSymbol typeSymbol,
            LogPropertiesConfiguration configuration,
            HashSet<ITypeSymbol> visitedTypes,
            LoggingTypes loggingTypes,
            int depth = 0)
        {
            var properties = new List<LogPropertyInfo>();

            // Prevent infinite recursion - max depth of 10 levels
            if (depth > 10)
            {
                return properties;
            }

            // Check for circular references
            if (visitedTypes.Contains(typeSymbol))
            {
                return properties;
            }

            // Add current type to visited set
            visitedTypes.Add(typeSymbol);

            if (typeSymbol is INamedTypeSymbol namedType)
            {
                // Get all public properties
                var publicProperties = namedType.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(prop => prop.DeclaredAccessibility == Accessibility.Public && prop.GetMethod != null);

                foreach (var property in publicProperties)
                {
                    // Check if property has LogPropertyIgnore attribute
                    bool hasIgnoreAttribute = property.GetAttributes()
                        .Any(attr => attr.AttributeClass?.Name == "LogPropertyIgnoreAttribute");

                    if (!hasIgnoreAttribute)
                    {
                        // Check for TagName attribute on the property
                        string? customTagName = null;
                        if (loggingTypes.TagNameAttribute != null)
                        {
                            var tagNameAttribute = property.GetAttributes()
                                .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, loggingTypes.TagNameAttribute));

                            if (tagNameAttribute != null &&
                                tagNameAttribute.ConstructorArguments.Length > 0 &&
                                tagNameAttribute.ConstructorArguments[0].Value is string tagName)
                            {
                                customTagName = tagName;
                            }
                        }

                        // Extract data classification information
                        var dataClassification = Utilities.DataClassificationExtractor.ExtractDataClassification(property, loggingTypes);

                        List<LogPropertyInfo>? nestedProperties = null;

                        // If Transitive is enabled, analyze nested properties for complex types
                        if (configuration.Transitive && IsComplexType(property.Type, loggingTypes))
                        {
                            // Create a new visited set for this branch to allow the same type in different branches
                            var branchVisitedTypes = new HashSet<ITypeSymbol>(visitedTypes, SymbolEqualityComparer.Default);

                            // Handle collection types
                            if (IsCollectionType(property.Type, loggingTypes, out var elementType) && elementType != null)
                            {
                                // Only add nested properties if the element type is complex
                                if (IsComplexType(elementType, loggingTypes))
                                {
                                    nestedProperties = ExtractPropertiesFromType(elementType, configuration, branchVisitedTypes, loggingTypes, depth + 1);
                                }
                            }
                            // Handle regular complex types
                            else
                            {
                                nestedProperties = ExtractPropertiesFromType(property.Type, configuration, branchVisitedTypes, loggingTypes, depth + 1);
                            }

                            // Only include nested properties if we found any
                            if (nestedProperties != null && nestedProperties.Count == 0)
                            {
                                nestedProperties = null;
                            }
                        }

                        var logProperty = new LogPropertyInfo(
                            property.Name,
                            property.Name,
                            GetSimpleTypeName(property.Type),
                            property.Type.CanBeReferencedByName && property.Type.NullableAnnotation == NullableAnnotation.Annotated,
                            customTagName,
                            dataClassification,
                            nestedProperties);

                        properties.Add(logProperty);
                    }
                }
            }

            // Remove current type from visited set for other branches
            visitedTypes.Remove(typeSymbol);

            return properties;
        }

        /// <summary>
        /// Determines if a type is a complex type (not a primitive or string)
        /// </summary>
        private static bool IsComplexType(ITypeSymbol typeSymbol, LoggingTypes loggingTypes)
        {
            // Unwrap nullable types
            if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated && typeSymbol is INamedTypeSymbol { IsGenericType: true } nullableType)
            {
                if (nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    typeSymbol = nullableType.TypeArguments[0];
                }
            }

            // Check if it's a collection - collections are treated separately
            if (IsCollectionType(typeSymbol, loggingTypes, out _))
            {
                return true; // Collections are complex so they get analyzed for nested properties
            }

            // Primitive types and strings are not complex
            if (typeSymbol.SpecialType != SpecialType.None)
            {
                return false;
            }

            // Enums are not complex
            if (typeSymbol.TypeKind == TypeKind.Enum)
            {
                return false;
            }

            // Check for well-known simple types using symbol comparison
            if (SymbolEqualityComparer.Default.Equals(typeSymbol, loggingTypes.DateTime) ||
                SymbolEqualityComparer.Default.Equals(typeSymbol, loggingTypes.DateTimeOffset) ||
                SymbolEqualityComparer.Default.Equals(typeSymbol, loggingTypes.TimeSpan) ||
                SymbolEqualityComparer.Default.Equals(typeSymbol, loggingTypes.Guid) ||
                SymbolEqualityComparer.Default.Equals(typeSymbol, loggingTypes.Uri))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines if a type is a collection type and extracts the element type
        /// </summary>
        private static bool IsCollectionType(ITypeSymbol typeSymbol, LoggingTypes loggingTypes, out ITypeSymbol? elementType)
        {
            elementType = null;

            // String is not considered a collection for our purposes
            if (typeSymbol.SpecialType == SpecialType.System_String)
            {
                return false;
            }

            // Handle arrays
            if (typeSymbol is IArrayTypeSymbol arrayType)
            {
                elementType = arrayType.ElementType;
                return true;
            }

            // Check if the type implements IEnumerable<T>
            if (typeSymbol is INamedTypeSymbol namedType)
            {
                // Check if the type itself is IEnumerable<T>
                if (namedType.IsGenericType &&
                    SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, loggingTypes.IEnumerableGeneric))
                {
                    elementType = namedType.TypeArguments[0];
                    return true;
                }

                // Check if any of the interfaces implement IEnumerable<T>
                foreach (var iface in namedType.AllInterfaces)
                {
                    if (iface.IsGenericType &&
                        SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, loggingTypes.IEnumerableGeneric))
                    {
                        elementType = iface.TypeArguments[0];
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets a simplified display name for a type symbol
        /// </summary>
        private static string GetSimpleTypeName(ITypeSymbol typeSymbol)
        {
            // Handle arrays
            if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
            {
                var elementTypeName = GetSimpleTypeName(arrayTypeSymbol.ElementType);
                return $"{elementTypeName}[]";
            }

            // Handle generic types (List<T>, IEnumerable<T>, etc.)
            if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var typeName = namedType.Name;
                // Return just the type name without generic parameters for display
                return typeName;
            }

            // Handle common built-in types
            return typeSymbol.SpecialType switch
            {
                SpecialType.System_Boolean => "bool",
                SpecialType.System_Byte => "byte",
                SpecialType.System_SByte => "sbyte",
                SpecialType.System_Int16 => "short",
                SpecialType.System_UInt16 => "ushort",
                SpecialType.System_Int32 => "int",
                SpecialType.System_UInt32 => "uint",
                SpecialType.System_Int64 => "long",
                SpecialType.System_UInt64 => "ulong",
                SpecialType.System_Decimal => "decimal",
                SpecialType.System_Single => "float",
                SpecialType.System_Double => "double",
                SpecialType.System_Char => "char",
                SpecialType.System_String => "string",
                SpecialType.System_Object => "object",
                _ => typeSymbol.Name // Use simple name for other types
            };
        }
    }
}
