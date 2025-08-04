namespace LoggerUsage.Models;

/// <summary>
/// Contains the results of logger usage extraction, including individual usage details and summary statistics.
/// </summary>
public class LoggerUsageExtractionResult
{
    /// <summary>
    /// Gets or sets the list of individual logger usage information.
    /// </summary>
    public List<LoggerUsageInfo> Results { get; set; } = [];
    
    /// <summary>
    /// Gets or sets the summary statistics of the logger usage extraction.
    /// </summary>
    public LoggerUsageExtractionSummary Summary { get; set; } = new();
}
