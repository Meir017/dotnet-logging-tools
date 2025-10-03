namespace LoggerUsage.Models;

/// <summary>
/// Represents configuration options for a [LogProperties] attribute.
/// </summary>
public record class LogPropertiesConfiguration(
    bool OmitReferenceName = false,
    bool SkipNullProperties = false,
    bool Transitive = false);

/// <summary>
/// Represents a parameter decorated with [LogProperties] attribute and its extracted properties.
/// </summary>
/// <param name="ParameterName">The name of the parameter.</param>
/// <param name="ParameterType">The type of the parameter.</param>
/// <param name="Configuration">The LogProperties attribute configuration.</param>
/// <param name="Properties">The list of properties extracted from the parameter type.</param>
/// <param name="TagProvider">Optional TagProvider information if the parameter has a TagProvider attribute.</param>
public record class LogPropertiesParameterInfo(
    string ParameterName,
    string ParameterType,
    LogPropertiesConfiguration Configuration,
    List<LogPropertyInfo> Properties,
    TagProviderInfo? TagProvider = null);
