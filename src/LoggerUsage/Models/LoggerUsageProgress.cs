namespace LoggerUsage.Models;

/// <summary>
/// Represents progress information for logger usage extraction operations.
/// </summary>
public sealed record LoggerUsageProgress
{
    private int _percentComplete;
    private string _operationDescription = string.Empty;

    /// <summary>
    /// Gets the percentage of completion (0-100).
    /// </summary>
    public required int PercentComplete
    {
        get => _percentComplete;
        init => _percentComplete = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets the description of the current operation.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is null or empty.</exception>
    public required string OperationDescription
    {
        get => _operationDescription;
        init
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Operation description cannot be null or empty.", nameof(OperationDescription));
            }
            _operationDescription = value;
        }
    }

    /// <summary>
    /// Gets the path of the file currently being analyzed, if applicable.
    /// </summary>
    public string? CurrentFilePath { get; init; }

    /// <summary>
    /// Gets the name of the analyzer currently running, if applicable.
    /// </summary>
    public string? CurrentAnalyzer { get; init; }
}
