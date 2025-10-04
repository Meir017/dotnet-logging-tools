namespace LoggerUsage.Models;

/// <summary>
/// Represents a property extracted from a parameter decorated with [LogProperties] attribute.
/// </summary>
/// <param name="Name">The name of the property as it will appear in logs.</param>
/// <param name="OriginalName">The original property name from the source code.</param>
/// <param name="Type">The type of the property.</param>
/// <param name="IsNullable">Whether the property type is nullable.</param>
/// <param name="CustomTagName">The custom tag name specified by TagNameAttribute, if any.</param>
/// <param name="DataClassification">The data classification information for compliance and redaction, if any.</param>
/// <param name="NestedProperties">Optional list of nested properties for transitive analysis. Null for primitive types.</param>
public record class LogPropertyInfo(
    string Name,
    string OriginalName,
    string Type,
    bool IsNullable,
    string? CustomTagName = null,
    DataClassificationInfo? DataClassification = null,
    List<LogPropertyInfo>? NestedProperties = null);
