using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Analyzers
{
    internal class BeginScopeAnalyzer(ILoggerFactory loggerFactory) : ILoggerUsageAnalyzer
    {
        private readonly ILogger<BeginScopeAnalyzer> _logger = loggerFactory.CreateLogger<BeginScopeAnalyzer>();

        public IEnumerable<LoggerUsageInfo> Analyze(LoggingTypes loggingTypes, SyntaxNode root, SemanticModel semanticModel)
        {
            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                if (semanticModel.GetOperation(invocation) is not IInvocationOperation operation)
                    continue;

                if (!loggingTypes.LoggerExtensionModeler.IsBeginScopeMethod(operation.TargetMethod))
                    continue;

                yield return ExtractBeginScopeUsage(operation, loggingTypes, invocation);
            }
        }

        private static LoggerUsageInfo ExtractBeginScopeUsage(IInvocationOperation operation, LoggingTypes loggingTypes, InvocationExpressionSyntax invocation)
        {
            var usage = new LoggerUsageInfo
            {
                MethodName = operation.TargetMethod.Name,
                MethodType = LoggerUsageMethodType.BeginScope,
                Location = new MethodCallLocation
                {
                    StartLineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line,
                    EndLineNumber = invocation.GetLocation().GetLineSpan().EndLinePosition.Line,
                    FilePath = invocation.GetLocation().SourceTree!.FilePath
                },
            };

            // Extract scope state information
            ExtractScopeState(operation, usage, loggingTypes);

            return usage;
        }

        private static void ExtractScopeState(IInvocationOperation operation, LoggerUsageInfo usage, LoggingTypes loggingTypes)
        {
            // For extension methods, skip the first argument (this parameter)
            // For core methods, use the first argument
            int argumentIndex = operation.TargetMethod.IsExtensionMethod ? 1 : 0;

            if (operation.Arguments.Length <= argumentIndex)
                return;

            var stateArgument = operation.Arguments[argumentIndex];

            // Extract message template from the state argument
            if (stateArgument.Value is ILiteralOperation literal && literal.ConstantValue.HasValue)
            {
                usage.MessageTemplate = literal.ConstantValue.Value?.ToString();
            }

            // Extract message parameters based on the argument type
            if (operation.TargetMethod.IsExtensionMethod && usage.MessageTemplate != null)
            {
                ExtractMessageParameters(operation, usage);
            }
            else if (!operation.TargetMethod.IsExtensionMethod)
            {
                // For core ILogger.BeginScope method, try to extract key-value pairs if the argument is a KeyValuePair collection
                if (TryExtractKeyValuePairParameters(stateArgument, usage, loggingTypes))
                {
                    // Key-value pairs were extracted successfully
                }
                else if (stateArgument.Value is IAnonymousObjectCreationOperation objectCreation)
                {
                    ExtractAnonymousObjectProperties(objectCreation, usage, loggingTypes);
                }
            }
        }

        private static void ExtractAnonymousObjectProperties(IAnonymousObjectCreationOperation objectCreation, LoggerUsageInfo usage, LoggingTypes loggingTypes)
        {
            foreach (var property in objectCreation.Initializers)
            {
                if (property is not ISimpleAssignmentOperation assignment)
                    continue;

                var propertyName = assignment.Target.Syntax switch
                {
                    IdentifierNameSyntax identifier => identifier.Identifier.Text,
                    _ => null
                };
                if (propertyName == null)
                    continue;

                var value = assignment.Value.ConstantValue;
                usage.MessageParameters ??= new List<MessageParameter>();
                usage.MessageParameters.Add(new MessageParameter(
                    Name: propertyName,
                    Type: assignment.Value.Type?.ToPrettyDisplayString() ?? "object",
                    Kind: value.HasValue ? "Constant" : assignment.Value.Kind.ToString()
                ));
            }
        }

        private static void ExtractMessageParameters(IInvocationOperation operation, LoggerUsageInfo usage)
        {
            var messageTemplate = usage.MessageTemplate;
            if (string.IsNullOrEmpty(messageTemplate))
                return;

            var formatter = new LogValuesFormatter(messageTemplate);
            if (formatter.ValueNames.Count == 0)
                return;

            var messageParameters = new List<MessageParameter>();

            // For extension methods, the params array is in argument index 2 (after 'this' and messageFormat)
            if (operation.Arguments.Length > 2)
            {
                var paramsArgument = operation.Arguments[2].Value.UnwrapConversion();

                // Check if this is an array creation with elements
                if (paramsArgument is IArrayCreationOperation arrayCreation && arrayCreation.Initializer != null)
                {
                    // Extract individual elements from the params array
                    for (int i = 0; i < arrayCreation.Initializer.ElementValues.Length && i < formatter.ValueNames.Count; i++)
                    {
                        var element = arrayCreation.Initializer.ElementValues[i].UnwrapConversion();
                        var parameterName = formatter.ValueNames[i];

                        messageParameters.Add(new MessageParameter(
                            Name: parameterName,
                            Type: element.Type?.ToPrettyDisplayString() ?? "object",
                            Kind: element.ConstantValue.HasValue ? "Constant" : element.Kind.ToString()
                        ));
                    }
                }
                else
                {
                    // Fallback: if not an array creation, treat as single parameter
                    if (formatter.ValueNames.Count > 0)
                    {
                        messageParameters.Add(new MessageParameter(
                            Name: formatter.ValueNames[0],
                            Type: paramsArgument.Type?.ToPrettyDisplayString() ?? "object",
                            Kind: paramsArgument.ConstantValue.HasValue ? "Constant" : paramsArgument.Kind.ToString()
                        ));
                    }
                }
            }

            usage.MessageParameters = messageParameters;
        }

        private static bool TryExtractKeyValuePairParameters(IArgumentOperation stateArgument, LoggerUsageInfo usage, LoggingTypes loggingTypes)
        {
            var messageParameters = new List<MessageParameter>();

            // Handle different types of collections that implement IEnumerable<KeyValuePair<string, object?>>
            if (stateArgument.Value is IObjectCreationOperation objectCreation)
            {
                // Check if this is a KeyValuePair collection before extracting
                if (objectCreation.Type != null && IsKeyValuePairEnumerable(objectCreation.Type, loggingTypes))
                {
                    // Handle new List<KeyValuePair<string, object?>> { ... }
                    // Handle new Dictionary<string, object?> { ... }
                    if (objectCreation.Initializer != null)
                    {
                        ExtractFromCollectionInitializer(objectCreation.Initializer, messageParameters, loggingTypes);
                    }
                    usage.MessageParameters = messageParameters;
                    return true;
                }
            }
            else if (stateArgument.Value is IArrayCreationOperation arrayCreation)
            {
                // Check if this is a KeyValuePair array before extracting
                if (arrayCreation.Type is IArrayTypeSymbol arrayType && IsKeyValuePairType(arrayType.ElementType, loggingTypes))
                {
                    // Handle new KeyValuePair<string, object?>[] { ... }
                    if (arrayCreation.Initializer != null)
                    {
                        ExtractFromArrayInitializer(arrayCreation.Initializer, messageParameters, loggingTypes);
                    }
                    usage.MessageParameters = messageParameters;
                    return true;
                }
            }
            else if (stateArgument.Value is ILocalReferenceOperation localRef)
            {
                // Check if this is a KeyValuePair collection before extracting
                if (localRef.Local.Type != null && IsKeyValuePairEnumerable(localRef.Local.Type, loggingTypes))
                {
                    // Handle variables that hold collections
                    ExtractFromLocalReference(localRef, messageParameters, loggingTypes);
                    usage.MessageParameters = messageParameters;
                    return true;
                }
            }
            else if (stateArgument.Value is IFieldReferenceOperation fieldRef)
            {
                // Check if this is a KeyValuePair collection before extracting
                if (fieldRef.Field.Type != null && IsKeyValuePairEnumerable(fieldRef.Field.Type, loggingTypes))
                {
                    // Handle field references that hold collections
                    ExtractFromFieldReference(fieldRef, messageParameters, loggingTypes);
                    usage.MessageParameters = messageParameters;
                    return true;
                }
            }

            return false;
        }

        private static void ExtractFromCollectionInitializer(IObjectOrCollectionInitializerOperation initializer, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            foreach (var elementInitializer in initializer.Initializers)
            {
                if (elementInitializer is IObjectCreationOperation keyValuePairCreation)
                {
                    // Handle new KeyValuePair<string, object?>("key", value) syntax
                    // Also handles new("key", value) target-typed syntax
                    ExtractKeyValuePairFromObjectCreation(keyValuePairCreation, messageParameters, loggingTypes);
                }
                else if (elementInitializer is IInvocationOperation invocation)
                {
                    // Handle Add() method calls or other invocation patterns
                    if (invocation.Arguments.Length >= 2)
                    {
                        var keyArg = invocation.Arguments[0].Value;
                        var valueArg = invocation.Arguments[1].Value;

                        if (keyArg is ILiteralOperation keyLiteral &&
                            keyLiteral.ConstantValue.HasValue &&
                            keyLiteral.ConstantValue.Value is string key)
                        {
                            var parameter = new MessageParameter(
                                Name: key,
                                Type: valueArg.Type?.ToPrettyDisplayString() ?? "object",
                                Kind: valueArg.ConstantValue.HasValue ? "Constant" : valueArg.Kind.ToString()
                            );
                            messageParameters.Add(parameter);
                        }
                    }
                    else if (invocation.Arguments.Length == 1)
                    {
                        // Handle single argument invocations, e.g., Add("key")
                        if (invocation.Arguments[0].Value is ILiteralOperation keyLiteral &&
                            keyLiteral.ConstantValue.HasValue &&
                            keyLiteral.ConstantValue.Value is string key)
                        {
                            var parameter = new MessageParameter(
                                Name: key,
                                Type: "object",
                                Kind: "Constant"
                            );
                            messageParameters.Add(parameter);
                        }
                        else if (loggingTypes.KeyValuePairOfStringNullableObject.Equals(invocation.Arguments[0].Value.Type, SymbolEqualityComparer.Default))
                        {
                            // Handle new KeyValuePair<string, object?>("key", value) syntax
                            if (invocation.Arguments[0].Value.UnwrapConversion() is IObjectCreationOperation kvpCreation)
                            {
                                ExtractKeyValuePairFromObjectCreation(kvpCreation, messageParameters, loggingTypes);
                            }
                        }
                    }
                }
                else if (elementInitializer is ISimpleAssignmentOperation assignment)
                {
                    // Handle ["key"] = value syntax for Dictionary
                    if (assignment.Target is IPropertyReferenceOperation propertyReference &&
                        propertyReference.Arguments.Length == 1 &&
                        propertyReference.Arguments[0].Value is ILiteralOperation keyLiteral &&
                        keyLiteral.ConstantValue.HasValue &&
                        keyLiteral.ConstantValue.Value is string key)
                    {
                        var valueArg = assignment.Value.UnwrapConversion();
                        var parameter = new MessageParameter(
                            Name: key,
                            Type: valueArg.Type?.ToPrettyDisplayString() ?? "object",
                            Kind: valueArg.ConstantValue.HasValue ? "Constant" : valueArg.Kind.ToString()
                        );
                        messageParameters.Add(parameter);
                    }
                }
            }
        }

        private static void ExtractKeyValuePairFromObjectCreation(IObjectCreationOperation keyValuePairCreation, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            if (keyValuePairCreation.Arguments.Length >= 2)
            {
                var keyArg = keyValuePairCreation.Arguments[0].Value;
                var valueArg = keyValuePairCreation.Arguments[1].Value.UnwrapConversion();

                if (keyArg is ILiteralOperation keyLiteral &&
                    keyLiteral.ConstantValue.HasValue &&
                    keyLiteral.ConstantValue.Value is string key)
                {
                    var parameter = new MessageParameter(
                        Name: key,
                        Type: valueArg.Type?.ToPrettyDisplayString() ?? "object",
                        Kind: valueArg.ConstantValue.HasValue ? "Constant" : valueArg.Kind.ToString()
                    );
                    messageParameters.Add(parameter);
                }
            }
            else if (keyValuePairCreation.Initializer is not null)
            {
                // Handle cases where the KeyValuePair is initialized with an initializer
                ExtractFromCollectionInitializer(keyValuePairCreation.Initializer, messageParameters, loggingTypes);
            }
        }

        private static void ExtractFromArrayInitializer(IArrayInitializerOperation initializer, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            foreach (var element in initializer.ElementValues)
            {
                var unwrappedElement = element.UnwrapConversion();
                if (unwrappedElement is IInvocationOperation invocation)
                {
                    // Handle new("key", value) syntax for KeyValuePair in array
                    if (invocation.Arguments.Length >= 2)
                    {
                        var keyArg = invocation.Arguments[0].Value;
                        var valueArg = invocation.Arguments[1].Value.UnwrapConversion();

                        if (keyArg is ILiteralOperation keyLiteral &&
                            keyLiteral.ConstantValue.HasValue &&
                            keyLiteral.ConstantValue.Value is string key)
                        {
                            var parameter = new MessageParameter(
                                Name: key,
                                Type: valueArg.Type?.ToPrettyDisplayString() ?? "object",
                                Kind: valueArg.ConstantValue.HasValue ? "Constant" : valueArg.Kind.ToString()
                            );
                            messageParameters.Add(parameter);
                        }
                    }
                }
                else if (unwrappedElement is IObjectCreationOperation keyValuePairCreation)
                {
                    // Handle new KeyValuePair<string, object?>("key", value) syntax in array
                    ExtractKeyValuePairFromObjectCreation(keyValuePairCreation, messageParameters, loggingTypes);
                }
            }
        }

        private static void ExtractFromLocalReference(ILocalReferenceOperation localRef, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            // For local variables, we'd need to track the variable assignment to extract the key-value pairs
            // This is a more complex scenario that would require dataflow analysis
            // For now, we'll add a placeholder indicating we found a local reference
            if (localRef.Local.Type != null && IsKeyValuePairEnumerable(localRef.Local.Type, loggingTypes))
            {
                // Add a generic parameter to indicate we found a key-value collection but couldn't extract details
                messageParameters.Add(new MessageParameter(
                    Name: $"<{localRef.Local.Name}>",
                    Type: localRef.Local.Type.ToPrettyDisplayString(),
                    Kind: localRef.Kind.ToString()
                ));
            }
        }

        private static void ExtractFromFieldReference(IFieldReferenceOperation fieldRef, List<MessageParameter> messageParameters, LoggingTypes loggingTypes)
        {
            // Similar to local reference, field analysis would require more complex tracking
            if (fieldRef.Field.Type != null && IsKeyValuePairEnumerable(fieldRef.Field.Type, loggingTypes))
            {
                messageParameters.Add(new MessageParameter(
                    Name: $"<{fieldRef.Field.Name}>",
                    Type: fieldRef.Field.Type.ToPrettyDisplayString(),
                    Kind: fieldRef.Kind.ToString()
                ));
            }
        }

        private static bool IsKeyValuePairEnumerable(ITypeSymbol type, LoggingTypes loggingTypes)
        {
            // Debug: Show what type we're checking
            var typeDisplay = type?.ToDisplayString() ?? "null";

            // Check if the type implements IEnumerable<KeyValuePair<string, object?>>
            if (type is INamedTypeSymbol namedType)
            {
                // Direct check for IEnumerable<KeyValuePair<string, object?>>
                if (namedType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                {
                    var typeArg = namedType.TypeArguments.FirstOrDefault();
                    var result = IsKeyValuePairType(typeArg, loggingTypes);
                    return result;
                }

                // Check implemented interfaces
                foreach (var interfaceType in namedType.AllInterfaces)
                {
                    if (interfaceType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                    {
                        var typeArg = interfaceType.TypeArguments.FirstOrDefault();
                        if (IsKeyValuePairType(typeArg, loggingTypes))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsKeyValuePairType(ITypeSymbol? type, LoggingTypes loggingTypes)
        {
            if (type is not INamedTypeSymbol namedType)
                return false;

            // Use the pre-resolved KeyValuePair<string, object?> symbol for cleaner comparison
            return SymbolEqualityComparer.Default.Equals(namedType, loggingTypes.KeyValuePairOfStringNullableObject);
        }
    }
}
