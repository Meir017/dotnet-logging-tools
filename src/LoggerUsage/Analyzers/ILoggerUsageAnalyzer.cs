using Microsoft.CodeAnalysis;
using LoggerUsage.Models;

namespace LoggerUsage.Analyzers
{
    public interface ILoggerUsageAnalyzer
    {
        IEnumerable<LoggerUsageInfo> Analyze(LoggingTypes loggingTypes, SyntaxNode root, SemanticModel semanticModel);
    }
}
