using LoggerUsage.Models;

namespace LoggerUsage.Diagnostics;

internal sealed record LoggerUsageFinding(
    LoggerUsageRule Rule,
    string Message,
    MethodCallLocation Location,
    string? MessageTemplate,
    string ParameterName,
    string? ParameterType,
    IReadOnlyList<LoggerUsageExtractionSummary.NameTypePair> Conflicts);
