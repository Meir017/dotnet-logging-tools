namespace LoggerUsage.ReportGenerator;

public interface ILoggerReportGeneratorFactory
{
    ILoggerReportGenerator GetReportGenerator(string type);
}
