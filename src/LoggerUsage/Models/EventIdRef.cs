namespace LoggerUsage.Models;

/// <summary>
/// Represents a reference to an event ID by kind and name.
/// </summary>
/// <param name="Kind">The kind or type of the event ID reference.</param>
/// <param name="Name">The name of the event ID reference.</param>
public record class EventIdRef(string Kind, string Name) : EventIdBase;
