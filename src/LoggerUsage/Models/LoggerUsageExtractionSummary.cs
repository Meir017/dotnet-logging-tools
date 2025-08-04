namespace LoggerUsage.Models;

/// <summary>
/// Provides summary statistics and analysis of logger usage extraction results.
/// </summary>
public class LoggerUsageExtractionSummary
{
    /// <summary>
    /// Gets or sets a dictionary mapping parameter names to the set of types used with each name.
    /// </summary>
    public Dictionary<string, HashSet<string>> ParameterTypesByName { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of parameter names that have inconsistencies, such as being used with multiple types.
    /// </summary>
    public List<InconsistentParameterNameInfo> InconsistentParameterNames { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of most commonly used parameter names with their usage statistics.
    /// </summary>
    public List<CommonParameterNameInfo> CommonParameterNames { get; set; } = new();

    /// <summary>
    /// Gets or sets the total number of unique parameter names found.
    /// </summary>
    public int UniqueParameterNameCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of parameter usages across all logger calls.
    /// </summary>
    public int TotalParameterUsageCount { get; set; }

    /// <summary>
    /// Represents a parameter name and its associated type.
    /// </summary>
    /// <param name="Name">The parameter name.</param>
    /// <param name="Type">The parameter type.</param>
    public record struct NameTypePair(string Name, string Type);
    
    /// <summary>
    /// Contains information about a parameter name that has consistency issues.
    /// </summary>
    /// <param name="Names">The list of name-type pairs that show the inconsistency.</param>
    /// <param name="IssueTypes">The types of issues found with this parameter name.</param>
    public record struct InconsistentParameterNameInfo(List<NameTypePair> Names, List<string> IssueTypes);
    
    /// <summary>
    /// Contains information about a commonly used parameter name.
    /// </summary>
    /// <param name="Name">The parameter name.</param>
    /// <param name="Count">The number of times this parameter name is used.</param>
    /// <param name="MostCommonType">The most frequently used type for this parameter name.</param>
    public record struct CommonParameterNameInfo(string Name, int Count, string MostCommonType);
}
