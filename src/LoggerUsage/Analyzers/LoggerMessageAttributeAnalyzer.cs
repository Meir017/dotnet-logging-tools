using Microsoft.CodeAnalysis;
using LoggerUsage.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.FindSymbols;

namespace LoggerUsage.Analyzers
{
    internal class LoggerMessageAttributeAnalyzer(ILogger<LoggerMessageAttributeAnalyzer> logger) : ILoggerUsageAnalyzer
    {
        /// <summary>
        /// Represents a LoggerMessage method declaration with its signature information
        /// </summary>
        private record LoggerMessageDeclaration(
            IMethodSymbol MethodSymbol,
            string ContainingTypeName,
            MethodDeclarationSyntax DeclarationSyntax,
            LoggerUsageInfo BaseUsageInfo);

        public async Task<IEnumerable<LoggerUsageInfo>> AnalyzeAsync(LoggingAnalysisContext context)
        {
            logger.LogTrace("Starting LoggerMessageAttribute analysis");

            // Phase 1: Discover LoggerMessage method declarations
            var declarations = DiscoverLoggerMessageDeclarations(context.LoggingTypes, context.Root, context.SemanticModel);

            if (!declarations.Any())
            {
                logger.LogTrace("No LoggerMessage declarations found");
                return [];
            }

            logger.LogTrace("Found {DeclarationCount} LoggerMessage declarations", declarations.Count);

            var results = new List<LoggerUsageInfo>();

            // Phase 2: Find invocations for each declaration
            foreach (var declaration in declarations)
            {
                var invocations = await FindLoggerMessageInvocations(declaration, context);

                // Extract LogProperties information
                var logPropertiesParameters = ExtractLogPropertiesParameters(declaration.MethodSymbol, context.LoggingTypes);

                // Create LoggerMessageUsageInfo with invocation data
                var loggerMessageUsage = new LoggerMessageUsageInfo
                {
                    MethodName = declaration.BaseUsageInfo.MethodName,
                    MethodType = declaration.BaseUsageInfo.MethodType,
                    Location = declaration.BaseUsageInfo.Location,
                    MessageTemplate = declaration.BaseUsageInfo.MessageTemplate,
                    LogLevel = declaration.BaseUsageInfo.LogLevel,
                    EventId = declaration.BaseUsageInfo.EventId,
                    MessageParameters = declaration.BaseUsageInfo.MessageParameters,
                    DeclaringTypeName = declaration.ContainingTypeName,
                    Invocations = invocations,
                    LogPropertiesParameters = logPropertiesParameters
                };

                logger.LogTrace("Analyzed LoggerMessage method {MethodName} with {InvocationCount} invocations",
                    loggerMessageUsage.MethodName, loggerMessageUsage.InvocationCount);

                results.Add(loggerMessageUsage);
            }

            // Ensure this is truly async
            await Task.Yield();
            return results;
        }

