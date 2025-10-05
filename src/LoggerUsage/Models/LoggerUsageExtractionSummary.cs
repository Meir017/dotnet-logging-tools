namespace LoggerUsage.Models;

/// <summary>
/// Provides summary statistics and analysis of logger usage extraction results.
/// </summary>
public class LoggerUsageExtractionSummary
{
    /// <summary>
    /// Gets or sets a dictionary mapping parameter names to the set of types used with each name.
    /// </summary>
    public Dictionary<string, HashSet<string>> ParameterTypesByName { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of parameter names that have inconsistencies, such as being used with multiple types.
    /// </summary>
    public List<InconsistentParameterNameInfo> InconsistentParameterNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of most commonly used parameter names with their usage statistics.
    /// </summary>
    public List<CommonParameterNameInfo> CommonParameterNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the total number of unique parameter names found.
    /// </summary>
    public int UniqueParameterNameCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of parameter usages across all logger calls.
    /// </summary>
    public int TotalParameterUsageCount { get; set; }

    /// <summary>
    /// Gets or sets classification statistics for parameters and properties.
    /// </summary>
    public ClassificationStatistics ClassificationStats { get; set; } = new();

    /// <summary>
    /// Gets or sets telemetry statistics for custom tag names, tag providers, and transitive properties.
    /// </summary>
    public TelemetryStatistics TelemetryStats { get; set; } = new();

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

    /// <summary>
    /// Contains classification statistics for parameters and properties.
    /// </summary>
    public class ClassificationStatistics
    {
        /// <summary>
        /// Gets or sets the total number of parameters with data classification.
        /// </summary>
        public int TotalClassifiedParameters { get; set; }

        /// <summary>
        /// Gets or sets the total number of properties with data classification.
        /// </summary>
        public int TotalClassifiedProperties { get; set; }

        /// <summary>
        /// Gets or sets the breakdown of classifications by value.
        /// </summary>
        public Dictionary<string, int> ByValue { get; set; } = [];

        /// <summary>
        /// Gets or sets the percentage of parameters with sensitive classification (Private or Sensitive levels).
        /// </summary>
        public double SensitiveParameterPercentage { get; set; }

        /// <summary>
        /// Gets or sets whether any data classifications were found in the analysis.
        /// </summary>
        public bool HasClassifications => TotalClassifiedParameters > 0 || TotalClassifiedProperties > 0;
    }

    /// <summary>
    /// Contains telemetry statistics for custom tag names, tag providers, and transitive properties.
    /// </summary>
    public class TelemetryStatistics
    {
        /// <summary>
        /// Gets or sets the total number of parameters with custom tag names.
        /// </summary>
        public int ParametersWithCustomTagNames { get; set; }

        /// <summary>
        /// Gets or sets the total number of properties with custom tag names.
        /// </summary>
        public int PropertiesWithCustomTagNames { get; set; }

        /// <summary>
        /// Gets or sets the total number of parameters with tag providers.
        /// </summary>
        public int ParametersWithTagProviders { get; set; }

        /// <summary>
        /// Gets or sets the total number of transitive properties extracted.
        /// </summary>
        public int TotalTransitiveProperties { get; set; }

        /// <summary>
        /// Gets or sets the list of unique custom tag name mappings found (original name -> custom tag name).
        /// </summary>
        public List<CustomTagNameMapping> CustomTagNameMappings { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of tag provider information found.
        /// </summary>
        public List<TagProviderInfo> TagProviders { get; set; } = [];

        /// <summary>
        /// Gets or sets whether any telemetry features were found in the analysis.
        /// </summary>
        public bool HasTelemetryFeatures =>
            ParametersWithCustomTagNames > 0 ||
            PropertiesWithCustomTagNames > 0 ||
            ParametersWithTagProviders > 0 ||
            TotalTransitiveProperties > 0;
    }

    /// <summary>
    /// Represents a custom tag name mapping.
    /// </summary>
    /// <param name="OriginalName">The original parameter or property name.</param>
    /// <param name="CustomTagName">The custom tag name specified by TagNameAttribute.</param>
    /// <param name="Context">The context where this mapping was found (e.g., "Parameter", "Property").</param>
    public record struct CustomTagNameMapping(string OriginalName, string CustomTagName, string Context);
}
