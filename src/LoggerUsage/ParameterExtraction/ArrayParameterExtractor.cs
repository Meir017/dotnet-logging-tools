using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using LoggerUsage.Models;
using LoggerUsage.Utilities;

namespace LoggerUsage.ParameterExtraction;

/// <summary>
/// Extracts parameters from array arguments used in logger methods.
/// </summary>
internal class ArrayParameterExtractor : IParameterExtractor
{
    public bool TryExtractParameters(
        IOperation operation, 
        LoggingTypes loggingTypes, 
        string? messageTemplate, 
        out List<MessageParameter> parameters)
    {
        parameters = new List<MessageParameter>();

        if (operation is not IInvocationOperation invocation || string.IsNullOrEmpty(messageTemplate))
        {
            return false;
        }

        int parameterStartIndex = invocation.TargetMethod.IsExtensionMethod ? 1 : 0;

        for (int i = parameterStartIndex; i < invocation.TargetMethod.Parameters.Length && i < invocation.Arguments.Length; i++)
        {
            var param = invocation.TargetMethod.Parameters[i];
            var arg = invocation.Arguments[i].Value;

            if (!loggingTypes.ObjectNullableArray.Equals(param.Type, SymbolEqualityComparer.Default))
            {
                continue;
            }

            if (arg is not IArrayCreationOperation arrayCreation)
            {
                continue;
            }

            var formatter = new LogValuesFormatter(messageTemplate);

            foreach (var element in arrayCreation.Initializer?.ElementValues ?? [])
            {
                var paramValue = element.UnwrapConversion();

                parameters.Add(MessageParameterFactory.CreateFromOperation(
                    formatter.ValueNames[parameters.Count],
                    paramValue
                ));
            }
        }

        return parameters.Count > 0;
    }
}