        /// <summary>
        /// Phase 1: Discover all LoggerMessage method declarations in the syntax tree
        /// </summary>
        private List<LoggerMessageDeclaration> DiscoverLoggerMessageDeclarations(
            LoggingTypes loggingTypes,
            SyntaxNode root,
            SemanticModel semanticModel)
        {
            var declarations = new List<LoggerMessageDeclaration>();
            var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

            foreach (var methodDeclaration in methodDeclarations)
            {
                if (semanticModel.GetDeclaredSymbol(methodDeclaration) is not IMethodSymbol methodSymbol)
                {
                    continue;
                }

                if (!methodSymbol.IsPartialDefinition)
                {
                    continue;
                }

                foreach (var attributeData in methodSymbol.GetAttributes())
                {
                    if (!loggingTypes.LoggerMessageAttribute.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                    {
                        continue;
                    }

                    // Create base LoggerUsageInfo (existing functionality)
                    var usage = new LoggerUsageInfo
                    {
                        MethodName = methodSymbol.Name,
                        MethodType = LoggerUsageMethodType.LoggerMessageAttribute,
                        Location = LocationHelper.CreateFromMethodDeclaration(methodDeclaration, root),
                    };

                    logger.LogTrace("Found LoggerMessageAttribute on method {MethodName}", usage.MethodName);

                    if (TryExtractEventId(attributeData, methodSymbol, loggingTypes, out var eventId))
                    {
                        usage.EventId = eventId;
                    }
                    if (TryExtractLogLevel(attributeData, loggingTypes, out var logLevel))
                    {
                        usage.LogLevel = logLevel;
                    }
                    if (TryExtractMessageTemplate(attributeData, loggingTypes, out var messageTemplate))
                    {
                        usage.MessageTemplate = messageTemplate;
                        if (TryExtractMessageParameters(attributeData, loggingTypes, methodSymbol, messageTemplate, out var messageParameters))
                        {
                            usage.MessageParameters = messageParameters;
                        }
                    }

                    var containingTypeName = methodSymbol.ContainingType.ToDisplayString();
                    declarations.Add(new LoggerMessageDeclaration(
                        methodSymbol,
                        containingTypeName,
                        methodDeclaration,
                        usage));

                    logger.LogTrace("Extracted LoggerMessageAttribute declaration {MethodName} in {ContainingType}",
                        usage.MethodName, containingTypeName);
                }
            }

            return declarations;
        }

        /// <summary>
        /// Phase 2: Find all invocations of a specific LoggerMessage method
        /// </summary>
        private async Task<List<LoggerMessageInvocation>> FindLoggerMessageInvocations(LoggerMessageDeclaration declaration, LoggingAnalysisContext context)
        {
            var invocations = new List<LoggerMessageInvocation>();
            if (context.Solution is not null)
            {
                var callers = await SymbolFinder.FindCallersAsync(declaration.MethodSymbol, context.Solution);
                foreach (var caller in callers)
                {
                    foreach (var location in caller.Locations)
                    {
                        if (location.IsInSource && location.SourceTree != null)
                        {
                            var syntaxRoot = await location.SourceTree.GetRootAsync();
                            if (syntaxRoot.FindNode(location.SourceSpan) is IdentifierNameSyntax identifierName // the method name - e.g. LogUserActivity
                                && identifierName.Parent is MemberAccessExpressionSyntax memberAccess // the member access - e.g. UserLogger.LogUserActivity
                                && memberAccess.Parent is InvocationExpressionSyntax invocationExpression // the invocation expression - e.g. UserLogger.LogUserActivity(...)
                            )
                            {
                                var document = context.Solution.GetDocumentId(location.SourceTree);
                                var project = context.Solution.GetProject(document!.ProjectId);
                                var compilation = await project!.GetCompilationAsync();
                                var semanticModel = compilation!.GetSemanticModel(location.SourceTree);

                                if (semanticModel.GetOperation(invocationExpression) is not IInvocationOperation operation)
                                {
                                    continue;
                                }

                                invocations.Add(CreateLoggerMessageInvocation(operation, invocationExpression, syntaxRoot, semanticModel));
                            }
                        }
                    }
                }

                return invocations;
            }

            var invocationNodes = context.Root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocationNode in invocationNodes)
            {
                if (context.SemanticModel.GetOperation(invocationNode) is not IInvocationOperation operation)
                {
                    continue;
                }

                if (IsLoggerMessageMethodInvocation(operation, declaration))
                {
                    var invocation = CreateLoggerMessageInvocation(operation, invocationNode, context.Root, context.SemanticModel);
                    invocations.Add(invocation);

                    logger.LogTrace("Found invocation of {MethodName} in {ContainingType} at line {LineNumber}",
                        declaration.MethodSymbol.Name,
                        invocation.ContainingType,
                        invocation.InvocationLocation.StartLineNumber);
                }
            }

            return invocations;
        }

