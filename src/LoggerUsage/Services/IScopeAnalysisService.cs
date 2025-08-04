using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;

namespace LoggerUsage.Services
{
    /// <summary>
    /// Service for analyzing scope operations in logger usage.
    /// </summary>
    internal interface IScopeAnalysisService
    {
        /// <summary>
        /// Analyzes a BeginScope invocation operation and extracts scope state information.
        /// </summary>
        /// <param name="operation">The invocation operation to analyze.</param>
        /// <param name="loggingTypes">The logging types context.</param>
        /// <returns>The result of the scope analysis.</returns>
        ScopeAnalysisResult AnalyzeScopeState(IInvocationOperation operation, LoggingTypes loggingTypes);
    }
}
