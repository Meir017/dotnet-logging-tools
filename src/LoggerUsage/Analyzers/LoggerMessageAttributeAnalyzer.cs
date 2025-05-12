using Microsoft.CodeAnalysis;
using LoggerUsage.Models;

namespace LoggerUsage.Analyzers
{
    internal class LoggerMessageAttributeAnalyzer : ILoggerUsageAnalyzer
    {
        public IEnumerable<LoggerUsageInfo> Analyze(LoggingTypes loggingTypes, SyntaxNode root, SemanticModel semanticModel)
        {
            yield break;
        }
    }
}
