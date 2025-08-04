using Microsoft.Extensions.Logging;

namespace LoggerUsage.Models;

/// <summary>
/// Contains detailed information about a specific logger usage instance.
/// </summary>
public class LoggerUsageInfo
{
    /// <summary>
    /// Gets or sets the name of the logger method being called.
    /// </summary>
    public required string MethodName { get; set; }
    
    /// <summary>
    /// Gets or sets the type of logger method being used.
    /// </summary>
    public required LoggerUsageMethodType MethodType { get; set; }
    
    /// <summary>
    /// Gets or sets the message template used in the logger call.
    /// </summary>
    public string? MessageTemplate { get; set; }
    
    /// <summary>
    /// Gets or sets the log level of the logger call.
    /// </summary>
    public LogLevel? LogLevel { get; set; }
    
    /// <summary>
    /// Gets or sets the event ID associated with the logger call.
    /// </summary>
    public EventIdBase? EventId { get; set; }
    
    /// <summary>
    /// Gets or sets the list of message parameters used in the logger call.
    /// </summary>
    public List<MessageParameter> MessageParameters { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the location information where the logger call was made.
    /// </summary>
    public required MethodCallLocation Location { get; set; }
}
