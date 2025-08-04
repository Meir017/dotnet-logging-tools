namespace LoggerUsage.ReportGenerator;

/// <summary>
/// Defines a factory for creating logger report generators based on the specified type.
/// </summary>
public interface ILoggerReportGeneratorFactory
{
    /// <summary>
    /// Creates and returns a report generator instance for the specified report type.
    /// </summary>
    /// <param name="type">The type of report generator to create (e.g., "json", "html", "markdown").</param>
    /// <returns>An instance of <see cref="ILoggerReportGenerator"/> for the specified type.</returns>
    ILoggerReportGenerator GetReportGenerator(string type);
}
