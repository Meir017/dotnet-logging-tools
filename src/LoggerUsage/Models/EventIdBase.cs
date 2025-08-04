using System.Text.Json.Serialization;

namespace LoggerUsage.Models;

/// <summary>
/// Base class for event ID representations, supporting polymorphic JSON serialization.
/// </summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(EventIdDetails))]
[JsonDerivedType(typeof(EventIdRef))]
public abstract record class EventIdBase;
