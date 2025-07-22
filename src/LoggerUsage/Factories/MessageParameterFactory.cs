using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;

namespace LoggerUsage.Factories;

/// <summary>
/// Factory implementation for creating MessageParameter instances.
/// </summary>
internal class MessageParameterFactory : IMessageParameterFactory
{
    public MessageParameter Create(string name, ITypeSymbol? type, IOperation operation)
    {
        var typeName = type?.ToPrettyDisplayString() ?? "object";
        var kind = operation.ConstantValue.HasValue ? "Constant" : operation.Kind.ToString();
        
        return new MessageParameter(
            Name: name,
            Type: typeName,
            Kind: kind
        );
    }

    public MessageParameter Create(string name, string typeName, string? kind)
    {
        return new MessageParameter(
            Name: name,
            Type: typeName ?? "object",
            Kind: kind
        );
    }
}
