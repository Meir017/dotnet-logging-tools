using Microsoft.CodeAnalysis;
using LoggerUsage.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Analyzers
{
    internal class LoggerMessageAttributeAnalyzer : ILoggerUsageAnalyzer
    {
        public IEnumerable<LoggerUsageInfo> Analyze(LoggingTypes loggingTypes, SyntaxNode root, SemanticModel semanticModel)
        {
            var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

            foreach (var methodDeclaration in methodDeclarations)
            {
                if (semanticModel.GetDeclaredSymbol(methodDeclaration) is not IMethodSymbol methodSymbol)
                    continue;

                if (!methodSymbol.IsPartialDefinition)
                    continue;

                foreach (var attributeData in methodSymbol.GetAttributes())
                {
                    if (!loggingTypes.LoggerMessageAttribute.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                        continue;

                    var usage = new LoggerUsageInfo
                    {
                        MethodName = methodSymbol.Name,
                        Location = new MethodCallLocation
                        {
                            LineNumber = methodDeclaration.GetLocation().GetLineSpan().StartLinePosition.Line,
                            ColumnNumber = methodDeclaration.GetLocation().GetLineSpan().StartLinePosition.Character,
                        },
                    };

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

                    yield return usage;
                }
            }
        }

        private static bool TryExtractEventId(AttributeData attribute, IMethodSymbol methodSymbol, LoggingTypes loggingTypes, out EventIdDetails eventId)
        {
            eventId = null!;
            return false;
        }

        private static bool TryExtractLogLevel(AttributeData attribute, LoggingTypes loggingTypes, out LogLevel logLevel)
        {
            logLevel = LogLevel.None;
            return false;
        }

        private static bool TryExtractMessageTemplate(AttributeData attribute, LoggingTypes loggingTypes, out string messageTemplate)
        {
            messageTemplate = string.Empty;
            return false;
        }

        private static bool TryExtractMessageParameters(AttributeData attribute, LoggingTypes loggingTypes, IMethodSymbol methodSymbol, string messageTemplate, out List<MessageParameter> messageParameters)
        {
            messageParameters = new List<MessageParameter>();
            return false;
        }
    }
}
