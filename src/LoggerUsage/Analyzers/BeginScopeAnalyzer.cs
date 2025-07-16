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
            ExtractScopeState(operation, usage);

            return usage;
        }

        private static void ExtractScopeState(IInvocationOperation operation, LoggerUsageInfo usage)
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
            else
            {
                // For complex expressions, store the syntax representation
                var syntaxNode = stateArgument.Syntax;
                usage.MessageTemplate = syntaxNode.ToString();
            }

            // Extract message parameters if this is an extension method with a message template and args
            if (operation.TargetMethod.IsExtensionMethod && usage.MessageTemplate != null)
            {
                ExtractMessageParameters(operation, usage);
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
    }
}
