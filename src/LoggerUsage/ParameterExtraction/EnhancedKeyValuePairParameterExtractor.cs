using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using LoggerUsage.Models;

namespace LoggerUsage.ParameterExtraction;

/// <summary>
/// Enhanced parameter extractor interface with comprehensive error handling.
/// </summary>
internal interface IEnhancedParameterExtractor : IParameterExtractor
{
    /// <summary>
    /// Extracts parameters with detailed error reporting.
    /// </summary>
    /// <param name="operation">The operation to extract parameters from</param>
    /// <param name="loggingTypes">The logging types context</param>
    /// <param name="messageTemplate">Optional message template for context</param>
    /// <returns>Extraction result containing parameters or error information</returns>
    ExtractionResult<List<MessageParameter>> ExtractWithResult(
        IOperation operation, 
        LoggingTypes loggingTypes, 
        string? messageTemplate = null);
}

/// <summary>
/// Enhanced KeyValuePair parameter extractor with comprehensive error handling and diagnostics.
/// </summary>
internal class EnhancedKeyValuePairParameterExtractor : IEnhancedParameterExtractor
{
    private readonly ILogger<EnhancedKeyValuePairParameterExtractor> _logger;

    public EnhancedKeyValuePairParameterExtractor(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<EnhancedKeyValuePairParameterExtractor>();
    }

    public bool TryExtractParameters(
        IOperation operation, 
        LoggingTypes loggingTypes, 
        string? messageTemplate, 
        out List<MessageParameter> parameters)
    {
        var result = ExtractWithResult(operation, loggingTypes, messageTemplate);
        parameters = result.Value ?? new List<MessageParameter>();
        return result.IsSuccess;
    }

