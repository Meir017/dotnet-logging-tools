using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;

namespace LoggerUsage.ParameterExtraction;

/// <summary>
/// Extracts parameters from anonymous object creation operations.
/// </summary>
internal class AnonymousObjectParameterExtractor : IParameterExtractor
{
    public bool TryExtractParameters(
        IOperation operation, 
        LoggingTypes loggingTypes, 
        string? messageTemplate, 
        out List<MessageParameter> parameters)
    {
        parameters = new List<MessageParameter>();

        if (operation is not IAnonymousObjectCreationOperation objectCreation)
        {
            return false;
        }

        foreach (var property in objectCreation.Initializers)
        {
            if (property is not ISimpleAssignmentOperation assignment)
                continue;

            var propertyName = GetPropertyName(assignment.Target.Syntax);
            if (propertyName == null)
                continue;

            var parameter = new MessageParameter(
                Name: propertyName,
                Type: assignment.Value.Type?.ToPrettyDisplayString() ?? "object",
                Kind: assignment.Value.ConstantValue.HasValue ? "Constant" : assignment.Value.Kind.ToString()
            );

            parameters.Add(parameter);
        }

        return parameters.Count > 0;
    }

    private static string? GetPropertyName(SyntaxNode syntax)
    {
        return syntax switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null
        };
    }
}
