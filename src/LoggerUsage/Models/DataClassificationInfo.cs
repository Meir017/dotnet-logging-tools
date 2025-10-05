namespace LoggerUsage.Models;

/// <summary>
/// Represents data classification information for a parameter or property.
/// </summary>
/// <param name="ClassificationTypeName">The full name of the classification attribute type.</param>
/// <param name="ClassificationValue">The classification value (e.g., "Private", "Public").</param>
/// <param name="IsCustomAttribute">Whether this is a custom classification attribute defined by the user.</param>
public record DataClassificationInfo(
    string ClassificationTypeName,
    string ClassificationValue,
    bool IsCustomAttribute);
