namespace LoggerUsage.Models;

public class LoggerUsageExtractionResult
{
    public List<LoggerUsageInfo> Results { get; set; } = [];
    public LoggerUsageExtractionSummary Summary { get; set; } = new();
}
