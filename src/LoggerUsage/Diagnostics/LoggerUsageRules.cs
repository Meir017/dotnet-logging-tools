namespace LoggerUsage.Diagnostics;

internal static class LoggerUsageRules
{
    public const string TypeMismatchIssue = "TypeMismatch";
    public const string CasingDifferenceIssue = "CasingDifference";

    public static readonly LoggerUsageRule ParameterTypeMismatch = new(
        "LUT001",
        "ParameterTypeMismatch",
        "Logging parameter types should be consistent",
        "Use one semantic type for a logging parameter name, or rename parameters that represent different concepts.",
        "warning",
        "https://github.com/Meir017/dotnet-logging-tools#lut001-parameter-type-mismatch");

    public static readonly LoggerUsageRule ParameterCasingInconsistency = new(
        "LUT002",
        "ParameterCasingInconsistency",
        "Logging parameter casing should be consistent",
        "Choose one canonical spelling and casing for each logging parameter name.",
        "note",
        "https://github.com/Meir017/dotnet-logging-tools#lut002-parameter-casing-inconsistency");

    public static IReadOnlyList<LoggerUsageRule> All { get; } =
    [
        ParameterTypeMismatch,
        ParameterCasingInconsistency
    ];
}
