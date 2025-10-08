using System.Text.Json.Serialization;

namespace LoggerUsage.VSCode.Bridge.Models;

/// <summary>
/// DTO for a single logging statement insight
/// </summary>
public record LoggingInsightDto
{
    /// <summary>
    /// Unique identifier (format: "filePath:line:column")
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// The logging method type
    /// </summary>
    [JsonPropertyName("methodType")]
    public required string MethodType { get; init; }

    /// <summary>
    /// Message template string
    /// </summary>
    [JsonPropertyName("messageTemplate")]
    public required string MessageTemplate { get; init; }

    /// <summary>
    /// Log level (e.g., Information, Warning, Error)
    /// </summary>
    [JsonPropertyName("logLevel")]
    public string? LogLevel { get; init; }

    /// <summary>
    /// Event ID information
    /// </summary>
    [JsonPropertyName("eventId")]
    public EventIdDto? EventId { get; init; }

    /// <summary>
    /// Parameter names
    /// </summary>
    [JsonPropertyName("parameters")]
    public required List<string> Parameters { get; init; }

    /// <summary>
    /// File location
    /// </summary>
    [JsonPropertyName("location")]
    public required LocationDto Location { get; init; }

    /// <summary>
    /// Tags/categories for filtering
    /// </summary>
    [JsonPropertyName("tags")]
    public required List<string> Tags { get; init; }

    /// <summary>
    /// Data classification information
    /// </summary>
    [JsonPropertyName("dataClassifications")]
    public required List<DataClassificationDto> DataClassifications { get; init; }

    /// <summary>
    /// Whether this insight has any inconsistencies
    /// </summary>
    [JsonPropertyName("hasInconsistencies")]
    public required bool HasInconsistencies { get; init; }

    /// <summary>
    /// Inconsistency details if applicable
    /// </summary>
    [JsonPropertyName("inconsistencies")]
    public List<ParameterInconsistencyDto>? Inconsistencies { get; init; }
}

/// <summary>
/// Event ID information
/// </summary>
public record EventIdDto
{
    [JsonPropertyName("id")]
    public int? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

/// <summary>
/// File location information
/// </summary>
public record LocationDto
{
    [JsonPropertyName("filePath")]
    public required string FilePath { get; init; }

    [JsonPropertyName("startLine")]
    public required int StartLine { get; init; }

    [JsonPropertyName("startColumn")]
    public required int StartColumn { get; init; }

    [JsonPropertyName("endLine")]
    public required int EndLine { get; init; }

    [JsonPropertyName("endColumn")]
    public required int EndColumn { get; init; }
}

/// <summary>
/// Data classification (sensitive data detection)
/// </summary>
public record DataClassificationDto
{
    [JsonPropertyName("parameterName")]
    public required string ParameterName { get; init; }

    [JsonPropertyName("classificationType")]
    public required string ClassificationType { get; init; }
}

/// <summary>
/// Parameter inconsistency information
/// </summary>
public record ParameterInconsistencyDto
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("severity")]
    public required string Severity { get; init; }

    [JsonPropertyName("location")]
    public LocationDto? Location { get; init; }
}

/// <summary>
/// Analysis summary statistics
/// </summary>
public record AnalysisSummaryDto
{
    [JsonPropertyName("totalInsights")]
    public required int TotalInsights { get; init; }

    [JsonPropertyName("byMethodType")]
    public required Dictionary<string, int> ByMethodType { get; init; }

    [JsonPropertyName("byLogLevel")]
    public required Dictionary<string, int> ByLogLevel { get; init; }

    [JsonPropertyName("inconsistenciesCount")]
    public required int InconsistenciesCount { get; init; }

    [JsonPropertyName("filesAnalyzed")]
    public required int FilesAnalyzed { get; init; }

    [JsonPropertyName("analysisTimeMs")]
    public required long AnalysisTimeMs { get; init; }

    /// <summary>
    /// Number of compilation warnings/errors encountered during analysis
    /// </summary>
    [JsonPropertyName("warningsCount")]
    public int WarningsCount { get; init; }
}
