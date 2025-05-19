using System.Text.Json;
using LoggerUsage.Models;

namespace LoggerUsage.Cli.ReportGenerator;

public class JsonLoggerReportGenerator : ILoggerReportGenerator
{
    public string GenerateReport(LoggerUsageExtractionResult loggerUsage) => JsonSerializer.Serialize(loggerUsage);
}
