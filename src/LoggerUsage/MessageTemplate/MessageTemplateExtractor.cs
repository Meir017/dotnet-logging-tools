using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace LoggerUsage.MessageTemplate;

/// <summary>
/// Implementation for extracting message templates from operations.
/// </summary>
internal class MessageTemplateExtractor : IMessageTemplateExtractor
{
    public bool TryExtract(IArgumentOperation argument, out string template)
    {
        return TryExtract(argument.Value, out template);
    }

    public bool TryExtract(IOperation operation, out string template)
    {
        template = string.Empty;

        // Handle literal string values
        if (operation is ILiteralOperation literal && 
            literal.Type?.SpecialType == SpecialType.System_String &&
            literal.ConstantValue.HasValue)
        {
            template = literal.ConstantValue.Value?.ToString() ?? string.Empty;
            return !string.IsNullOrEmpty(template);
        }

        // Handle other constant string operations
        if (operation.Type?.SpecialType == SpecialType.System_String && 
            operation.ConstantValue.HasValue)
        {
            template = operation.ConstantValue.Value?.ToString() ?? string.Empty;
            return !string.IsNullOrEmpty(template);
        }

        return false;
    }
}
