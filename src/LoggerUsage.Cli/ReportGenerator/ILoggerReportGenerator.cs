using LoggerUsage.Models;

namespace LoggerUsage.Cli.ReportGenerator;

public interface ILoggerReportGenerator
{
    string GenerateReport(List<LoggerUsageInfo> results);
}
