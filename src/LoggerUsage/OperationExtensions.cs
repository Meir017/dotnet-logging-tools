using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace LoggerUsage;

internal static class OperationExtensions
{
    public static IOperation UnwrapConversion(this IOperation operation)
    {
        return operation is IConversionOperation conversion
            ? UnwrapConversion(conversion.Operand)
            : operation;
    }
}