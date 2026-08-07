namespace LoggerUsage.Diagnostics;

internal sealed record LoggerUsageRule(
    string Id,
    string Name,
    string ShortDescription,
    string FullDescription,
    string DefaultLevel,
    string HelpUri);