    public ExtractionResult<List<MessageParameter>> ExtractWithResult(
        IOperation operation, 
        LoggingTypes loggingTypes, 
        string? messageTemplate = null)
    {
        try
        {
            if (operation == null)
            {
                _logger.LogWarning("Cannot extract parameters from null operation");
                return ExtractionResult<List<MessageParameter>>.Failure("Operation is null");
            }

            if (loggingTypes == null)
            {
                _logger.LogWarning("Cannot extract parameters without logging types context");
                return ExtractionResult<List<MessageParameter>>.Failure("LoggingTypes is null");
            }

            _logger.LogDebug("Attempting to extract KeyValuePair parameters from {OperationType}", operation.GetType().Name);

            var parameters = new List<MessageParameter>();

            var extractionResult = operation switch
            {
                IObjectCreationOperation objectCreation => ExtractFromObjectCreation(objectCreation, parameters, loggingTypes),
                IArrayCreationOperation arrayCreation => ExtractFromArrayCreation(arrayCreation, parameters, loggingTypes),
                ILocalReferenceOperation localRef => ExtractFromLocalReference(localRef, parameters, loggingTypes),
                IFieldReferenceOperation fieldRef => ExtractFromFieldReference(fieldRef, parameters, loggingTypes),
                _ => ExtractionResult.Failure($"Unsupported operation type for KeyValuePair extraction: {operation.GetType().Name}")
            };

            if (!extractionResult.IsSuccess)
            {
                _logger.LogDebug("Failed to extract KeyValuePair parameters: {Error}", extractionResult.ErrorMessage);
                return ExtractionResult<List<MessageParameter>>.Failure(extractionResult.ErrorMessage!, extractionResult.Exception!);
            }

            _logger.LogDebug("Successfully extracted {Count} KeyValuePair parameters", parameters.Count);
            return ExtractionResult<List<MessageParameter>>.Success(parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while extracting KeyValuePair parameters from {OperationType}", operation.GetType().Name);
            return ExtractionResult<List<MessageParameter>>.Failure($"Exception during KeyValuePair extraction: {ex.Message}", ex);
        }
    }

    private ExtractionResult ExtractFromObjectCreation(IObjectCreationOperation objectCreation, List<MessageParameter> parameters, LoggingTypes loggingTypes)
    {
        try
        {
            if (!IsKeyValuePairEnumerable(objectCreation.Type, loggingTypes))
            {
                _logger.LogDebug("Object creation type {Type} is not a KeyValuePair enumerable", objectCreation.Type?.ToDisplayString());
                return ExtractionResult.Failure($"Type {objectCreation.Type?.ToDisplayString()} is not a KeyValuePair enumerable");
            }

            if (objectCreation.Initializer == null)
            {
                _logger.LogDebug("Object creation has no initializer");
                return ExtractionResult.Failure("Object creation has no initializer");
            }

            var result = ExtractFromCollectionInitializer(objectCreation.Initializer, parameters, loggingTypes);
            if (!result.IsSuccess)
            {
                return result;
            }

            if (parameters.Count == 0)
            {
                _logger.LogDebug("No parameters extracted from object creation initializer");
                return ExtractionResult.Failure("No parameters found in object creation initializer");
            }

            return ExtractionResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting from object creation operation");
            return ExtractionResult.Failure($"Error extracting from object creation: {ex.Message}", ex);
        }
    }

    private ExtractionResult ExtractFromArrayCreation(IArrayCreationOperation arrayCreation, List<MessageParameter> parameters, LoggingTypes loggingTypes)
    {
        try
        {
            if (!IsKeyValuePairEnumerable(arrayCreation.Type, loggingTypes))
            {
                _logger.LogDebug("Array creation type {Type} is not a KeyValuePair enumerable", arrayCreation.Type?.ToDisplayString());
                return ExtractionResult.Failure($"Array type {arrayCreation.Type?.ToDisplayString()} is not a KeyValuePair enumerable");
            }

            if (arrayCreation.Initializer == null)
            {
                _logger.LogDebug("Array creation has no initializer");
                return ExtractionResult.Failure("Array creation has no initializer");
            }

            var result = ExtractFromArrayInitializer(arrayCreation.Initializer, parameters, loggingTypes);
            if (!result.IsSuccess)
            {
                return result;
            }

            if (parameters.Count == 0)
            {
                _logger.LogDebug("No parameters extracted from array creation initializer");
                return ExtractionResult.Failure("No parameters found in array creation initializer");
            }

            return ExtractionResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting from array creation operation");
            return ExtractionResult.Failure($"Error extracting from array creation: {ex.Message}", ex);
        }
    }

    private ExtractionResult ExtractFromLocalReference(ILocalReferenceOperation localRef, List<MessageParameter> parameters, LoggingTypes loggingTypes)
    {
        try
        {
            if (!IsKeyValuePairEnumerable(localRef.Type, loggingTypes))
            {
                _logger.LogDebug("Local reference type {Type} is not a KeyValuePair enumerable", localRef.Type?.ToDisplayString());
                return ExtractionResult.Failure($"Local variable type {localRef.Type?.ToDisplayString()} is not a KeyValuePair enumerable");
            }

            // For local references, we can't extract the actual values at compile time
            // But we can record that this is a KeyValuePair collection
            _logger.LogDebug("Local reference {LocalName} is a KeyValuePair enumerable but values cannot be determined at compile time", localRef.Local.Name);
            
            // Add a placeholder parameter to indicate this is a KeyValuePair collection
            parameters.Add(new MessageParameter(
                Name: $"<{localRef.Local.Name}>",
                Type: localRef.Type?.ToDisplayString(),
                Kind: "LocalReference"
            ));

            return ExtractionResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting from local reference operation");
            return ExtractionResult.Failure($"Error extracting from local reference: {ex.Message}", ex);
        }
    }

    private ExtractionResult ExtractFromFieldReference(IFieldReferenceOperation fieldRef, List<MessageParameter> parameters, LoggingTypes loggingTypes)
    {
        try
        {
            if (!IsKeyValuePairEnumerable(fieldRef.Type, loggingTypes))
            {
                _logger.LogDebug("Field reference type {Type} is not a KeyValuePair enumerable", fieldRef.Type?.ToDisplayString());
                return ExtractionResult.Failure($"Field type {fieldRef.Type?.ToDisplayString()} is not a KeyValuePair enumerable");
            }

            // For field references, we can't extract the actual values at compile time
            // But we can record that this is a KeyValuePair collection
            _logger.LogDebug("Field reference {FieldName} is a KeyValuePair enumerable but values cannot be determined at compile time", fieldRef.Field.Name);
            
            // Add a placeholder parameter to indicate this is a KeyValuePair collection
            parameters.Add(new MessageParameter(
                Name: $"<{fieldRef.Field.Name}>",
                Type: fieldRef.Type?.ToDisplayString(),
                Kind: "FieldReference"
            ));

            return ExtractionResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting from field reference operation");
            return ExtractionResult.Failure($"Error extracting from field reference: {ex.Message}", ex);
        }
    }

    private ExtractionResult ExtractFromCollectionInitializer(IObjectOrCollectionInitializerOperation initializer, List<MessageParameter> parameters, LoggingTypes loggingTypes)
    {
        try
        {
            foreach (var elementInitializer in initializer.Initializers)
            {
                var result = elementInitializer switch
                {
                    IObjectCreationOperation keyValuePairCreation => ExtractKeyValuePairFromObjectCreation(keyValuePairCreation, parameters, loggingTypes),
                    IInvocationOperation invocation => ExtractFromInvocation(invocation, parameters, loggingTypes),
                    ISimpleAssignmentOperation assignment => ExtractFromAssignment(assignment, parameters),
                    _ => ExtractionResult.Failure($"Unsupported initializer element type: {elementInitializer.GetType().Name}")
                };

                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Failed to extract from collection initializer element {ElementType}: {Error}", 
                        elementInitializer.GetType().Name, result.ErrorMessage);
                    // Continue processing other elements rather than failing completely
                }
            }

            return ExtractionResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting from collection initializer");
            return ExtractionResult.Failure($"Error extracting from collection initializer: {ex.Message}", ex);
        }
    }

    private ExtractionResult ExtractFromArrayInitializer(IArrayInitializerOperation initializer, List<MessageParameter> parameters, LoggingTypes loggingTypes)
    {
        try
        {
            foreach (var element in initializer.ElementValues)
            {
                var unwrappedElement = element.UnwrapConversion();
                var result = unwrappedElement switch
                {
                    IInvocationOperation invocation when invocation.Arguments.Length >= 2 => ExtractFromInvocationArguments(invocation, parameters),
                    IObjectCreationOperation keyValuePairCreation => ExtractKeyValuePairFromObjectCreation(keyValuePairCreation, parameters, loggingTypes),
                    _ => ExtractionResult.Failure($"Unsupported array element type: {unwrappedElement.GetType().Name}")
                };

                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Failed to extract from array element {ElementType}: {Error}", 
                        unwrappedElement.GetType().Name, result.ErrorMessage);
                    // Continue processing other elements rather than failing completely
                }
            }

            return ExtractionResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting from array initializer");
            return ExtractionResult.Failure($"Error extracting from array initializer: {ex.Message}", ex);
        }
    }

    private ExtractionResult ExtractKeyValuePairFromObjectCreation(IObjectCreationOperation objectCreation, List<MessageParameter> parameters, LoggingTypes loggingTypes)
    {
        try
        {
            if (!IsKeyValuePairType(objectCreation.Type, loggingTypes))
            {
                _logger.LogDebug("Object creation type {Type} is not a KeyValuePair", objectCreation.Type?.ToDisplayString());
                return ExtractionResult.Failure($"Type {objectCreation.Type?.ToDisplayString()} is not a KeyValuePair");
            }

            if (objectCreation.Arguments.Length >= 2)
            {
                return ExtractFromKeyValueArguments(objectCreation.Arguments[0].Value, objectCreation.Arguments[1].Value, parameters);
            }
            else if (objectCreation.Initializer != null)
            {
                return ExtractFromCollectionInitializer(objectCreation.Initializer, parameters, loggingTypes);
            }

            _logger.LogDebug("KeyValuePair object creation has insufficient arguments and no initializer");
            return ExtractionResult.Failure("KeyValuePair object creation has insufficient arguments and no initializer");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting KeyValuePair from object creation");
            return ExtractionResult.Failure($"Error extracting KeyValuePair from object creation: {ex.Message}", ex);
        }
    }

    private ExtractionResult ExtractFromInvocation(IInvocationOperation invocation, List<MessageParameter> parameters, LoggingTypes loggingTypes)
    {
        try
        {
            if (invocation.Arguments.Length >= 2)
            {
                return ExtractFromKeyValueArguments(invocation.Arguments[0].Value, invocation.Arguments[1].Value, parameters);
            }
            else if (invocation.Arguments.Length == 1)
            {
                var argument = invocation.Arguments[0];
                if (argument.Value is ILiteralOperation keyLiteral &&
                    keyLiteral.ConstantValue.HasValue &&
                    keyLiteral.ConstantValue.Value is string key)
                {
                    parameters.Add(new MessageParameter(key, "object", "Constant"));
                    return ExtractionResult.Success();
                }
                else if (IsKeyValuePairType(argument.Value.Type, loggingTypes))
                {
                    if (argument.Value is IObjectCreationOperation kvpCreation)
                    {
                        return ExtractKeyValuePairFromObjectCreation(kvpCreation, parameters, loggingTypes);
                    }
                }
            }

            _logger.LogDebug("Invocation operation has unsupported argument structure");
            return ExtractionResult.Failure("Invocation operation has unsupported argument structure");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting from invocation operation");
            return ExtractionResult.Failure($"Error extracting from invocation: {ex.Message}", ex);
        }
    }

    private ExtractionResult ExtractFromAssignment(ISimpleAssignmentOperation assignment, List<MessageParameter> parameters)
    {
        try
        {
            var value = assignment.Value.UnwrapConversion();

            // Handle dictionary-style initialization like { ["key"] = value }
            if (assignment.Target is IPropertyReferenceOperation propertyRef &&
                propertyRef.Arguments.Length > 0 &&
                propertyRef.Arguments[0].Value is ILiteralOperation keyLiteral &&
                keyLiteral.ConstantValue.HasValue &&
                keyLiteral.ConstantValue.Value is string key)
            {
                parameters.Add(new MessageParameter(
                    Name: key,
                    Type: value.Type?.ToDisplayString(),
                    Kind: value.ConstantValue.HasValue ? "Constant" : value.Kind.ToString()
                ));
                return ExtractionResult.Success();
            }

            _logger.LogDebug("Assignment operation has unsupported target structure");
            return ExtractionResult.Failure("Assignment operation has unsupported target structure");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting from assignment operation");
            return ExtractionResult.Failure($"Error extracting from assignment: {ex.Message}", ex);
        }
    }

    private ExtractionResult ExtractFromInvocationArguments(IInvocationOperation invocation, List<MessageParameter> parameters)
    {
        try
        {
            var keyArg = invocation.Arguments[0].Value;
            var valueArg = invocation.Arguments[1].Value;

            if (keyArg.ConstantValue.HasValue && keyArg.ConstantValue.Value is string key)
            {
                parameters.Add(new MessageParameter(
                    Name: key,
                    Type: valueArg.Type?.ToDisplayString() ?? "object",
                    Kind: valueArg.ConstantValue.HasValue ? "Constant" : valueArg.Kind.ToString()
                ));
                return ExtractionResult.Success();
            }

            _logger.LogDebug("Invocation arguments do not contain a constant string key");
            return ExtractionResult.Failure("Invocation arguments do not contain a constant string key");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting from invocation arguments");
            return ExtractionResult.Failure($"Error extracting from invocation arguments: {ex.Message}", ex);
        }
    }

    private ExtractionResult ExtractFromKeyValueArguments(IOperation keyArg, IOperation valueArg, List<MessageParameter> parameters)
    {
        try
        {
            var value = valueArg.UnwrapConversion();

            if (keyArg is ILiteralOperation keyLiteral &&
                keyLiteral.ConstantValue.HasValue &&
                keyLiteral.ConstantValue.Value is string key)
            {
                parameters.Add(new MessageParameter(
                    Name: key,
                    Type: value.Type?.ToDisplayString() ?? "object",
                    Kind: value.ConstantValue.HasValue ? "Constant" : value.Kind.ToString()
                ));
                return ExtractionResult.Success();
            }

            _logger.LogDebug("Key argument is not a constant string literal");
            return ExtractionResult.Failure("Key argument is not a constant string literal");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting from key-value arguments");
            return ExtractionResult.Failure($"Error extracting from key-value arguments: {ex.Message}", ex);
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
