using LoggerUsage.Models;

namespace LoggerUsage;

/// <summary>
/// Provides functionality to generate summary statistics from logger usage extraction results.
/// </summary>
public class LoggerUsageSummarizer
{
    /// <summary>
    /// Populates the summary section of the extraction result with aggregated statistics and insights.
    /// </summary>
    /// <param name="extractionResult">The extraction result to populate with summary information.</param>
    public void PopulateSummary(LoggerUsageExtractionResult extractionResult)
    {
        var summary = extractionResult.Summary;
        // Use case-sensitive dictionaries to distinguish parameter names by case
        var parameterTypesByName = new Dictionary<string, HashSet<string>>();
        var parameterUsageCount = new Dictionary<string, int>();

        foreach (var usage in extractionResult.Results)
        {
            if (usage.MessageParameters == null)
            {
                continue;
            }

            foreach (var param in usage.MessageParameters)
            {
                if (!parameterTypesByName.TryGetValue(param.Name, out var types))
                {
                    types = [];
                    parameterTypesByName[param.Name] = types;
                }
                if (!string.IsNullOrEmpty(param.Type))
                {
                    types.Add(param.Type);
                }

                if (!parameterUsageCount.ContainsKey(param.Name))
                {
                    parameterUsageCount[param.Name] = 0;
                }

                parameterUsageCount[param.Name]++;
            }
        }

        summary.ParameterTypesByName = parameterTypesByName;
        summary.TotalParameterUsageCount = parameterUsageCount.Values.Sum();
        summary.UniqueParameterNameCount = parameterTypesByName.Count;

        // Improved: Collect all inconsistencies with details
        var inconsistenciesRaw = new List<(List<LoggerUsageExtractionSummary.NameTypePair> Names, string IssueType)>();

        // Type mismatch: same parameter name (case-sensitive) used with multiple types
        foreach (var kvp in parameterTypesByName)
        {
            if (kvp.Value.Count > 1)
            {
                var nameTypePairs = kvp.Value
                    .Select(type => new LoggerUsageExtractionSummary.NameTypePair(kvp.Key, type))
                    .ToList();
                inconsistenciesRaw.Add((nameTypePairs, "TypeMismatch"));
            }
        }

        // Casing differences: group parameter names that differ only by casing
        var lowerCaseGroups = parameterTypesByName.Keys
            .GroupBy(name => name.ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => g.ToList())
            .ToList();
        foreach (var group in lowerCaseGroups)
        {
            // Add CasingDifference for the group
            var nameTypePairs = group
                .SelectMany(name => parameterTypesByName[name].Select(type => new LoggerUsageExtractionSummary.NameTypePair(name, type)))
                .ToList();
            inconsistenciesRaw.Add((nameTypePairs, "CasingDifference"));

            // Only add a TypeMismatch for the group if there are multiple types across all names
            var allTypes = new HashSet<string>();
            foreach (var name in group)
            {
                foreach (var t in parameterTypesByName[name])
                {
                    allTypes.Add(t);
                }
            }
            if (allTypes.Count > 1 && group.Count > 1)
            {
                inconsistenciesRaw.Add((nameTypePairs, "TypeMismatch"));
            }
        }

        // Group by Names (set equality) and aggregate IssueTypes
        var inconsistencies = inconsistenciesRaw
            .GroupBy(x => x.Names, new NameTypePairListComparer())
            .Select(g => new LoggerUsageExtractionSummary.InconsistentParameterNameInfo(
                g.Key,
                [.. g.Select(x => x.IssueType).Distinct()]
            ))
            .ToList();

        summary.InconsistentParameterNames = inconsistencies;

        // Find most common parameter names
        summary.CommonParameterNames = [.. parameterUsageCount
            .OrderByDescending(kvp => kvp.Value)
            .Take(10)
            .Select(kvp =>
            {
                var name = kvp.Key;
                var count = kvp.Value;
                var mostCommonType = parameterTypesByName[name]
                    .GroupBy(t => t)
                    .OrderByDescending(g => g.Count())
                    .First().Key;
                return new LoggerUsageExtractionSummary.CommonParameterNameInfo
                {
                    Name = name,
                    Count = count,
                    MostCommonType = mostCommonType
                };
            })];

        // Calculate classification statistics
        PopulateClassificationStatistics(extractionResult);

        // Calculate telemetry statistics
        PopulateTelemetryStatistics(extractionResult);
    }

    /// <summary>
    /// Populates classification statistics from the extraction results.
    /// </summary>
    private void PopulateClassificationStatistics(LoggerUsageExtractionResult extractionResult)
    {
        var stats = extractionResult.Summary.ClassificationStats;
        var classificationCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Count classified parameters
        foreach (var usage in extractionResult.Results)
        {
            if (usage.MessageParameters != null)
            {
                foreach (var param in usage.MessageParameters)
                {
                    if (param.DataClassification != null)
                    {
                        stats.TotalClassifiedParameters++;
                        var value = param.DataClassification.ClassificationValue;
                        classificationCounts[value] = classificationCounts.GetValueOrDefault(value) + 1;
                    }
                }
            }

            // Count classified properties in LogProperties
            if (usage is LoggerMessageUsageInfo loggerMessageUsage)
            {
                foreach (var logPropsParam in loggerMessageUsage.LogPropertiesParameters)
                {
                    CountClassifiedProperties(logPropsParam.Properties, classificationCounts, stats);
                }
            }
        }

        stats.ByValue = classificationCounts;

        // Calculate sensitive parameter percentage
        var totalParams = stats.TotalClassifiedParameters + stats.TotalClassifiedProperties;
        if (totalParams > 0)
        {
            // Count values that contain "Private" or "Sensitive"
            var sensitiveCount = classificationCounts
                .Where(kvp => kvp.Key.Contains("Private", StringComparison.OrdinalIgnoreCase) ||
                             kvp.Key.Contains("Sensitive", StringComparison.OrdinalIgnoreCase) ||
                             kvp.Key.Contains("Confidential", StringComparison.OrdinalIgnoreCase))
                .Sum(kvp => kvp.Value);
            stats.SensitiveParameterPercentage = (double)sensitiveCount / totalParams * 100.0;
        }
    }

