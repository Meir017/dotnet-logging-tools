using LoggerUsage.Models;

namespace LoggerUsage.Configuration;

/// <summary>
/// Configuration options for enhanced error handling and diagnostics.
/// </summary>
public class ErrorHandlingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to use enhanced error handling with detailed diagnostics.
    /// </summary>
    public bool UseEnhancedErrorHandling { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to log extraction attempts at Debug level.
    /// </summary>
    public bool LogExtractionAttempts { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to log extraction failures at Warning level.
    /// </summary>
    public bool LogExtractionFailures { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to continue processing when individual extractions fail.
    /// </summary>
    public bool ContinueOnExtractionFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to collect detailed error statistics.
    /// </summary>
    public bool CollectErrorStatistics { get; set; } = false;
}

/// <summary>
/// Statistics about extraction operations and errors.
/// </summary>
public class ExtractionStatistics
{
    /// <summary>
    /// Gets or sets the total number of extraction attempts.
    /// </summary>
    public int TotalAttempts { get; set; }

    /// <summary>
    /// Gets or sets the number of successful extractions.
    /// </summary>
    public int SuccessfulExtractions { get; set; }

    /// <summary>
    /// Gets or sets the number of failed extractions.
    /// </summary>
    public int FailedExtractions { get; set; }

    /// <summary>
    /// Gets or sets the most common error messages and their counts.
    /// </summary>
    public Dictionary<string, int> ErrorCounts { get; set; } = new();

    /// <summary>
    /// Gets or sets the operation types that failed most frequently.
    /// </summary>
    public Dictionary<string, int> FailedOperationTypes { get; set; } = new();

    /// <summary>
    /// Gets the success rate as a percentage.
    /// </summary>
    public double SuccessRate => TotalAttempts > 0 ? (double)SuccessfulExtractions / TotalAttempts * 100 : 0;
}
