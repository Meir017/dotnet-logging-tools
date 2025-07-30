using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;
using LoggerUsage.Services;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Analyzers
{
    internal class BeginScopeAnalyzer : ILoggerUsageAnalyzer
    {
        private readonly IScopeAnalysisService _scopeAnalysisService;
        private readonly ILogger<BeginScopeAnalyzer> _logger;

        public BeginScopeAnalyzer(
            IScopeAnalysisService scopeAnalysisService,
            ILoggerFactory loggerFactory)
        {
            _scopeAnalysisService = scopeAnalysisService;
            _logger = loggerFactory.CreateLogger<BeginScopeAnalyzer>();
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

        private LoggerUsageInfo ExtractBeginScopeUsage(IInvocationOperation operation, LoggingTypes loggingTypes, InvocationExpressionSyntax invocation)
        {
            var usage = new LoggerUsageInfo
            {
                MethodName = operation.TargetMethod.Name,
                MethodType = LoggerUsageMethodType.BeginScope,
                Location = LocationHelper.CreateFromInvocation(invocation),
            };

            var analysisResult = _scopeAnalysisService.AnalyzeScopeState(operation, loggingTypes);

            if (analysisResult.IsSuccess)
            {
                usage.MessageTemplate = analysisResult.MessageTemplate;
                usage.MessageParameters = analysisResult.Parameters;
                
                _logger.LogDebug("Successfully analyzed BeginScope usage with {Count} parameters", 
                    analysisResult.Parameters.Count);
            }
            else
            {
                _logger.LogWarning("Failed to analyze BeginScope usage: {Error}", analysisResult.ErrorMessage);
                usage.MessageParameters = new List<MessageParameter>();
            }

            return usage;
        }
    }
}
