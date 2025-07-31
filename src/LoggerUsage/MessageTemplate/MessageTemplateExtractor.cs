using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace LoggerUsage.MessageTemplate;

/// <summary>
/// Implementation for extracting message templates from operations.
/// </summary>
internal class MessageTemplateExtractor : IMessageTemplateExtractor
{
    public bool TryExtract(IOperation operation, out string? template)
    {
        // Handle literal string values
        if (operation is ILiteralOperation literal && 
            literal.Type?.SpecialType == SpecialType.System_String &&
            literal.ConstantValue.HasValue)
        {
            template = literal.ConstantValue.Value?.ToString();
            return true;
        }

        // Handle other constant string operations
        if (operation.Type?.SpecialType == SpecialType.System_String && 
            operation.ConstantValue.HasValue)
        {
            template = operation.ConstantValue.Value?.ToString();
            return true;
        }

        template = null;
        return false;
    }
}
