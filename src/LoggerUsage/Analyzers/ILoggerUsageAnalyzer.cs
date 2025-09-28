using LoggerUsage.Models;

namespace LoggerUsage.Analyzers
{
    /// <summary>
    /// Defines a contract for analyzing logger usage patterns in C# source code.
    /// </summary>
    public interface ILoggerUsageAnalyzer
    {
        /// <summary>
        /// Analyzes the provided context to extract logger usage information.
        /// </summary>
        /// <param name="context">The analysis context containing all necessary data for the analysis.</param>
        /// <returns>A collection of logger usage information found in the analyzed code.</returns>
        IEnumerable<LoggerUsageInfo> Analyze(AnalysisContext context);
    }
}
