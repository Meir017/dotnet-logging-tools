using Microsoft.CodeAnalysis;
using LoggerUsage.Models;

namespace LoggerUsage.Analyzers
{
    /// <summary>
    /// Defines a contract for analyzing logger usage patterns in C# source code.
    /// </summary>
    public interface ILoggerUsageAnalyzer
    {
        /// <summary>
        /// Analyzes the provided syntax tree and semantic model to extract logger usage information.
        /// </summary>
        /// <param name="loggingTypes">The logging types configuration used for analysis.</param>
        /// <param name="root">The root syntax node of the syntax tree to analyze.</param>
        /// <param name="semanticModel">The semantic model providing type information for the syntax tree.</param>
        /// <returns>A collection of logger usage information found in the analyzed code.</returns>
        IEnumerable<LoggerUsageInfo> Analyze(LoggingTypes loggingTypes, SyntaxNode root, SemanticModel semanticModel);
    }
}
