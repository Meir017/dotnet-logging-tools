namespace LoggerUsage.Models;

/// <summary>
/// Represents information about a TagProvider attribute on a parameter.
/// </summary>
/// <param name="ParameterName">The name of the parameter that has the TagProvider attribute.</param>
/// <param name="ProviderTypeName">The fully qualified type name of the provider type.</param>
/// <param name="ProviderMethodName">The name of the provider method.</param>
/// <param name="OmitReferenceName">Whether to omit the parameter name from tag names.</param>
/// <param name="IsValid">Whether the provider configuration is valid.</param>
/// <param name="ValidationMessage">Optional message explaining validation errors.</param>
public record class TagProviderInfo(
    string ParameterName,
    string ProviderTypeName,
    string ProviderMethodName,
    bool OmitReferenceName,
    bool IsValid,
    string? ValidationMessage = null);