        /// <summary>
        /// Determines if an invocation operation is calling the specified LoggerMessage method
        /// </summary>
        private static bool IsLoggerMessageMethodInvocation(IInvocationOperation operation, LoggerMessageDeclaration declaration)
        {
            var targetMethod = operation.TargetMethod;

            // Check if the target method matches our LoggerMessage method
            // This includes both the partial definition and any generated implementation
            if (SymbolEqualityComparer.Default.Equals(targetMethod.OriginalDefinition, declaration.MethodSymbol.OriginalDefinition))
            {
                return true;
            }

            // Check if this is a generated method that matches our signature
            if (IsGeneratedLoggerMessageMethod(targetMethod, declaration))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a method is a generated LoggerMessage method matching the declaration
        /// </summary>
        private static bool IsGeneratedLoggerMessageMethod(IMethodSymbol targetMethod, LoggerMessageDeclaration declaration)
        {
            // Must have the same name and containing type
            if (targetMethod.Name != declaration.MethodSymbol.Name)
            {
                return false;
            }

            if (!SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, declaration.MethodSymbol.ContainingType))
            {
                return false;
            }

            // Check parameter signatures match
            if (targetMethod.Parameters.Length != declaration.MethodSymbol.Parameters.Length)
            {
                return false;
            }

            for (int i = 0; i < targetMethod.Parameters.Length; i++)
            {
                if (!SymbolEqualityComparer.Default.Equals(
                    targetMethod.Parameters[i].Type,
                    declaration.MethodSymbol.Parameters[i].Type))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Creates a LoggerMessageInvocation from an invocation operation
        /// </summary>
        private static LoggerMessageInvocation CreateLoggerMessageInvocation(
            IInvocationOperation operation,
            InvocationExpressionSyntax invocationSyntax,
            SyntaxNode root,
            SemanticModel semanticModel)
        {
            var containingType = GetContainingTypeName(invocationSyntax, semanticModel);
            var location = LocationHelper.CreateFromInvocation(invocationSyntax);
            var arguments = ExtractInvocationArguments(operation);

            return new LoggerMessageInvocation
            {
                ContainingType = containingType,
                InvocationLocation = location,
                Arguments = arguments
            };
        }

        /// <summary>
        /// Gets the fully qualified name of the type containing the invocation
        /// </summary>
        private static string GetContainingTypeName(InvocationExpressionSyntax invocationSyntax, SemanticModel semanticModel)
        {
            var containingSymbol = semanticModel.GetEnclosingSymbol(invocationSyntax.SpanStart);

            while (containingSymbol != null)
            {
                if (containingSymbol is INamedTypeSymbol typeSymbol)
                {
                    return typeSymbol.ToDisplayString();
                }
                containingSymbol = containingSymbol.ContainingSymbol;
            }

            return "Unknown";
        }

        /// <summary>
        /// Extracts argument information from an invocation operation
        /// </summary>
        private static List<MessageParameter> ExtractInvocationArguments(IInvocationOperation operation)
        {
            var arguments = new List<MessageParameter>();

            foreach (var argument in operation.Arguments)
            {
                var parameterName = argument.Parameter?.Name ?? "Unknown";
                var parameterType = argument.Parameter?.Type?.ToDisplayString() ?? "Unknown";

                // For now, we'll create a simple representation
                // This can be enhanced later to analyze the actual argument expressions
                arguments.Add(new MessageParameter(parameterName, parameterType, "InvocationArgument"));
            }

            return arguments;
        }

        private static bool TryExtractEventId(AttributeData attribute, IMethodSymbol methodSymbol, LoggingTypes loggingTypes, out EventIdDetails eventIdDetails)
        {
            string? eventName = null;
            int? eventId = null;
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == nameof(LoggerMessageAttribute.EventId))
                {
                    if (namedArg.Value.Value is int eventIdValue)
                    {
                        eventId = eventIdValue;
                    }
                }
                if (namedArg.Key == nameof(LoggerMessageAttribute.EventName))
                {
                    if (namedArg.Value.Value is string eventNameValue)
                    {
                        eventName = eventNameValue;
                    }
                }
            }

            if (eventId is null && attribute.ConstructorArguments is { Length: 3 })
            {
                var eventIdArg = attribute.ConstructorArguments[0];
                if (eventIdArg.Value is int eventIdValue)
                {
                    eventId = eventIdValue;
                }
            }

            eventIdDetails = (eventName, eventId) switch
            {
                (null, int id) => new EventIdDetails(ConstantOrReference.Constant(id), ConstantOrReference.Missing),
                (string name, null) => new EventIdDetails(ConstantOrReference.Missing, ConstantOrReference.Constant(name)),
                (string name, int id) => new EventIdDetails(ConstantOrReference.Constant(id), ConstantOrReference.Constant(name)),
                (null, null) => null!
            };

            return eventIdDetails is not null;
        }

