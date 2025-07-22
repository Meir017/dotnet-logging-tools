using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using LoggerUsage.Models;

namespace LoggerUsage.MessageTemplate;

/// <summary>
/// Enhanced message template extractor with comprehensive error handling and diagnostics.
/// </summary>
internal class EnhancedMessageTemplateExtractor : IEnhancedMessageTemplateExtractor
{
    private readonly ILogger<EnhancedMessageTemplateExtractor> _logger;

    public EnhancedMessageTemplateExtractor(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<EnhancedMessageTemplateExtractor>();
    }

    public bool TryExtract(IArgumentOperation argument, out string template)
    {
        return TryExtract(argument.Value, out template);
    }

    public bool TryExtract(IOperation operation, out string template)
    {
        var result = ExtractWithResult(operation);
        template = result.Value ?? string.Empty;
        return result.IsSuccess;
    }

    /// <summary>
    /// Extracts message template with detailed error reporting from an argument operation.
    /// </summary>
    /// <param name="argument">The argument operation to extract from</param>
    /// <returns>Extraction result with success/failure information</returns>
    public ExtractionResult<string> ExtractWithResult(IArgumentOperation argument)
    {
        return ExtractWithResult(argument.Value);
    }

    /// <summary>
    /// Extracts message template with detailed error reporting.
    /// </summary>
    /// <param name="operation">The operation to extract from</param>
    /// <returns>Extraction result with success/failure information</returns>
    public ExtractionResult<string> ExtractWithResult(IOperation operation)
    {
        try
        {
            if (operation == null)
            {
                _logger.LogWarning("Cannot extract template from null operation");
                return ExtractionResult<string>.Failure("Operation is null");
            }

            _logger.LogDebug("Attempting to extract message template from {OperationType}", operation.GetType().Name);

            // Handle literal string values
            if (operation is ILiteralOperation literal)
            {
                return ExtractFromLiteral(literal);
            }

            // Handle other constant string operations
            if (operation.Type?.SpecialType == SpecialType.System_String && operation.ConstantValue.HasValue)
            {
                return ExtractFromConstant(operation);
            }

            // Handle field references (e.g., const fields)
            if (operation is IFieldReferenceOperation fieldRef)
            {
                return ExtractFromFieldReference(fieldRef);
            }

            // Handle property references (e.g., string properties)
            if (operation is IPropertyReferenceOperation propertyRef)
            {
                return ExtractFromPropertyReference(propertyRef);
            }

            // Handle local references (e.g., string variables)
            if (operation is ILocalReferenceOperation localRef)
            {
                return ExtractFromLocalReference(localRef);
            }

            // Handle conversions (unwrap and try again)
            if (operation is IConversionOperation conversion)
            {
                _logger.LogDebug("Unwrapping conversion operation and retrying");
                return ExtractWithResult(conversion.Operand);
            }

            _logger.LogDebug("No template extraction strategy found for {OperationType}", operation.GetType().Name);
            return ExtractionResult<string>.Failure($"Unsupported operation type: {operation.GetType().Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract message template from {OperationType}", operation.GetType().Name);
            return ExtractionResult<string>.Failure($"Exception during template extraction: {ex.Message}", ex);
        }
    }

    private ExtractionResult<string> ExtractFromLiteral(ILiteralOperation literal)
    {
        if (literal.Type?.SpecialType != SpecialType.System_String)
        {
            _logger.LogDebug("Literal operation is not a string type: {Type}", literal.Type?.ToDisplayString());
            return ExtractionResult<string>.Failure($"Literal is not a string type: {literal.Type?.ToDisplayString()}");
        }

        if (!literal.ConstantValue.HasValue)
        {
            _logger.LogDebug("Literal operation has no constant value");
            return ExtractionResult<string>.Failure("Literal has no constant value");
        }

        var template = literal.ConstantValue.Value?.ToString();
        if (string.IsNullOrEmpty(template))
        {
            _logger.LogDebug("Extracted template is null or empty");
            return ExtractionResult<string>.Failure("Template is null or empty");
        }

        _logger.LogDebug("Successfully extracted template from literal: {Template}", template);
        return ExtractionResult<string>.Success(template);
    }

    private ExtractionResult<string> ExtractFromConstant(IOperation operation)
    {
        var template = operation.ConstantValue.Value?.ToString();
        if (string.IsNullOrEmpty(template))
        {
            _logger.LogDebug("Constant operation has null or empty value");
            return ExtractionResult<string>.Failure("Constant value is null or empty");
        }

        _logger.LogDebug("Successfully extracted template from constant: {Template}", template);
        return ExtractionResult<string>.Success(template);
    }

    private ExtractionResult<string> ExtractFromFieldReference(IFieldReferenceOperation fieldRef)
    {
        if (fieldRef.Field.IsConst && fieldRef.Field.ConstantValue != null)
        {
            var template = fieldRef.Field.ConstantValue.ToString();
            if (!string.IsNullOrEmpty(template))
            {
                _logger.LogDebug("Successfully extracted template from const field {FieldName}: {Template}", 
                    fieldRef.Field.Name, template);
                return ExtractionResult<string>.Success(template);
            }
        }

        _logger.LogDebug("Field reference {FieldName} is not a const string or has empty value", fieldRef.Field.Name);
        return ExtractionResult<string>.Failure($"Field '{fieldRef.Field.Name}' is not a constant string");
    }

    private ExtractionResult<string> ExtractFromPropertyReference(IPropertyReferenceOperation propertyRef)
    {
        _logger.LogDebug("Property reference {PropertyName} cannot be extracted at compile time", propertyRef.Property.Name);
        return ExtractionResult<string>.Failure($"Property '{propertyRef.Property.Name}' value cannot be determined at compile time");
    }

    private ExtractionResult<string> ExtractFromLocalReference(ILocalReferenceOperation localRef)
    {
        _logger.LogDebug("Local reference {LocalName} cannot be extracted at compile time", localRef.Local.Name);
        return ExtractionResult<string>.Failure($"Local variable '{localRef.Local.Name}' value cannot be determined at compile time");
    }
}
