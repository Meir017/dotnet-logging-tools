using LoggerUsage.Models;

namespace LoggerUsage.Diagnostics;

internal static class LoggerUsageFindingFactory
{
    public static IReadOnlyList<LoggerUsageFinding> Create(LoggerUsageExtractionResult extractionResult)
    {
        var findings = new Dictionary<FindingKey, LoggerUsageFinding>();

        foreach (var inconsistency in extractionResult.Summary.InconsistentParameterNames)
        {
            var conflicts = inconsistency.Names
                .Distinct()
                .OrderBy(pair => pair.Name, StringComparer.Ordinal)
                .ThenBy(pair => pair.Type, StringComparer.Ordinal)
                .ToArray();

            if (inconsistency.IssueTypes.Contains(LoggerUsageRules.TypeMismatchIssue, StringComparer.Ordinal)
                && conflicts.Select(conflict => conflict.Name).Distinct(StringComparer.Ordinal).Count() == 1)
            {
                AddFindings(
                    extractionResult.Results,
                    conflicts,
                    LoggerUsageRules.ParameterTypeMismatch,
                    CreateTypeMismatchMessage(conflicts),
                    findings);
            }

            if (inconsistency.IssueTypes.Contains(LoggerUsageRules.CasingDifferenceIssue, StringComparer.Ordinal))
            {
                AddFindings(
                    extractionResult.Results,
                    conflicts,
                    LoggerUsageRules.ParameterCasingInconsistency,
                    CreateCasingMessage(conflicts),
                    findings);
            }
        }

        return findings.Values
            .OrderBy(finding => finding.Location.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.Location.StartLineNumber)
            .ThenBy(finding => finding.Location.EndLineNumber)
            .ThenBy(finding => finding.Rule.Id, StringComparer.Ordinal)
            .ThenBy(finding => finding.ParameterName, StringComparer.Ordinal)
            .ThenBy(finding => finding.ParameterType, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddFindings(
        IEnumerable<LoggerUsageInfo> usages,
        IReadOnlyList<LoggerUsageExtractionSummary.NameTypePair> conflicts,
        LoggerUsageRule rule,
        string message,
        IDictionary<FindingKey, LoggerUsageFinding> findings)
    {
        foreach (var usage in usages)
        {
            if (string.IsNullOrWhiteSpace(usage.Location.FilePath))
            {
                continue;
            }

            foreach (var parameter in usage.MessageParameters)
            {
                if (!conflicts.Any(conflict =>
                    StringComparer.Ordinal.Equals(conflict.Name, parameter.Name)
                    && StringComparer.Ordinal.Equals(conflict.Type, parameter.Type)))
                {
                    continue;
                }

                var key = new FindingKey(
                    rule.Id,
                    usage.Location.FilePath,
                    usage.Location.StartLineNumber,
                    usage.Location.EndLineNumber,
                    parameter.Name,
                    parameter.Type);

                findings.TryAdd(
                    key,
                    new LoggerUsageFinding(
                        rule,
                        message,
                        usage.Location,
                        usage.MessageTemplate,
                        parameter.Name,
                        parameter.Type,
                        conflicts));
            }
        }
    }

    private static string CreateTypeMismatchMessage(
        IReadOnlyList<LoggerUsageExtractionSummary.NameTypePair> conflicts)
    {
        var names = string.Join(", ", conflicts
            .Select(conflict => $"'{conflict.Name}'")
            .Distinct(StringComparer.Ordinal));
        var types = string.Join(", ", conflicts
            .Select(conflict => conflict.Type)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));

        return $"Logging parameter {names} is used with conflicting types: {types}.";
    }

    private static string CreateCasingMessage(
        IReadOnlyList<LoggerUsageExtractionSummary.NameTypePair> conflicts)
    {
        var names = string.Join(", ", conflicts
            .Select(conflict => $"'{conflict.Name}'")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));

        return $"Logging parameter names differ only by casing: {names}.";
    }

    private readonly record struct FindingKey(
        string RuleId,
        string FilePath,
        int StartLine,
        int EndLine,
        string ParameterName,
        string? ParameterType);
}
