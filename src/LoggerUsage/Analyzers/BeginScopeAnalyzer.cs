using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Analyzers
{
    internal class BeginScopeAnalyzer : ILoggerUsageAnalyzer
    {
        public BeginScopeAnalyzer(ILoggerFactory loggerFactory)
        {
            // Logger factory is accepted for consistency with other analyzers but not currently used
            _ = loggerFactory;
        }

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
            var argumentIndex = ScopeParameterExtractor.GetArgumentIndex(operation);

            if (operation.Arguments.Length <= argumentIndex)
                return;

            var stateArgument = operation.Arguments[argumentIndex];

            // Extract message template from the state argument
            ExtractMessageTemplate(stateArgument, usage);

            // Extract message parameters based on the argument type and method type
            ExtractParameters(operation, stateArgument, usage, loggingTypes);
        }

        #region State Extraction Strategy

        /// <summary>
        /// Extracts the message template from the state argument if it's a literal value.
        /// </summary>
        private static void ExtractMessageTemplate(IArgumentOperation stateArgument, LoggerUsageInfo usage)
        {
            if (stateArgument.Value is ILiteralOperation literal && literal.ConstantValue.HasValue)
            {
                usage.MessageTemplate = literal.ConstantValue.Value?.ToString();
            }
        }

        /// <summary>
        /// Determines the parameter extraction strategy based on method type and argument content.
        /// </summary>
        private static void ExtractParameters(IInvocationOperation operation, IArgumentOperation stateArgument, LoggerUsageInfo usage, LoggingTypes loggingTypes)
        {
            if (operation.TargetMethod.IsExtensionMethod && usage.MessageTemplate != null)
            {
                // Handle extension methods with message templates
                ScopeParameterExtractor.ExtractMessageParameters(operation, usage);
            }
            else if (!operation.TargetMethod.IsExtensionMethod)
            {
                // Handle core ILogger.BeginScope method
                ExtractCoreMethodParameters(stateArgument, usage, loggingTypes);
            }
        }

        /// <summary>
        /// Extracts parameters from core ILogger.BeginScope method calls.
        /// </summary>
        private static void ExtractCoreMethodParameters(IArgumentOperation stateArgument, LoggerUsageInfo usage, LoggingTypes loggingTypes)
        {
            // Try to extract key-value pairs first
            if (KeyValuePairHandler.TryExtractKeyValuePairParameters(stateArgument, usage, loggingTypes))
            {
                return; // Successfully extracted key-value pairs
            }

            // Fallback to anonymous object extraction
            if (stateArgument.Value is IAnonymousObjectCreationOperation objectCreation)
            {
                ScopeParameterExtractor.ExtractAnonymousObjectProperties(objectCreation, usage);
            }
        }

        #endregion
    }
}
