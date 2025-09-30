namespace LoggerUsage.Models;

/// <summary>
/// Represents a property extracted from a parameter decorated with [LogProperties] attribute.
/// </summary>
/// <param name="Name">The name of the property as it will appear in logs.</param>
/// <param name="OriginalName">The original property name from the source code.</param>
/// <param name="Type">The type of the property.</param>
/// <param name="IsNullable">Whether the property type is nullable.</param>
public record class LogPropertyInfo(
    string Name,
    string OriginalName,
    string Type,
    bool IsNullable);
