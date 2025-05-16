using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using LoggerUsage.Models;
using Microsoft.CodeAnalysis;
using Xunit.Sdk;

namespace LoggerUsage.Tests;

public class MessageParameterListXunitSerializer : IXunitSerializer
{
    public object Deserialize(Type type, string serializedValue) => JsonSerializer.Deserialize(serializedValue, type)!;

    public bool IsSerializable(Type type, object? value, [NotNullWhen(false)] out string? failureReason)
    {
        if (value is List<MessageParameter>)
        {
            failureReason = null;
            return true;
        }

        failureReason = $"Type {type} is not serializable";
        return false;
    }

    public string Serialize(object value) => JsonSerializer.Serialize(value);
}
