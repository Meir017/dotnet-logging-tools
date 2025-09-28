using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;
using LoggerUsage.Services;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Analyzers
{
    internal class BeginScopeAnalyzer(
        IScopeAnalysisService scopeAnalysisService,
        ILogger<BeginScopeAnalyzer> logger) : ILoggerUsageAnalyzer
    {
        public async Task<IEnumerable<LoggerUsageInfo>> AnalyzeAsync(LoggingAnalysisContext context)
        {
            var results = new List<LoggerUsageInfo>();
            var invocations = context.Root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            
            foreach (var invocation in invocations)
            {
                if (context.SemanticModel.GetOperation(invocation) is not IInvocationOperation operation)
                {
                    continue;
                }

                if (!context.LoggingTypes.LoggerExtensionModeler.IsBeginScopeMethod(operation.TargetMethod))
                {
                    continue;
                }

                results.Add(ExtractBeginScopeUsage(operation, context.LoggingTypes, invocation));
            }
            
            // Ensure this is truly async
            await Task.Yield();
            return results;
        }

        private LoggerUsageInfo ExtractBeginScopeUsage(IInvocationOperation operation, LoggingTypes loggingTypes, InvocationExpressionSyntax invocation)
        {
            var usage = new LoggerUsageInfo
            {
                MethodName = operation.TargetMethod.Name,
                MethodType = LoggerUsageMethodType.BeginScope,
                Location = LocationHelper.CreateFromInvocation(invocation),
            };

            var analysisResult = scopeAnalysisService.AnalyzeScopeState(operation, loggingTypes);

            if (analysisResult.IsSuccess)
            {
                usage.MessageTemplate = analysisResult.MessageTemplate;
                usage.MessageParameters = analysisResult.Parameters;

                logger.LogDebug("Successfully analyzed BeginScope usage with {Count} parameters",
                    analysisResult.Parameters.Count);
            }
            else
            {
                logger.LogWarning("Failed to analyze BeginScope usage: {Error}", analysisResult.ErrorMessage);
                usage.MessageParameters = [];
            }

            return usage;
        }
    }
}
