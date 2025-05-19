using LoggerUsage.Models;

namespace LoggerUsage;

public class LoggerUsageSummarizer
{
    public void PopulateSummary(LoggerUsageExtractionResult extractionResult)
    {
        var summary = extractionResult.Summary;
        // Use case-sensitive dictionaries to distinguish parameter names by case
        var parameterTypesByName = new Dictionary<string, HashSet<string>>();
        var parameterUsageCount = new Dictionary<string, int>();

        foreach (var usage in extractionResult.Results)
        {
            if (usage.MessageParameters == null) continue;
            foreach (var param in usage.MessageParameters)
            {
                if (!parameterTypesByName.TryGetValue(param.Name, out var types))
                {
                    types = new HashSet<string>();
                    parameterTypesByName[param.Name] = types;
                }
                if (!string.IsNullOrEmpty(param.Type))
                {
                    types.Add(param.Type);
                }

                if (!parameterUsageCount.ContainsKey(param.Name))
                    parameterUsageCount[param.Name] = 0;
                parameterUsageCount[param.Name]++;
            }
        }

        summary.ParameterTypesByName = parameterTypesByName;
        summary.TotalParameterUsageCount = parameterUsageCount.Values.Sum();
        summary.UniqueParameterNameCount = parameterTypesByName.Count;

        // Improved: Collect all inconsistencies with details
        var inconsistencies = new List<LoggerUsageExtractionSummary.InconsistentParameterNameInfo>();

        // Type mismatch: same parameter name (case-sensitive) used with multiple types
        foreach (var kvp in parameterTypesByName)
        {
            if (kvp.Value.Count > 1)
            {
                var nameTypePairs = kvp.Value
                    .Select(type => new LoggerUsage.Models.LoggerUsageExtractionSummary.NameTypePair(kvp.Key, type))
                    .ToList();
                // Always add a TypeMismatch for the single name
                inconsistencies.Add(new LoggerUsage.Models.LoggerUsageExtractionSummary.InconsistentParameterNameInfo(
                    nameTypePairs,
                    "TypeMismatch"
                ));
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
                .SelectMany(name => parameterTypesByName[name].Select(type => new LoggerUsage.Models.LoggerUsageExtractionSummary.NameTypePair(name, type)))
                .ToList();
            inconsistencies.Add(new LoggerUsage.Models.LoggerUsageExtractionSummary.InconsistentParameterNameInfo(
                nameTypePairs,
                "CasingDifference"
            ));

            // Only add a TypeMismatch for the group if there are multiple types across all names
            var allTypes = new HashSet<string>();
            foreach (var name in group)
            {
                foreach (var t in parameterTypesByName[name])
                    allTypes.Add(t);
            }
            // Only add if there are at least two types and at least two distinct names in the group
            if (allTypes.Count > 1 && group.Count > 1)
            {
                inconsistencies.Add(new LoggerUsage.Models.LoggerUsageExtractionSummary.InconsistentParameterNameInfo(
                    nameTypePairs,
                    "TypeMismatch"
                ));
            }
        }

        summary.InconsistentParameterNames = inconsistencies;

        // Find most common parameter names
        summary.CommonParameterNames = parameterUsageCount
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
            })
            .ToList();
    }
}
