namespace LoggerUsage.Models;

public class LoggerUsageExtractionSummary
{
    // Parameter name -> set of types used
    public Dictionary<string, HashSet<string>> ParameterTypesByName { get; set; } = new();

    // Parameter names used with more than one type or with other issues
    public List<InconsistentParameterNameInfo> InconsistentParameterNames { get; set; } = new();

    // Most common parameter names (with usage count)
    public List<CommonParameterNameInfo> CommonParameterNames { get; set; } = new();

    // Total unique parameter names
    public int UniqueParameterNameCount { get; set; }

    // Total parameter usages
    public int TotalParameterUsageCount { get; set; }

    public record struct NameTypePair(string Name, string Type);
    public record struct InconsistentParameterNameInfo(List<NameTypePair> Names, List<string> IssueTypes);
    public record struct CommonParameterNameInfo(string Name, int Count, string MostCommonType);
}
