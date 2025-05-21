using System.Text.Json;
using LoggerUsage.Models;

namespace LoggerUsage.ReportGenerator;

internal class JsonLoggerReportGenerator : ILoggerReportGenerator
{
    public string GenerateReport(LoggerUsageExtractionResult loggerUsage) => JsonSerializer.Serialize(loggerUsage);
}
