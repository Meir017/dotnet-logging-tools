namespace LoggerUsage.Models;

/// <summary>
/// Represents detailed event ID information including both ID and name components.
/// </summary>
/// <param name="Id">The event ID value as a constant or reference.</param>
/// <param name="Name">The event name as a constant or reference.</param>
public record class EventIdDetails(ConstantOrReference Id, ConstantOrReference Name) : EventIdBase;
