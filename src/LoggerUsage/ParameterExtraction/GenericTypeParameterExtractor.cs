using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;
using LoggerUsage.Utilities;

namespace LoggerUsage.ParameterExtraction;

/// <summary>
/// Extracts parameters from generic type arguments used in LoggerMessage.Define calls.
/// </summary>
internal class GenericTypeParameterExtractor : IParameterExtractor
{
    public bool TryExtractParameters(
        IOperation operation,
        LoggingTypes loggingTypes,
        string? messageTemplate,
        out List<MessageParameter> parameters)
    {
        parameters = [];

        if (operation is not IInvocationOperation invocation ||
            string.IsNullOrEmpty(messageTemplate) ||
            !invocation.TargetMethod.IsGenericMethod)
        {
            return false;
        }

        var formatter = new LogValuesFormatter(messageTemplate);
        var typeArguments = invocation.TargetMethod.TypeArguments;

        for (int i = 0; i < typeArguments.Length && i < formatter.ValueNames.Count; i++)
        {
            parameters.Add(new MessageParameter(
                Name: formatter.ValueNames[i],
                Type: typeArguments[i].ToPrettyDisplayString(),
                Kind: null
            ));
        }

        return parameters.Count > 0;
    }
}
