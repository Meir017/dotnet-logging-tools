using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;

namespace LoggerUsage.Factories;

/// <summary>
/// Factory interface for creating MessageParameter instances.
/// </summary>
internal interface IMessageParameterFactory
{
    /// <summary>
    /// Creates a MessageParameter from operation context.
    /// </summary>
    /// <param name="name">The parameter name</param>
    /// <param name="type">The parameter type symbol</param>
    /// <param name="operation">The operation providing the parameter value</param>
    /// <returns>A new MessageParameter instance</returns>
    MessageParameter Create(string name, ITypeSymbol? type, IOperation operation);
    
    /// <summary>
    /// Creates a MessageParameter from basic information.
    /// </summary>
    /// <param name="name">The parameter name</param>
    /// <param name="typeName">The parameter type name</param>
    /// <param name="kind">The parameter kind</param>
    /// <returns>A new MessageParameter instance</returns>
    MessageParameter Create(string name, string typeName, string? kind);
}
