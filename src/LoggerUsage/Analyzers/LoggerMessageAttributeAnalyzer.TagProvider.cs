using Microsoft.CodeAnalysis;
using LoggerUsage.Models;

namespace LoggerUsage.Analyzers
{
    // Partial class containing TagProvider-related functionality
    internal partial class LoggerMessageAttributeAnalyzer
    {
        /// <summary>
        /// Extracts TagProvider information from the attribute on a parameter
        /// </summary>
        private static TagProviderInfo ExtractTagProviderInfo(
            IParameterSymbol parameter,
            AttributeData tagProviderAttribute,
            LoggingTypes loggingTypes)
        {
            ITypeSymbol? providerType = null;
            string? providerMethodName = null;
            bool omitReferenceName = false;

            // Extract constructor arguments
            // [TagProvider(typeof(ProviderType), "MethodName")]
            if (tagProviderAttribute.ConstructorArguments.Length >= 2)
            {
                // First argument is the provider type
                if (tagProviderAttribute.ConstructorArguments[0].Value is ITypeSymbol typeArg)
                {
                    providerType = typeArg;
                }

                // Second argument is the method name
                if (tagProviderAttribute.ConstructorArguments[1].Value is string methodName)
                {
                    providerMethodName = methodName;
                }
            }

            // Extract named arguments (properties of the attribute)
            foreach (var namedArg in tagProviderAttribute.NamedArguments)
            {
                if (namedArg.Key == "OmitReferenceName" && namedArg.Value.Value is bool omitValue)
                {
                    omitReferenceName = omitValue;
                }
            }

            // Validate the provider configuration
            var (isValid, validationMessage) = ValidateTagProvider(
                providerType,
                providerMethodName,
                parameter.Type,
                loggingTypes);

            return new TagProviderInfo(
                parameter.Name,
                providerType?.ToDisplayString() ?? "Unknown",
                providerMethodName ?? "Unknown",
                omitReferenceName,
                isValid,
                validationMessage);
        }

        /// <summary>
        /// Validates a TagProvider configuration against expected method signature and accessibility rules
        /// </summary>
        private static (bool IsValid, string? ValidationMessage) ValidateTagProvider(
            ITypeSymbol? providerType,
            string? providerMethodName,
            ITypeSymbol parameterType,
            LoggingTypes loggingTypes)
        {
            // Check if provider type exists
            if (providerType == null)
            {
                return (false, "Provider type not found");
            }

            // Check if method name is provided
            if (string.IsNullOrEmpty(providerMethodName))
            {
                return (false, "Provider method name is empty");
            }

            // Find the method on the provider type
            var method = providerType.GetMembers(providerMethodName)
                .OfType<IMethodSymbol>()
                .FirstOrDefault();

            if (method == null)
            {
                return (false, $"Method '{providerMethodName}' not found on type '{providerType.ToDisplayString()}'");
            }

            // Check if method is static
            if (!method.IsStatic)
            {
                return (false, $"Method '{providerMethodName}' must be static");
            }

            // Check if method is accessible (public or internal)
            if (method.DeclaredAccessibility != Accessibility.Public &&
                method.DeclaredAccessibility != Accessibility.Internal)
            {
                return (false, $"Method '{providerMethodName}' must be public or internal");
            }

            // Check if method returns void
            if (method.ReturnsVoid == false)
            {
                return (false, $"Method '{providerMethodName}' must return void");
            }

            // Check if method has exactly 2 parameters
            if (method.Parameters.Length != 2)
            {
                return (false, $"Method '{providerMethodName}' must have exactly 2 parameters");
            }

            // Check if first parameter is ITagCollector
            if (loggingTypes.ITagCollector == null)
            {
                // ITagCollector not available, cannot validate
                return (true, null);
            }

            var firstParam = method.Parameters[0];
            if (!SymbolEqualityComparer.Default.Equals(firstParam.Type, loggingTypes.ITagCollector))
            {
                return (false, $"First parameter must be ITagCollector, but was '{firstParam.Type.ToDisplayString()}'");
            }

            // Check if second parameter matches the parameter type
            var secondParam = method.Parameters[1];
            if (!SymbolEqualityComparer.Default.Equals(secondParam.Type, parameterType))
            {
                return (false, $"Second parameter must be '{parameterType.ToDisplayString()}', but was '{secondParam.Type.ToDisplayString()}'");
            }

            return (true, null);
        }
    }
}
