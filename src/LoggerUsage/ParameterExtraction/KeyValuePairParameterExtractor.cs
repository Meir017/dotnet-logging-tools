using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;

namespace LoggerUsage.ParameterExtraction;

/// <summary>
/// Extracts parameters from KeyValuePair collections used in logger scopes.
/// </summary>
internal class KeyValuePairParameterExtractor : IParameterExtractor
{
    public bool TryExtractParameters(
        IOperation operation, 
        LoggingTypes loggingTypes, 
        string? messageTemplate, 
        out List<MessageParameter> parameters)
    {
        parameters = new List<MessageParameter>();

        return operation switch
        {
            IObjectCreationOperation objectCreation => TryExtractFromObjectCreation(objectCreation, parameters, loggingTypes),
            IArrayCreationOperation arrayCreation => TryExtractFromArrayCreation(arrayCreation, parameters, loggingTypes),
            ILocalReferenceOperation localRef => TryExtractFromLocalReference(localRef, parameters, loggingTypes),
            IFieldReferenceOperation fieldRef => TryExtractFromFieldReference(fieldRef, parameters, loggingTypes),
            _ => false
        };
    }

    private static bool TryExtractFromObjectCreation(IObjectCreationOperation objectCreation, List<MessageParameter> parameters, LoggingTypes loggingTypes)
    {
        if (!IsKeyValuePairEnumerable(objectCreation.Type, loggingTypes))
            return false;

        if (objectCreation.Initializer != null)
        {
            ExtractFromCollectionInitializer(objectCreation.Initializer, parameters, loggingTypes);
        }

        return parameters.Count > 0;
    }

    private static bool TryExtractFromArrayCreation(IArrayCreationOperation arrayCreation, List<MessageParameter> parameters, LoggingTypes loggingTypes)
    {
        if (!IsKeyValuePairEnumerable(arrayCreation.Type, loggingTypes))
            return false;

        if (arrayCreation.Initializer != null)
        {
            ExtractFromArrayInitializer(arrayCreation.Initializer, parameters, loggingTypes);
        }

        return parameters.Count > 0;
    }

    private static bool TryExtractFromLocalReference(ILocalReferenceOperation localRef, List<MessageParameter> parameters, LoggingTypes loggingTypes)
    {
        // For now, just check if it's a KeyValuePair type - extracting values would require more complex analysis
        return IsKeyValuePairEnumerable(localRef.Type, loggingTypes);
    }

    private static bool TryExtractFromFieldReference(IFieldReferenceOperation fieldRef, List<MessageParameter> parameters, LoggingTypes loggingTypes)
    {
        // For now, just check if it's a KeyValuePair type - extracting values would require more complex analysis
        return IsKeyValuePairEnumerable(fieldRef.Type, loggingTypes);
    }

    private static void ExtractFromCollectionInitializer(IObjectOrCollectionInitializerOperation initializer, List<MessageParameter> parameters, LoggingTypes loggingTypes)
    {
        foreach (var elementInitializer in initializer.Initializers)
        {
            if (elementInitializer is IObjectCreationOperation keyValuePairCreation)
            {
                ExtractKeyValuePairFromObjectCreation(keyValuePairCreation, parameters, loggingTypes);
            }
        }
    }

    private static void ExtractFromArrayInitializer(IArrayInitializerOperation initializer, List<MessageParameter> parameters, LoggingTypes loggingTypes)
    {
        foreach (var element in initializer.ElementValues)
        {
            if (element is IObjectCreationOperation keyValuePairCreation)
            {
                ExtractKeyValuePairFromObjectCreation(keyValuePairCreation, parameters, loggingTypes);
            }
        }
    }

    private static void ExtractKeyValuePairFromObjectCreation(IObjectCreationOperation objectCreation, List<MessageParameter> parameters, LoggingTypes loggingTypes)
    {
        if (!IsKeyValuePairType(objectCreation.Type, loggingTypes))
            return;

        if (objectCreation.Arguments.Length >= 2)
        {
            var keyArg = objectCreation.Arguments[0].Value;
            var valueArg = objectCreation.Arguments[1].Value;

            if (keyArg.ConstantValue.HasValue && keyArg.ConstantValue.Value is string key)
            {
                parameters.Add(new MessageParameter(
                    Name: key,
                    Type: valueArg.Type?.ToPrettyDisplayString() ?? "object",
                    Kind: valueArg.ConstantValue.HasValue ? "Constant" : valueArg.Kind.ToString()
                ));
            }
        }
    }

    private static bool IsKeyValuePairEnumerable(ITypeSymbol? type, LoggingTypes loggingTypes)
    {
        if (type == null) return false;

        // Check if it's an enumerable of KeyValuePair
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var typeDefinition = namedType.ConstructedFrom;
            if (SymbolEqualityComparer.Default.Equals(typeDefinition, loggingTypes.IEnumerableOfKeyValuePair.ConstructedFrom))
            {
                return true;
            }
        }

        // Fallback to name-based check for compatibility
        return (type.Name?.Contains("KeyValuePair") ?? false) || (type.ToString()?.Contains("KeyValuePair") ?? false);
    }

    private static bool IsKeyValuePairType(ITypeSymbol? type, LoggingTypes loggingTypes)
    {
        if (type == null) return false;

        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var typeDefinition = namedType.ConstructedFrom;
            if (SymbolEqualityComparer.Default.Equals(typeDefinition, loggingTypes.KeyValuePairGeneric))
            {
                return true;
            }
        }

        // Fallback to name-based check for compatibility
        return (type.Name?.Contains("KeyValuePair") ?? false) && type is INamedTypeSymbol genericType && genericType.IsGenericType;
    }
}
