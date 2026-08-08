namespace LoggerUsage.ReportGenerator;

/// <summary>
/// Provides optional context used while generating a report.
/// </summary>
/// <param name="SourceRoot">The source root used to create relative source paths.</param>
public sealed record ReportGenerationContext(string? SourceRoot = null);
