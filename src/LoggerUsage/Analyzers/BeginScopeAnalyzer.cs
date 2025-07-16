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

            // For now, just capture the argument as a string representation
            // We'll enhance this to detect different scope types later
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
        }
    }
}
