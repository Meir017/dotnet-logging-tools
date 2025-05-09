using Microsoft.Extensions.Logging;

namespace LoggerUsage.Models;

public class LoggerUsageInfo
{
    public required string MethodName { get; set; }
    public string? MessageTemplate { get; set; }
    public LogLevel? LogLevel { get; set; }
    public EventIdBase? EventId { get; set; }
    public List<MessageParameter> MessageParameters { get; set; } = new();
    public required MethodCallLocation Location { get; set; }
}
