using System.Text.Json.Serialization;

namespace LoggerUsage.Models;

[JsonPolymorphic]
[JsonDerivedType(typeof(EventIdDetails))]
[JsonDerivedType(typeof(EventIdRef))]
public abstract record class EventIdBase;
