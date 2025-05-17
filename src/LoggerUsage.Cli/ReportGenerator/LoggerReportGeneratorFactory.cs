using LoggerUsage.Cli.ReportGenerator;

namespace LoggerUsage.Cli.ReportGenerator;

public class LoggerReportGeneratorFactory : ILoggerReportGeneratorFactory
{
    public ILoggerReportGenerator GetReportGenerator(string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath) && outputPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            return new HtmlLoggerReportGenerator();
        return new JsonLoggerReportGenerator();
    }
}
