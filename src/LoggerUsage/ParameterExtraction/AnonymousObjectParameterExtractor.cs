using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;
using LoggerUsage.Utilities;

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
            if (property is not ISimpleAssignmentOperation assignment
            || assignment.Target is not IPropertyReferenceOperation propertyRef)
                continue;

            var parameter = MessageParameterFactory.CreateFromOperation(
                propertyRef.Member.Name,
                assignment.Value
            );

            parameters.Add(parameter);
        }

        return parameters.Count > 0;
    }
}
