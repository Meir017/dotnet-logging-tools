using LoggerUsage.Models;
using System.Text;

namespace LoggerUsage.ReportGenerator;

/// <summary>
/// Generates markdown formatted reports from logger usage extraction results.
/// </summary>
internal class MarkdownLoggerReportGenerator : ILoggerReportGenerator
{
    public string GenerateReport(LoggerUsageExtractionResult loggerUsage)
    {
        var markdown = new StringBuilder();

        // Title
        markdown.AppendLine("# Logger Usage Report");
        markdown.AppendLine();

        // Summary section
        GenerateSummarySection(markdown, loggerUsage.Summary, loggerUsage.Results.Count);

        // Table of contents
        GenerateTableOfContents(markdown, loggerUsage);

        // Detailed results
        GenerateDetailedResults(markdown, loggerUsage.Results);

        // Parameter inconsistencies section
        if (loggerUsage.Summary.InconsistentParameterNames.Count > 0)
        {
            GenerateParameterInconsistenciesSection(markdown, loggerUsage.Summary);
        }

        return markdown.ToString();
    }

    private static void GenerateSummarySection(StringBuilder markdown, LoggerUsageExtractionSummary summary, int totalUsages)
    {
        markdown.AppendLine("## 📊 Summary");
        markdown.AppendLine();

        // Summary table
        markdown.AppendLine("| Metric | Value |");
        markdown.AppendLine("|--------|-------|");
        markdown.AppendLine($"| Total Log Usages | {totalUsages} |");
        markdown.AppendLine($"| Unique Parameter Names | {summary.UniqueParameterNameCount} |");
        markdown.AppendLine($"| Total Parameter Usages | {summary.TotalParameterUsageCount} |");
        markdown.AppendLine($"| Parameter Name Inconsistencies | {summary.InconsistentParameterNames.Count} |");
        markdown.AppendLine();

        // Most common parameter names
        if (summary.CommonParameterNames.Count > 0)
        {
            markdown.AppendLine("### Most Common Parameter Names");
            markdown.AppendLine();
            markdown.AppendLine("| Parameter Name | Most Common Type | Usage Count |");
            markdown.AppendLine("|----------------|------------------|-------------|");

            foreach (var param in summary.CommonParameterNames.Take(10))
            {
                markdown.AppendLine($"| `{param.Name}` | `{param.MostCommonType}` | {param.Count} |");
            }

            if (summary.CommonParameterNames.Count > 10)
            {
                markdown.AppendLine($"| ... | ... | *{summary.CommonParameterNames.Count - 10} more parameters* |");
            }
            markdown.AppendLine();
        }
    }

    private static void GenerateTableOfContents(StringBuilder markdown, LoggerUsageExtractionResult loggerUsage)
    {
        markdown.AppendLine("## 📑 Table of Contents");
        markdown.AppendLine();
        markdown.AppendLine("- [📊 Summary](#-summary)");
        markdown.AppendLine("- [📝 Detailed Results](#-detailed-results)");

        if (loggerUsage.Summary.InconsistentParameterNames.Count > 0)
        {
            markdown.AppendLine("- [⚠️ Parameter Inconsistencies](#️-parameter-inconsistencies)");
        }

        markdown.AppendLine();
    }

    private static void GenerateDetailedResults(StringBuilder markdown, List<LoggerUsageInfo> results)
    {
        markdown.AppendLine("## 📝 Detailed Results");
        markdown.AppendLine();

        if (results.Count == 0)
        {
            markdown.AppendLine("*No logger usages found.*");
            markdown.AppendLine();
            return;
        }

        // Group by file for better organization
        var groupedByFile = results.GroupBy(r => r.Location.FilePath).OrderBy(g => g.Key);

        foreach (var fileGroup in groupedByFile)
        {
            var fileName = Path.GetFileName(fileGroup.Key);
            var relativePath = GetRelativePath(fileGroup.Key);

            markdown.AppendLine($"### 📄 {fileName}");
            markdown.AppendLine($"*{relativePath}*");
            markdown.AppendLine();

            // Sort by line number within file
            var sortedUsages = fileGroup.OrderBy(u => u.Location.StartLineNumber);

            foreach (var usage in sortedUsages)
            {
                GenerateUsageEntry(markdown, usage);
            }

            markdown.AppendLine();
        }
    }

