using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using LoggerUsage.Models;
using LoggerUsage.Analyzers;
using LoggerUsage.Utilities;

namespace LoggerUsage.Services
{
    /// <summary>
    /// Implementation of KeyValuePair extraction service that handles extraction and validation of KeyValuePair collections.
    /// </summary>
    public class KeyValuePairExtractionService : IKeyValuePairExtractionService
    {
        private readonly ILogger<KeyValuePairExtractionService> _logger;

        public KeyValuePairExtractionService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<KeyValuePairExtractionService>();
        }

        public List<MessageParameter> TryExtractParameters(IArgumentOperation argument, LoggingTypes loggingTypes)
        {
            try
            {
                _logger.LogDebug("Attempting to extract KeyValuePair parameters from argument");

                var messageParameters = new List<MessageParameter>();

                bool extracted = argument.Value switch
                {
                    IObjectCreationOperation objectCreation => TryExtractFromObjectCreation(objectCreation, messageParameters, loggingTypes),
                    IArrayCreationOperation arrayCreation => TryExtractFromArrayCreation(arrayCreation, messageParameters, loggingTypes),
                    ILocalReferenceOperation localRef => TryExtractFromLocalReference(localRef, messageParameters, loggingTypes),
                    IFieldReferenceOperation fieldRef => TryExtractFromFieldReference(fieldRef, messageParameters, loggingTypes),
                    _ => false
                };

                if (extracted)
                {
                    _logger.LogDebug("Successfully extracted {Count} KeyValuePair parameters", messageParameters.Count);
                    return messageParameters;
                }
                else
                {
                    _logger.LogDebug("No KeyValuePair parameters extracted - not a KeyValuePair type");
                    return []; // Empty but indicating failed extraction
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract KeyValuePair parameters");
                return [];
            }
        }

        public bool IsKeyValuePairEnumerable(ITypeSymbol? type, LoggingTypes loggingTypes)
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

        public bool IsKeyValuePairType(ITypeSymbol? type, LoggingTypes loggingTypes)
        {
            if (type is not INamedTypeSymbol namedType)
                return false;

            return SymbolEqualityComparer.Default.Equals(namedType, loggingTypes.KeyValuePairOfStringNullableObject);
        }

        private bool TryExtractFromObjectCreation(IObjectCreationOperation objectCreation, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
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

        private bool TryExtractFromArrayCreation(IArrayCreationOperation arrayCreation, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
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

        private bool TryExtractFromLocalReference(ILocalReferenceOperation localRef, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            if (localRef.Local.Type != null && IsKeyValuePairEnumerable(localRef.Local.Type, loggingTypes))
            {
                var parameter = MessageParameterFactory.CreateFromReference(
                    localRef.Local.Name,
                    localRef.Local.Type,
                    localRef.Kind
                );
                messageParameters.Add(parameter);
                return true;
            }
            return false;
        }

        private bool TryExtractFromFieldReference(IFieldReferenceOperation fieldRef, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            if (fieldRef.Field.Type != null && IsKeyValuePairEnumerable(fieldRef.Field.Type, loggingTypes))
            {
                var parameter = MessageParameterFactory.CreateFromReference(
                    fieldRef.Field.Name,
                    fieldRef.Field.Type,
                    fieldRef.Kind
                );
                messageParameters.Add(parameter);
                return true;
            }
            return false;
        }

        private void ExtractFromCollectionInitializer(IObjectOrCollectionInitializerOperation initializer, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
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

        private void ExtractFromArrayInitializer(IArrayInitializerOperation initializer, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
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

        private void ExtractKeyValuePairFromObjectCreation(IObjectCreationOperation keyValuePairCreation, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
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

        private void ExtractFromInvocation(IInvocationOperation invocation, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            // Handle method calls that create KeyValuePair instances
            if (invocation.Arguments.Length is 1)
            {
                var operation = invocation.Arguments[0].Value.UnwrapConversion();
                if (operation is IObjectCreationOperation objectCreation)
                {
                    ExtractKeyValuePairFromObjectCreation(objectCreation, messageParameters, loggingTypes);
                }
            }
            else if (invocation.Arguments.Length >= 2)
            {
                ExtractFromInvocationArguments(invocation, messageParameters);
            }
        }

        private void ExtractFromInvocationArguments(IInvocationOperation invocation, List<MessageParameter> messageParameters)
        {
            var keyArg = invocation.Arguments[0].Value;
            var valueArg = invocation.Arguments[1].Value;

            if (keyArg.ConstantValue.HasValue && keyArg.ConstantValue.Value is string key)
            {
                messageParameters.Add(MessageParameterFactory.CreateFromKeyValue(key, valueArg));
            }
        }

        private void ExtractFromAssignment(ISimpleAssignmentOperation assignment, List<MessageParameter> messageParameters)
        {
            var value = assignment.Value.UnwrapConversion();

            // Handle dictionary-style initialization like { ["key"] = value }
            if (assignment.Target is IPropertyReferenceOperation propertyRef &&
                propertyRef.Arguments.Length > 0 &&
                propertyRef.Arguments[0].Value is ILiteralOperation keyLiteral &&
                keyLiteral.ConstantValue.HasValue &&
                keyLiteral.ConstantValue.Value is string key)
            {
                messageParameters.Add(MessageParameterFactory.CreateFromKeyValue(key, value));
            }
            // Handle other direct literal assignments (if any)
            else if (assignment.Target is ILiteralOperation literal &&
                literal.ConstantValue.HasValue &&
                literal.ConstantValue.Value is string directKey)
            {
                messageParameters.Add(MessageParameterFactory.CreateFromKeyValue(directKey, value));
            }
        }

        private void ExtractFromKeyValueArguments(IOperation keyArg, IOperation valueArg, List<MessageParameter> messageParameters)
        {
            if (keyArg is ILiteralOperation keyLiteral &&
                keyLiteral.ConstantValue.HasValue &&
                keyLiteral.ConstantValue.Value is string key)
            {
                var parameter = MessageParameterFactory.CreateFromKeyValue(key, valueArg);
                messageParameters.Add(parameter);
            }
        }
    }
}
