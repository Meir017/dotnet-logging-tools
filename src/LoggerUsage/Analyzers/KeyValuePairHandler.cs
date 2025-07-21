using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;

namespace LoggerUsage.Analyzers
{
    /// <summary>
    /// Handles extraction and validation of KeyValuePair collections for logger scope parameters.
    /// </summary>
    internal static class KeyValuePairHandler
    {
        /// <summary>
        /// Attempts to extract parameters from KeyValuePair collections.
        /// </summary>
        public static bool TryExtractKeyValuePairParameters(IArgumentOperation stateArgument, LoggerUsageInfo usage, LoggingTypes loggingTypes)
        {
            var messageParameters = new List<MessageParameter>();

            bool extracted = stateArgument.Value switch
            {
                IObjectCreationOperation objectCreation => TryExtractFromObjectCreation(objectCreation, messageParameters, loggingTypes),
                IArrayCreationOperation arrayCreation => TryExtractFromArrayCreation(arrayCreation, messageParameters, loggingTypes),
                ILocalReferenceOperation localRef => TryExtractFromLocalReference(localRef, messageParameters, loggingTypes),
                IFieldReferenceOperation fieldRef => TryExtractFromFieldReference(fieldRef, messageParameters, loggingTypes),
                _ => false
            };

            if (extracted)
            {
                usage.MessageParameters = messageParameters;
            }

            return extracted;
        }

        /// <summary>
        /// Extracts parameters from collection initializer operations.
        /// </summary>
        public static void ExtractFromCollectionInitializer(IObjectOrCollectionInitializerOperation initializer, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            foreach (var elementInitializer in initializer.Initializers)
            {
                switch (elementInitializer)
                {
                    case IObjectCreationOperation keyValuePairCreation:
                        ExtractKeyValuePairFromObjectCreation(keyValuePairCreation, messageParameters, loggingTypes);
                        break;
                    case IInvocationOperation invocation:
                        ExtractFromInvocation(invocation, messageParameters, loggingTypes);
                        break;
                    case ISimpleAssignmentOperation assignment:
                        ExtractFromAssignment(assignment, messageParameters);
                        break;
                }
            }
        }

        /// <summary>
        /// Extracts parameters from array initializer operations.
        /// </summary>
        public static void ExtractFromArrayInitializer(IArrayInitializerOperation initializer, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            foreach (var element in initializer.ElementValues)
            {
                var unwrappedElement = element.UnwrapConversion();
                switch (unwrappedElement)
                {
                    case IInvocationOperation invocation when invocation.Arguments.Length >= 2:
                        ExtractFromInvocationArguments(invocation, messageParameters);
                        break;
                    case IObjectCreationOperation keyValuePairCreation:
                        ExtractKeyValuePairFromObjectCreation(keyValuePairCreation, messageParameters, loggingTypes);
                        break;
                }
            }
        }

        /// <summary>
        /// Checks if a type implements IEnumerable&lt;KeyValuePair&lt;string, object?&gt;&gt;.
        /// </summary>
        public static bool IsKeyValuePairEnumerable(ITypeSymbol type, LoggingTypes loggingTypes)
        {
            if (type is not INamedTypeSymbol namedType)
                return false;

            // Direct check for IEnumerable<KeyValuePair<string, object?>>
            if (namedType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                var typeArg = namedType.TypeArguments.FirstOrDefault();
                return IsKeyValuePairType(typeArg, loggingTypes);
            }

            // Check implemented interfaces
            return namedType.AllInterfaces.Any(interfaceType =>
                interfaceType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T &&
                IsKeyValuePairType(interfaceType.TypeArguments.FirstOrDefault(), loggingTypes));
        }

        /// <summary>
        /// Checks if a type is KeyValuePair&lt;string, object?&gt;.
        /// </summary>
        public static bool IsKeyValuePairType(ITypeSymbol? type, LoggingTypes loggingTypes)
        {
            if (type is not INamedTypeSymbol namedType)
                return false;

            return SymbolEqualityComparer.Default.Equals(namedType, loggingTypes.KeyValuePairOfStringNullableObject);
        }

        private static bool TryExtractFromObjectCreation(IObjectCreationOperation objectCreation, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            if (objectCreation.Type != null && IsKeyValuePairEnumerable(objectCreation.Type, loggingTypes))
            {
                if (objectCreation.Initializer != null)
                {
                    ExtractFromCollectionInitializer(objectCreation.Initializer, messageParameters, loggingTypes);
                }
                return true;
            }
            return false;
        }

        private static bool TryExtractFromArrayCreation(IArrayCreationOperation arrayCreation, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            if (arrayCreation.Type is IArrayTypeSymbol arrayType && IsKeyValuePairType(arrayType.ElementType, loggingTypes))
            {
                if (arrayCreation.Initializer != null)
                {
                    ExtractFromArrayInitializer(arrayCreation.Initializer, messageParameters, loggingTypes);
                }
                return true;
            }
            return false;
        }