        private static bool TryExtractLogLevel(AttributeData attribute, LoggingTypes loggingTypes, out LogLevel? logLevel)
        {
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == nameof(LoggerMessageAttribute.Level))
                {
                    logLevel = (LogLevel)namedArg.Value.Value!;
                    return true;
                }
            }

            if (attribute.ConstructorArguments is { Length: 1 } or { Length: 2 } &&
                loggingTypes.LogLevel.Equals(attribute.ConstructorArguments[0].Type, SymbolEqualityComparer.Default))
            {
                logLevel = (LogLevel)attribute.ConstructorArguments[0].Value!;
                return true;
            }

            if (attribute.ConstructorArguments is { Length: 3 } &&
                loggingTypes.LogLevel.Equals(attribute.ConstructorArguments[1].Type, SymbolEqualityComparer.Default))
            {
                logLevel = (LogLevel)attribute.ConstructorArguments[1].Value!;
                return true;
            }

            logLevel = null;
            return false;
        }

        private static bool TryExtractMessageTemplate(AttributeData attribute, LoggingTypes loggingTypes, out string messageTemplate)
        {
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == nameof(LoggerMessageAttribute.Message))
                {
                    messageTemplate = (string)namedArg.Value.Value!;
                    return true;
                }
            }

            if (attribute.ConstructorArguments is { Length: 1 } &&
                SpecialType.System_String.Equals(attribute.ConstructorArguments[0].Type?.SpecialType))
            {
                messageTemplate = (string)attribute.ConstructorArguments[0].Value!;
                return true;
            }

            if (attribute.ConstructorArguments is { Length: 2 } &&
                SpecialType.System_String.Equals(attribute.ConstructorArguments[1].Type?.SpecialType))
            {
                messageTemplate = (string)attribute.ConstructorArguments[1].Value!;
                return true;
            }

            if (attribute.ConstructorArguments is { Length: 3 } &&
                SpecialType.System_String.Equals(attribute.ConstructorArguments[2].Type?.SpecialType))
            {
                messageTemplate = (string)attribute.ConstructorArguments[2].Value!;
                return true;
            }

            messageTemplate = string.Empty;
            return false;
        }

        private static bool TryExtractMessageParameters(AttributeData attribute, LoggingTypes loggingTypes, IMethodSymbol methodSymbol, string messageTemplate, out List<MessageParameter> messageParameters)
        {
            // Use MethodSignatureParameterExtractor from the strategy pattern
            return ParameterExtraction.MethodSignatureParameterExtractor.TryExtractFromMethodSignature(
                methodSymbol, messageTemplate, loggingTypes, out messageParameters);
        }

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
                    
                    // Extract properties from the parameter type
                    var properties = ExtractPropertiesFromType(parameter.Type);

                    var logPropertiesParameter = new LogPropertiesParameterInfo(
                        parameter.Name,
                        parameter.Type.Name,
                        configuration,
                        properties);

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
        private static List<LogPropertyInfo> ExtractPropertiesFromType(ITypeSymbol typeSymbol)
        {
            var properties = new List<LogPropertyInfo>();

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
                        var logProperty = new LogPropertyInfo(
                            property.Name,
                            property.Name,
                            GetSimpleTypeName(property.Type),
                            property.Type.CanBeReferencedByName && property.Type.NullableAnnotation == NullableAnnotation.Annotated);

                        properties.Add(logProperty);
                    }
                }
            }

            return properties;
        }

        private static string GetSimpleTypeName(ITypeSymbol typeSymbol)
        {
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
