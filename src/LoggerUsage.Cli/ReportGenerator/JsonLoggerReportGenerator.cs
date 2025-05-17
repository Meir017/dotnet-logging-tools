using System.Text.Json;
using LoggerUsage.Models;

namespace LoggerUsage.Cli.ReportGenerator;

public class JsonLoggerReportGenerator : ILoggerReportGenerator
{
    public string GenerateReport(List<LoggerUsageInfo> results) => JsonSerializer.Serialize(results);
}