        private static bool TryExtractFromLocalReference(ILocalReferenceOperation localRef, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            if (localRef.Local.Type != null && IsKeyValuePairEnumerable(localRef.Local.Type, loggingTypes))
            {
                var parameter = ScopeParameterExtractor.CreateMessageParameter(
                    $"<{localRef.Local.Name}>",
                    localRef.Local.Type.ToPrettyDisplayString(),
                    localRef.Kind.ToString()
                );
                messageParameters.Add(parameter);
                return true;
            }
            return false;
        }

        private static bool TryExtractFromFieldReference(IFieldReferenceOperation fieldRef, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            if (fieldRef.Field.Type != null && IsKeyValuePairEnumerable(fieldRef.Field.Type, loggingTypes))
            {
                var parameter = ScopeParameterExtractor.CreateMessageParameter(
                    $"<{fieldRef.Field.Name}>",
                    fieldRef.Field.Type.ToPrettyDisplayString(),
                    fieldRef.Kind.ToString()
                );
                messageParameters.Add(parameter);
                return true;
            }
            return false;
        }

        private static void ExtractKeyValuePairFromObjectCreation(IObjectCreationOperation keyValuePairCreation, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            if (keyValuePairCreation.Arguments.Length >= 2)
            {
                ExtractFromKeyValueArguments(keyValuePairCreation.Arguments[0].Value, keyValuePairCreation.Arguments[1].Value, messageParameters);
            }
            else if (keyValuePairCreation.Initializer is not null)
            {
                ExtractFromCollectionInitializer(keyValuePairCreation.Initializer, messageParameters, loggingTypes);
            }
        }

        private static void ExtractFromInvocation(IInvocationOperation invocation, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            if (invocation.Arguments.Length >= 2)
            {
                ExtractFromKeyValueArguments(invocation.Arguments[0].Value, invocation.Arguments[1].Value, messageParameters);
            }
            else if (invocation.Arguments.Length == 1)
            {
                var argument = invocation.Arguments[0];
                if (argument.Value is ILiteralOperation keyLiteral &&
                    keyLiteral.ConstantValue.HasValue &&
                    keyLiteral.ConstantValue.Value is string key)
                {
                    var parameter = ScopeParameterExtractor.CreateMessageParameter(key, "object", "Constant");
                    messageParameters.Add(parameter);
                }
                else if (loggingTypes.KeyValuePairOfStringNullableObject.Equals(argument.Value.Type, SymbolEqualityComparer.Default))
                {
                    if (argument.Value.UnwrapConversion() is IObjectCreationOperation kvpCreation)
                    {
                        ExtractKeyValuePairFromObjectCreation(kvpCreation, messageParameters, loggingTypes);
                    }
                }
            }
        }

        private static void ExtractFromAssignment(ISimpleAssignmentOperation assignment, List<MessageParameter> messageParameters)
        {
            // Handle ["key"] = value syntax for Dictionary
            if (assignment.Target is IPropertyReferenceOperation propertyReference &&
                propertyReference.Arguments.Length == 1 &&
                propertyReference.Arguments[0].Value is ILiteralOperation keyLiteral &&
                keyLiteral.ConstantValue.HasValue &&
                keyLiteral.ConstantValue.Value is string key)
            {
                var valueArg = assignment.Value.UnwrapConversion();
                var parameter = ScopeParameterExtractor.CreateMessageParameter(
                    key,
                    valueArg.Type?.ToPrettyDisplayString() ?? "object",
                    valueArg.ConstantValue.HasValue ? "Constant" : valueArg.Kind.ToString()
                );
                messageParameters.Add(parameter);
            }
        }

        private static void ExtractFromInvocationArguments(IInvocationOperation invocation, List<MessageParameter> messageParameters)
        {
            var keyArg = invocation.Arguments[0].Value;
            var valueArg = invocation.Arguments[1].Value.UnwrapConversion();

            if (keyArg is ILiteralOperation keyLiteral &&
                keyLiteral.ConstantValue.HasValue &&
                keyLiteral.ConstantValue.Value is string key)
            {
                var parameter = ScopeParameterExtractor.CreateMessageParameter(
                    key,
                    valueArg.Type?.ToPrettyDisplayString() ?? "object",
                    valueArg.ConstantValue.HasValue ? "Constant" : valueArg.Kind.ToString()
                );
                messageParameters.Add(parameter);
            }
        }

        private static void ExtractFromKeyValueArguments(IOperation keyArg, IOperation valueArg, List<MessageParameter> messageParameters)
        {
            var unwrappedValueArg = valueArg.UnwrapConversion();

            if (keyArg is ILiteralOperation keyLiteral &&
                keyLiteral.ConstantValue.HasValue &&
                keyLiteral.ConstantValue.Value is string key)
            {
                var parameter = ScopeParameterExtractor.CreateMessageParameter(
                    key,
                    unwrappedValueArg.Type?.ToPrettyDisplayString() ?? "object",
                    unwrappedValueArg.ConstantValue.HasValue ? "Constant" : unwrappedValueArg.Kind.ToString()
                );
                messageParameters.Add(parameter);
            }
        }
    }
}
