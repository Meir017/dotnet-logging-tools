using LoggerUsage.Models;

namespace LoggerUsage.Analyzers
{
    /// <summary>
    /// Defines a contract for analyzing logger usage patterns in C# source code.
    /// </summary>
    public interface ILoggerUsageAnalyzer
    {
        /// <summary>
        /// Asynchronously analyzes the provided context to extract logger usage information.
        /// </summary>
        /// <param name="context">The analysis context containing all necessary data for the analysis.</param>
        /// <returns>A task that represents the asynchronous operation, containing the logger usage information found in the analyzed code.</returns>
        Task<IEnumerable<LoggerUsageInfo>> AnalyzeAsync(LoggingAnalysisContext context);
    }
}
