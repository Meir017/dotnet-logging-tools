using LoggerUsage.Models;

namespace LoggerUsage.ReportGenerator;

public interface ILoggerReportGenerator
{
    string GenerateReport(LoggerUsageExtractionResult loggerUsage);
}
