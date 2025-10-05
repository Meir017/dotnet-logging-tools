using Microsoft.CodeAnalysis;
using LoggerUsage.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.FindSymbols;

namespace LoggerUsage.Analyzers
{
    internal partial class LoggerMessageAttributeAnalyzer(ILogger<LoggerMessageAttributeAnalyzer> logger) : ILoggerUsageAnalyzer
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
    }
}
