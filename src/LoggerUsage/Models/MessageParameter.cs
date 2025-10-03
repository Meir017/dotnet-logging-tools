namespace LoggerUsage.Models;

/// <summary>
/// Represents a parameter used in a logger message template.
/// </summary>
/// <param name="Name">The name of the parameter as it appears in the message template.</param>
/// <param name="Type">The type of the parameter, if determinable.</param>
/// <param name="Kind">The kind or category of the parameter.</param>
/// <param name="CustomTagName">The custom tag name specified by TagNameAttribute, if any.</param>
public record class MessageParameter(string Name, string? Type, string? Kind, string? CustomTagName = null);
