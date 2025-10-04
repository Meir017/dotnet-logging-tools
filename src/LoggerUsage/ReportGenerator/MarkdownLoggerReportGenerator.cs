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

        // Classification statistics (if any)
        if (summary.ClassificationStats.HasClassifications)
        {
            markdown.AppendLine("### 🔒 Data Classification Summary");
            markdown.AppendLine();
            markdown.AppendLine("| Metric | Value |");
            markdown.AppendLine("|--------|-------|");
            markdown.AppendLine($"| Classified Parameters | {summary.ClassificationStats.TotalClassifiedParameters} |");
            markdown.AppendLine($"| Classified Properties | {summary.ClassificationStats.TotalClassifiedProperties} |");
            markdown.AppendLine($"| Sensitive Data Percentage | {summary.ClassificationStats.SensitiveParameterPercentage:F1}% |");
            markdown.AppendLine();

            if (summary.ClassificationStats.ByLevel.Count > 0)
            {
                markdown.AppendLine("**Classification Breakdown:**");
                markdown.AppendLine();
                foreach (var kvp in summary.ClassificationStats.ByLevel.OrderBy(x => x.Key))
                {
                    var icon = GetClassificationIcon(kvp.Key);
                    markdown.AppendLine($"- {icon} **{kvp.Key}**: {kvp.Value}");
                }
                markdown.AppendLine();
            }

            if (summary.ClassificationStats.SensitiveParameterPercentage > 0)
            {
                markdown.AppendLine("> ⚠️ **Compliance Note:** Some parameters contain sensitive data and may be redacted at runtime if redaction is enabled.");
                markdown.AppendLine();
            }
        }

        // Telemetry features statistics (if any)
        if (summary.TelemetryStats.HasTelemetryFeatures)
        {
            markdown.AppendLine("### 🏷️ Telemetry Features Summary");
            markdown.AppendLine();
            markdown.AppendLine("| Metric | Value |");
            markdown.AppendLine("|--------|-------|");
            markdown.AppendLine($"| Parameters with Custom Tag Names | {summary.TelemetryStats.ParametersWithCustomTagNames} |");
            markdown.AppendLine($"| Properties with Custom Tag Names | {summary.TelemetryStats.PropertiesWithCustomTagNames} |");
            markdown.AppendLine($"| Parameters with Tag Providers | {summary.TelemetryStats.ParametersWithTagProviders} |");
            markdown.AppendLine($"| Transitive Properties | {summary.TelemetryStats.TotalTransitiveProperties} |");
            markdown.AppendLine();

            // Custom tag name mappings
            if (summary.TelemetryStats.CustomTagNameMappings.Count > 0)
            {
                markdown.AppendLine("**Custom Tag Name Mappings:**");
                markdown.AppendLine();
                markdown.AppendLine("| Original Name | Custom Tag Name | Context |");
                markdown.AppendLine("|---------------|-----------------|---------|");
                foreach (var mapping in summary.TelemetryStats.CustomTagNameMappings.Take(20))
                {
                    markdown.AppendLine($"| `{mapping.OriginalName}` | `{mapping.CustomTagName}` | {mapping.Context} |");
                }
                if (summary.TelemetryStats.CustomTagNameMappings.Count > 20)
                {
                    markdown.AppendLine($"| ... | ... | *{summary.TelemetryStats.CustomTagNameMappings.Count - 20} more mappings* |");
                }
                markdown.AppendLine();
            }

            // Tag providers
            if (summary.TelemetryStats.TagProviders.Count > 0)
            {
                markdown.AppendLine("**Tag Providers:**");
                markdown.AppendLine();
                markdown.AppendLine("| Parameter | Provider Type | Provider Method | Omit Name | Valid |");
                markdown.AppendLine("|-----------|---------------|-----------------|-----------|-------|");
                foreach (var provider in summary.TelemetryStats.TagProviders)
                {
                    var validIcon = provider.IsValid ? "✓" : "⚠️";
                    markdown.AppendLine($"| `{provider.ParameterName}` | `{provider.ProviderTypeName}` | `{provider.ProviderMethodName}` | {provider.OmitReferenceName} | {validIcon} |");
                    if (!provider.IsValid && !string.IsNullOrEmpty(provider.ValidationMessage))
                    {
                        markdown.AppendLine($"| | **Validation:** {provider.ValidationMessage} | | | |");
                    }
                }
                markdown.AppendLine();
            }
        }

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

    private static string GetClassificationIcon(DataClassificationLevel level) => level switch
    {
        DataClassificationLevel.Public => "🌐",
        DataClassificationLevel.Internal => "🏢",
        DataClassificationLevel.Private => "🔒",
        DataClassificationLevel.Sensitive => "🔐",
        DataClassificationLevel.Custom => "🏷️",
        DataClassificationLevel.None => "⚪",
        _ => "❓"
    };

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

        // Handle LoggerMessage specific information
        if (usage is LoggerMessageUsageInfo loggerMessageUsage)
        {
            markdown.AppendLine("**LoggerMessage Method Details:**");
            markdown.AppendLine($"- **Declaring Type:** `{loggerMessageUsage.DeclaringTypeName}`");
            markdown.AppendLine($"- **Method Name:** `{loggerMessageUsage.MethodName}`");
            markdown.AppendLine($"- **Invocation Count:** {loggerMessageUsage.InvocationCount}");
            markdown.AppendLine();
        }

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
            markdown.AppendLine("| Name | Type | Kind | Custom Tag Name | Classification |");
            markdown.AppendLine("|------|------|------|-----------------|----------------|");

            foreach (var param in usage.MessageParameters)
            {
                var customTag = !string.IsNullOrEmpty(param.CustomTagName) ? $"`{param.CustomTagName}`" : "-";
                var classification = param.DataClassification != null 
                    ? $"{GetClassificationIcon(param.DataClassification.Level)} {param.DataClassification.Level}" 
                    : "-";
                markdown.AppendLine($"| `{param.Name}` | `{param.Type ?? "unknown"}` | {param.Kind} | {customTag} | {classification} |");
            }
            markdown.AppendLine();

            // Add security note if any parameters are classified as sensitive
            if (usage.MessageParameters.Any(p => p.DataClassification != null && 
                (p.DataClassification.Level == DataClassificationLevel.Private || 
                 p.DataClassification.Level == DataClassificationLevel.Sensitive)))
            {
                markdown.AppendLine("> 🔒 **Security Note:** This log contains sensitive data that may be redacted at runtime.");
                markdown.AppendLine();
            }
        }

        // LoggerMessage invocations
        if (usage is LoggerMessageUsageInfo loggerMessageUsageWithInvocations && loggerMessageUsageWithInvocations.HasInvocations)
        {
            markdown.AppendLine("**Invocations:**");
            markdown.AppendLine();
            
            foreach (var invocation in loggerMessageUsageWithInvocations.Invocations)
            {
                var invocationLine = invocation.InvocationLocation.StartLineNumber + 1;
                var invocationFile = Path.GetFileName(invocation.InvocationLocation.FilePath);
                
                markdown.AppendLine($"- **{invocationFile}** (Line {invocationLine})");
                markdown.AppendLine($"  - **Containing Type:** `{invocation.ContainingType}`");
                
                if (invocation.Arguments.Count > 0)
                {
                    markdown.AppendLine($"  - **Arguments:**");
                    foreach (var arg in invocation.Arguments)
                    {
                        markdown.AppendLine($"    - `{arg.Name}`: `{arg.Type ?? "unknown"}` ({arg.Kind})");
                    }
                }
                markdown.AppendLine();
            }
        }

        // LogProperties parameters
        if (usage is LoggerMessageUsageInfo loggerMessageUsageWithLogProperties && loggerMessageUsageWithLogProperties.HasLogProperties)
        {
            markdown.AppendLine("**LogProperties Parameters:**");
            markdown.AppendLine();
            
            foreach (var logPropsParam in loggerMessageUsageWithLogProperties.LogPropertiesParameters)
            {
                markdown.AppendLine($"- **Parameter:** `{logPropsParam.ParameterName}` (`{logPropsParam.ParameterType}`)");
                
                // TagProvider information
                if (logPropsParam.TagProvider != null)
                {
                    var statusIcon = logPropsParam.TagProvider.IsValid ? "✓" : "✗";
                    markdown.AppendLine($"  - **Tag Provider:** `{logPropsParam.TagProvider.ProviderTypeName}.{logPropsParam.TagProvider.ProviderMethodName}` {statusIcon}");
                    
                    if (logPropsParam.TagProvider.OmitReferenceName)
                    {
                        markdown.AppendLine("    - OmitReferenceName: true");
                    }
                    
                    if (!logPropsParam.TagProvider.IsValid && !string.IsNullOrEmpty(logPropsParam.TagProvider.ValidationMessage))
                    {
                        markdown.AppendLine($"    - ⚠️ **Validation Error:** {logPropsParam.TagProvider.ValidationMessage}");
                    }
                }
                
                // Configuration
                var configOptions = new List<string>();
                if (logPropsParam.Configuration.OmitReferenceName)
                {
                    configOptions.Add("OmitReferenceName");
                }
                if (logPropsParam.Configuration.SkipNullProperties)
                {
                    configOptions.Add("SkipNullProperties");
                }
                if (logPropsParam.Configuration.Transitive)
                {
                    configOptions.Add("Transitive");
                }
                
                if (configOptions.Count > 0)
                {
                    markdown.AppendLine($"  - **Configuration:** {string.Join(", ", configOptions)}");
                }
                
                markdown.AppendLine($"  - **Properties:** {logPropsParam.Properties.Count} properties extracted");
                
                // Display properties in a tree structure
                GeneratePropertyTree(markdown, logPropsParam.Properties, 2);
                
                markdown.AppendLine();
            }
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

    /// <summary>
    /// Generates a hierarchical tree structure for LogProperties
    /// </summary>
    private static void GeneratePropertyTree(StringBuilder markdown, List<LogPropertyInfo> properties, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 2);
        
        foreach (var property in properties)
        {
            var nullableIndicator = property.IsNullable ? "?" : "";
            markdown.Append($"{indent}- `{property.Name}`: `{property.Type}{nullableIndicator}`");
            
            // Show custom tag name if present
            if (!string.IsNullOrEmpty(property.CustomTagName))
            {
                markdown.Append($" → `{property.CustomTagName}`");
            }
            
            // Show data classification if present
            if (property.DataClassification != null)
            {
                var icon = GetClassificationIcon(property.DataClassification.Level);
                markdown.Append($" {icon} *{property.DataClassification.Level}*");
            }
            
            // If there are nested properties, indicate collection or complex type
            if (property.NestedProperties != null && property.NestedProperties.Count > 0)
            {
                markdown.AppendLine(" ⮑");
                GeneratePropertyTree(markdown, property.NestedProperties, indentLevel + 1);
            }
            else
            {
                markdown.AppendLine();
            }
        }
    }

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
