using Microsoft.CodeAnalysis;
using LoggerUsage.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace LoggerUsage.Analyzers
{
    internal partial class LoggerMessageAttributeAnalyzer : ILoggerUsageAnalyzer
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

        [GeneratedRegex(@"\{([^}:,]+)(?:[^}]*)\}", RegexOptions.Compiled)]
        private static partial Regex MessageTemplateRegex { get; }

        private static bool TryExtractMessageParameters(AttributeData attribute, LoggingTypes loggingTypes, IMethodSymbol methodSymbol, string messageTemplate, out List<MessageParameter> messageParameters)
        {
            messageParameters = new List<MessageParameter>();
            if (string.IsNullOrEmpty(messageTemplate))
                return false;

            // 1. Extract placeholders from the message template
            var matches = MessageTemplateRegex.Matches(messageTemplate);
            if (matches.Count == 0)
                return false;

            // 2. Get method parameters, excluding ILogger, LogLevel, and Exception (by type)
            var parameters = methodSymbol.Parameters
                .Where(p =>
                    !loggingTypes.ILogger.Equals(p.Type, SymbolEqualityComparer.Default) &&
                    !loggingTypes.LogLevel.Equals(p.Type, SymbolEqualityComparer.Default) &&
                    !loggingTypes.Exception.Equals(p.Type, SymbolEqualityComparer.Default))
                .ToList();

            // 3. For each placeholder, find a matching parameter (case-insensitive)
            foreach (Match match in matches)
            {
                var placeholder = match.Groups[1].Value;
                var param = parameters.FirstOrDefault(p => string.Equals(p.Name, placeholder, StringComparison.OrdinalIgnoreCase));
                if (param != null)
                {
                    messageParameters.Add(new MessageParameter(param.Name, param.Type.ToDisplayString(), null));
                }
            }

            return messageParameters.Count > 0;
        }
    }
}
