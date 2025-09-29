namespace LoggerUsage.ReportGenerator;

internal class LoggerReportGeneratorFactory : ILoggerReportGeneratorFactory
{
    public ILoggerReportGenerator GetReportGenerator(string type) => type switch
    {
        ".json" => new JsonLoggerReportGenerator(),
        ".html" => new HtmlLoggerReportGenerator(),
        ".md" or ".markdown" => new MarkdownLoggerReportGenerator(),
        _ => throw new NotSupportedException($"The report type '{type}' is not supported.")
    };
}
