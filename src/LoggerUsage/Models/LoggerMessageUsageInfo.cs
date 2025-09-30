using System.Linq;

namespace LoggerUsage.Models;

/// <summary>
/// Extends LoggerUsageInfo with LoggerMessage-specific functionality including invocation tracking.
/// </summary>
public class LoggerMessageUsageInfo : LoggerUsageInfo
{
    /// <summary>
    /// All invocations of this LoggerMessage method found in the analyzed code.
    /// </summary>
    public List<LoggerMessageInvocation> Invocations { get; set; } = [];

    /// <summary>
    /// The fully qualified name of the containing type where this LoggerMessage method is declared.
    /// </summary>
    public required string DeclaringTypeName { get; set; }

    /// <summary>
    /// Gets or sets the list of parameters decorated with [LogProperties] attribute.
    /// </summary>
    public List<LogPropertiesParameterInfo> LogPropertiesParameters { get; set; } = [];

    /// <summary>
    /// Indicates whether this LoggerMessage method has any invocations.
    /// </summary>
    public bool HasInvocations => Invocations.Count > 0;

    /// <summary>
    /// The total number of invocations found for this LoggerMessage method.
    /// </summary>
    public int InvocationCount => Invocations.Count;

    /// <summary>
    /// Gets whether this LoggerMessage method uses LogProperties parameters.
    /// </summary>
    public bool HasLogProperties => LogPropertiesParameters.Count > 0;

    /// <summary>
    /// Gets the total number of structured properties that will be logged from all LogProperties parameters.
    /// </summary>
    public int TotalLogPropertiesCount => LogPropertiesParameters.Sum(p => p.Properties.Count);
}
