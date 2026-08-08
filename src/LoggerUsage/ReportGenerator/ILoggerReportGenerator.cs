using LoggerUsage.Models;

namespace LoggerUsage.ReportGenerator;

/// <summary>
/// Defines a contract for generating reports from logger usage extraction results.
/// </summary>
public interface ILoggerReportGenerator
{
    /// <summary>
    /// Generates a formatted report from the provided logger usage extraction results.
    /// </summary>
    /// <param name="loggerUsage">The logger usage extraction results to generate a report from.</param>
    /// <returns>A formatted string representation of the logger usage report.</returns>
    string GenerateReport(LoggerUsageExtractionResult loggerUsage);

    /// <summary>
    /// Generates a formatted report using additional report-generation context.
    /// </summary>
    /// <param name="loggerUsage">The logger usage extraction results to generate a report from.</param>
    /// <param name="context">Additional context used to generate the report.</param>
    /// <returns>A formatted string representation of the logger usage report.</returns>
    string GenerateReport(LoggerUsageExtractionResult loggerUsage, ReportGenerationContext context) =>
        GenerateReport(loggerUsage);
}