    /// <summary>
    /// Recursively counts classified properties including nested ones.
    /// </summary>
    private void CountClassifiedProperties(
        List<LogPropertyInfo> properties,
        Dictionary<string, int> classificationCounts,
        LoggerUsageExtractionSummary.ClassificationStatistics stats)
    {
        foreach (var property in properties)
        {
            if (property.DataClassification != null)
            {
                stats.TotalClassifiedProperties++;
                var value = property.DataClassification.ClassificationValue;
                classificationCounts[value] = classificationCounts.GetValueOrDefault(value) + 1;
            }

            // Recursively count nested properties
            if (property.NestedProperties != null && property.NestedProperties.Count > 0)
            {
                CountClassifiedProperties(property.NestedProperties, classificationCounts, stats);
            }
        }
    }

    /// <summary>
    /// Populates telemetry statistics from the extraction results.
    /// </summary>
    private void PopulateTelemetryStatistics(LoggerUsageExtractionResult extractionResult)
    {
        var stats = extractionResult.Summary.TelemetryStats;
        var customTagNameMappings = new HashSet<LoggerUsageExtractionSummary.CustomTagNameMapping>();
        var tagProviders = new Dictionary<string, TagProviderInfo>();

        foreach (var usage in extractionResult.Results)
        {
            // Count parameters with custom tag names
            if (usage.MessageParameters != null)
            {
                foreach (var param in usage.MessageParameters)
                {
                    if (!string.IsNullOrEmpty(param.CustomTagName))
                    {
                        stats.ParametersWithCustomTagNames++;
                        customTagNameMappings.Add(new LoggerUsageExtractionSummary.CustomTagNameMapping(
                            param.Name,
                            param.CustomTagName,
                            "Parameter"
                        ));
                    }
                }
            }

            // Process LogProperties parameters for tag names, providers, and transitive properties
            if (usage is LoggerMessageUsageInfo loggerMessageUsage && loggerMessageUsage.LogPropertiesParameters != null)
            {
                foreach (var logPropertiesParam in loggerMessageUsage.LogPropertiesParameters)
                {
                    // Count tag providers
                    if (logPropertiesParam.TagProvider != null)
                    {
                        stats.ParametersWithTagProviders++;
                        var key = $"{logPropertiesParam.TagProvider.ParameterName}:{logPropertiesParam.TagProvider.ProviderTypeName}";
                        if (!tagProviders.ContainsKey(key))
                        {
                            tagProviders[key] = logPropertiesParam.TagProvider;
                        }
                    }

                    // Count properties with custom tag names and transitive properties
                    CountPropertiesWithTelemetryFeatures(logPropertiesParam.Properties, stats, customTagNameMappings);
                }
            }
        }

        stats.CustomTagNameMappings = [.. customTagNameMappings.OrderBy(m => m.OriginalName)];
        stats.TagProviders = [.. tagProviders.Values.OrderBy(tp => tp.ParameterName)];
    }

    /// <summary>
    /// Recursively counts properties with custom tag names and transitive properties.
    /// </summary>
    private void CountPropertiesWithTelemetryFeatures(
        List<LogPropertyInfo> properties,
        LoggerUsageExtractionSummary.TelemetryStatistics stats,
        HashSet<LoggerUsageExtractionSummary.CustomTagNameMapping> customTagNameMappings)
    {
        foreach (var property in properties)
        {
            if (!string.IsNullOrEmpty(property.CustomTagName))
            {
                stats.PropertiesWithCustomTagNames++;
                customTagNameMappings.Add(new LoggerUsageExtractionSummary.CustomTagNameMapping(
                    property.OriginalName,
                    property.CustomTagName,
                    "Property"
                ));
            }

            // Count transitive properties (nested properties)
            if (property.NestedProperties != null && property.NestedProperties.Count > 0)
            {
                stats.TotalTransitiveProperties += property.NestedProperties.Count;
                CountPropertiesWithTelemetryFeatures(property.NestedProperties, stats, customTagNameMappings);
            }
        }
    }

    // Compares two lists of NameTypePair for set equality (order-insensitive, unique pairs)
    private class NameTypePairListComparer : IEqualityComparer<List<LoggerUsageExtractionSummary.NameTypePair>>
    {
        public bool Equals(List<LoggerUsageExtractionSummary.NameTypePair>? x, List<LoggerUsageExtractionSummary.NameTypePair>? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            if (x.Count != y.Count)
            {
                return false;
            }

            var setX = new HashSet<LoggerUsageExtractionSummary.NameTypePair>(x);
            var setY = new HashSet<LoggerUsageExtractionSummary.NameTypePair>(y);
            return setX.SetEquals(setY);
        }

        public int GetHashCode(List<LoggerUsageExtractionSummary.NameTypePair> obj)
        {
            int hash = 19;
            foreach (var pair in obj.OrderBy(p => p.Name).ThenBy(p => p.Type))
            {
                hash = hash * 31 + pair.Name.GetHashCode();
                hash = hash * 31 + (pair.Type?.GetHashCode() ?? 0);
            }
            return hash;
        }
    }
}
