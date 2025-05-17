namespace LoggerUsage.Cli.ReportGenerator;

public interface ILoggerReportGeneratorFactory
{
    ILoggerReportGenerator GetReportGenerator(string? outputPath);
}
