using LoggerUsage.Models;
using LoggerUsage.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace LoggerUsage.Services;

internal sealed class DirectLogStateExtractor : IDirectLogStateExtractor
{
    private const string OriginalFormatKey = "{OriginalFormat}";

    public bool TryExtract(
        IOperation state,
        LoggingTypes loggingTypes,
        out string? messageTemplate,
        out List<MessageParameter> messageParameters)
    {
        messageTemplate = null;
        messageParameters = [];

        var value = state.UnwrapConversion();
        var entries = new List<(string Key, IOperation Value)>();
        var extracted = value switch
        {
            IArrayCreationOperation arrayCreation
                when IsKeyValuePairArray(arrayCreation.Type, loggingTypes) =>
                TryCollectArray(arrayCreation, entries, loggingTypes),
            IObjectCreationOperation objectCreation
                when IsKeyValuePairCollection(objectCreation.Type, loggingTypes) =>
                TryCollectObject(objectCreation, entries, loggingTypes),
            ICollectionExpressionOperation collectionExpression
                when IsKeyValuePairCollectionExpression(collectionExpression.Type, loggingTypes) =>
                TryCollectCollectionExpression(collectionExpression, entries, loggingTypes),
            _ => false
        };

        if (!extracted)
        {
            return false;
        }

        var originalFormatEntries = entries
            .Where(entry => string.Equals(entry.Key, OriginalFormatKey, StringComparison.Ordinal))
            .ToList();
        if (originalFormatEntries.Count != 1)
        {
            return false;
        }

        var originalFormat = originalFormatEntries[0].Value.UnwrapConversion().ConstantValue;
        if (!originalFormat.HasValue || originalFormat.Value is not string template)
        {
            return false;
        }

        messageTemplate = template;
        var valuesByName = entries
            .Where(entry => !string.Equals(entry.Key, OriginalFormatKey, StringComparison.Ordinal))
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new Queue<IOperation>(group.Select(entry => entry.Value)),
                StringComparer.Ordinal);

        foreach (var name in new LogValuesFormatter(messageTemplate).ValueNames)
        {
            if (!valuesByName.TryGetValue(name, out var values) || values.Count == 0)
            {
                messageTemplate = null;
                messageParameters = [];
                return false;
            }

            messageParameters.Add(MessageParameterFactory.CreateFromStructuredState(name, values.Dequeue()));
        }

        return true;
    }

    private static bool TryCollectArray(
        IArrayCreationOperation arrayCreation,
        List<(string Key, IOperation Value)> entries,
        LoggingTypes loggingTypes)
    {
        if (arrayCreation.Initializer is null)
        {
            return false;
        }

        foreach (var element in arrayCreation.Initializer.ElementValues)
        {
            if (!TryCollectPair(element.UnwrapConversion(), entries, loggingTypes))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryCollectObject(
        IObjectCreationOperation objectCreation,
        List<(string Key, IOperation Value)> entries,
        LoggingTypes loggingTypes)
    {
        if (objectCreation.Initializer is null)
        {
            return false;
        }

        foreach (var initializer in objectCreation.Initializer.Initializers)
        {
            var collected = initializer switch
            {
                IInvocationOperation invocation => TryCollectInvocation(invocation, entries, loggingTypes),
                ISimpleAssignmentOperation assignment => TryCollectAssignment(assignment, entries),
                _ => TryCollectPair(initializer.UnwrapConversion(), entries, loggingTypes)
            };

            if (!collected)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryCollectCollectionExpression(
        ICollectionExpressionOperation collectionExpression,
        List<(string Key, IOperation Value)> entries,
        LoggingTypes loggingTypes)
    {
        foreach (var element in collectionExpression.Elements)
        {
            if (element is ISpreadOperation ||
                !TryCollectPair(element.UnwrapConversion(), entries, loggingTypes))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryCollectInvocation(
        IInvocationOperation invocation,
        List<(string Key, IOperation Value)> entries,
        LoggingTypes loggingTypes)
    {
        if (invocation.Arguments.Length == 1)
        {
            return TryCollectPair(
                invocation.Arguments[0].Value.UnwrapConversion(),
                entries,
                loggingTypes);
        }

        if (invocation.Arguments.Length >= 2)
        {
            return TryCollectKeyValue(
                invocation.Arguments[0].Value,
                invocation.Arguments[1].Value,
                entries);
        }

        return false;
    }

    private static bool TryCollectAssignment(
        ISimpleAssignmentOperation assignment,
        List<(string Key, IOperation Value)> entries)
    {
        if (assignment.Target is IPropertyReferenceOperation propertyReference &&
            propertyReference.Arguments.Length > 0)
        {
            return TryCollectKeyValue(
                propertyReference.Arguments[0].Value,
                assignment.Value,
                entries);
        }

        return false;
    }

    private static bool TryCollectPair(
        IOperation operation,
        List<(string Key, IOperation Value)> entries,
        LoggingTypes loggingTypes)
    {
        if (operation is not IObjectCreationOperation pairCreation ||
            !IsKeyValuePair(pairCreation.Type, loggingTypes) ||
            pairCreation.Arguments.Length < 2)
        {
            return false;
        }

        var keyArgument = pairCreation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0);
        var valueArgument = pairCreation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 1);
        return keyArgument is not null &&
            valueArgument is not null &&
            TryCollectKeyValue(keyArgument.Value, valueArgument.Value, entries);
    }

    private static bool TryCollectKeyValue(
        IOperation keyOperation,
        IOperation valueOperation,
        List<(string Key, IOperation Value)> entries)
    {
        var key = keyOperation.UnwrapConversion().ConstantValue;
        if (!key.HasValue || key.Value is not string keyValue)
        {
            return false;
        }

        entries.Add((keyValue, valueOperation));
        return true;
    }

    private static bool IsKeyValuePairArray(ITypeSymbol? type, LoggingTypes loggingTypes) =>
        type is IArrayTypeSymbol arrayType && IsKeyValuePair(arrayType.ElementType, loggingTypes);

    private static bool IsKeyValuePairCollection(ITypeSymbol? type, LoggingTypes loggingTypes) =>
        type is INamedTypeSymbol namedType &&
        (IsKeyValuePairEnumerable(namedType, loggingTypes) ||
         namedType.AllInterfaces.Any(interfaceType =>
             IsKeyValuePairEnumerable(interfaceType, loggingTypes)));

    private static bool IsKeyValuePairCollectionExpression(ITypeSymbol? type, LoggingTypes loggingTypes) =>
        type switch
        {
            IArrayTypeSymbol arrayType => IsKeyValuePair(arrayType.ElementType, loggingTypes),
            INamedTypeSymbol namedType => IsKeyValuePairCollection(namedType, loggingTypes),
            _ => false
        };

    private static bool IsKeyValuePair(ITypeSymbol? type, LoggingTypes loggingTypes) =>
        type is INamedTypeSymbol namedType &&
        SymbolEqualityComparer.Default.Equals(
            namedType,
            loggingTypes.KeyValuePairOfStringNullableObject);

    private static bool IsKeyValuePairEnumerable(
        INamedTypeSymbol type,
        LoggingTypes loggingTypes) =>
        type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T &&
        IsKeyValuePair(type.TypeArguments[0], loggingTypes);
}