    private static void GenerateUsageEntry(StringBuilder markdown, LoggerUsageInfo usage)
    {
        var logLevel = usage.LogLevel?.ToString() ?? "Unknown";
        var methodType = usage.MethodType.ToString();
        var lineNumber = usage.Location.StartLineNumber + 1;

        // Usage header with log level emoji
        var levelEmoji = GetLogLevelEmoji(logLevel);
        markdown.AppendLine($"#### {levelEmoji} Line {lineNumber}: {logLevel} - {methodType}");
        markdown.AppendLine();

        // Message template
        if (!string.IsNullOrEmpty(usage.MessageTemplate))
        {
            markdown.AppendLine("**Message Template:**");
            markdown.AppendLine($"```");
            markdown.AppendLine(usage.MessageTemplate);
            markdown.AppendLine($"```");
            markdown.AppendLine();
        }

        // EventId information
        if (usage.EventId != null)
        {
            markdown.AppendLine("**Event ID:**");
            var eventIdText = usage.EventId switch
            {
                EventIdDetails details => $"{details.Id.Value} ({details.Name.Value})",
                EventIdRef reference => reference.Name,
                _ => "Unknown"
            };
            markdown.AppendLine($"- {eventIdText}");
            markdown.AppendLine();
        }

        // Parameters
        if (usage.MessageParameters != null && usage.MessageParameters.Count > 0)
        {
            markdown.AppendLine("**Parameters:**");
            markdown.AppendLine();
            markdown.AppendLine("| Name | Type | Kind |");
            markdown.AppendLine("|------|------|------|");

            foreach (var param in usage.MessageParameters)
            {
                markdown.AppendLine($"| `{param.Name}` | `{param.Type ?? "unknown"}` | {param.Kind} |");
            }
            markdown.AppendLine();
        }

        // Location details
        var startPos = $"{usage.Location.StartLineNumber + 1}";
        var endPos = $"{usage.Location.EndLineNumber + 1}";
        var locationText = usage.Location.StartLineNumber == usage.Location.EndLineNumber
            ? $"Line {startPos}"
            : $"Lines {startPos}-{endPos}";
        markdown.AppendLine($"**Location:** {locationText}");
        markdown.AppendLine();

        markdown.AppendLine("---");
        markdown.AppendLine();
    }

    private static void GenerateParameterInconsistenciesSection(StringBuilder markdown, LoggerUsageExtractionSummary summary)
    {
        markdown.AppendLine("## ⚠️ Parameter Inconsistencies");
        markdown.AppendLine();
        markdown.AppendLine("The following parameter names are used with different types or in inconsistent ways:");
        markdown.AppendLine();

        foreach (var inconsistency in summary.InconsistentParameterNames)
        {
            markdown.AppendLine($"### Parameter Name Variations");
            markdown.AppendLine();

            markdown.AppendLine("**Names and Types:**");
            markdown.AppendLine();
            markdown.AppendLine("| Name | Type |");
            markdown.AppendLine("|------|------|");

            foreach (var name in inconsistency.Names)
            {
                markdown.AppendLine($"| `{name.Name}` | `{name.Type}` |");
            }
            markdown.AppendLine();

            if (inconsistency.IssueTypes.Count > 0)
            {
                markdown.AppendLine("**Issues:**");
                foreach (var issue in inconsistency.IssueTypes)
                {
                    markdown.AppendLine($"- {issue}");
                }
                markdown.AppendLine();
            }

            markdown.AppendLine("---");
            markdown.AppendLine();
        }
    }

    private static string GetLogLevelEmoji(string logLevel) => logLevel switch
    {
        "Trace" => "🔍",
        "Debug" => "🐛",
        "Information" => "ℹ️",
        "Warning" => "⚠️",
        "Error" => "❌",
        "Critical" => "🚨",
        "None" => "⚪",
        _ => "📝"
    };

    private static int GetLogLevelOrder(string logLevel) => logLevel switch
    {
        "Trace" => 0,
        "Debug" => 1,
        "Information" => 2,
        "Warning" => 3,
        "Error" => 4,
        "Critical" => 5,
        "None" => 6,
        _ => 7
    };

    private static string GetRelativePath(string fullPath)
    {
        try
        {
            var currentDir = Directory.GetCurrentDirectory();
            var uri = new Uri(currentDir + Path.DirectorySeparatorChar);
            var relative = uri.MakeRelativeUri(new Uri(fullPath));
            return Uri.UnescapeDataString(relative.ToString().Replace('/', Path.DirectorySeparatorChar));
        }
        catch
        {
            return fullPath;
        }
    }
}
