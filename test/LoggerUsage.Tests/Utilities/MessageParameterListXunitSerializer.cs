using System.Diagnostics.CodeAnalysis;
using LoggerUsage.Models;
using Microsoft.CodeAnalysis;
using Xunit.Sdk;

namespace LoggerUsage.Tests;

public class MessageParameterListXunitSerializer : IXunitSerializer
{
    public object Deserialize(Type type, string serializedValue)
    {
        if (type == typeof(List<MessageParameter>))
        {
            var parts = serializedValue.Split('|');
            var messageParameters = new List<MessageParameter>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                var subParts = parts[i].Split(',');
                var name = subParts[0];
                var typeName = subParts[1];
                var kind = subParts[2];
                messageParameters.Add(new MessageParameter(name, typeName, kind));
            }
            return messageParameters;
        }

        throw new NotSupportedException($"Cannot deserialize type {type}");
    }

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

    public string Serialize(object value)
    {
        if (value is List<MessageParameter> messageParameters)
        {
            return string.Join("|", messageParameters.Select(mp => $"{mp.Name},{mp.Type},{mp.Kind}"));
        }

        throw new NotSupportedException($"Cannot serialize type {value.GetType()}");
    }
}
