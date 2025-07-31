using LoggerUsage.Models;
using Microsoft.CodeAnalysis;

namespace LoggerUsage.Utilities
{
    /// <summary>
    /// Utility class for creating MessageParameter instances with consistent formatting.
    /// </summary>
    internal static class MessageParameterFactory
    {

        /// <summary>
        /// Creates a MessageParameter from an IOperation with automatic type and kind resolution.
        /// </summary>
        /// <param name="name">The parameter name</param>
        /// <param name="operation">The operation to extract type and kind from</param>
        /// <returns>A new MessageParameter instance</returns>
        public static MessageParameter CreateFromOperation(string name, IOperation operation)
        {
            var unwrapped = operation.UnwrapConversion();
            return new MessageParameter(
                Name: name,
                Type: GetDisplayString(unwrapped.Type),
                Kind: GetKindString(unwrapped)
            );
        }

        /// <summary>
        /// Creates a MessageParameter from a reference (local or field) with angle bracket formatting.
        /// </summary>
        /// <param name="name">The reference name (will be wrapped in angle brackets)</param>
        /// <param name="type">The type symbol</param>
        /// <param name="kind">The operation kind</param>
        /// <returns>A new MessageParameter instance</returns>
        public static MessageParameter CreateFromReference(string name, ITypeSymbol type, OperationKind kind)
        {
            return new MessageParameter(
                Name: $"<{name}>",
                Type: GetDisplayString(type),
                Kind: kind.ToString()
            );
        }

        /// <summary>
        /// Creates a MessageParameter from key-value arguments with automatic type and kind resolution.
        /// </summary>
        /// <param name="key">The parameter key</param>
        /// <param name="valueOperation">The value operation to extract type and kind from</param>
        /// <returns>A new MessageParameter instance</returns>
        public static MessageParameter CreateFromKeyValue(string key, IOperation valueOperation)
        {
            var unwrapped = valueOperation.UnwrapConversion();
            return new MessageParameter(
                Name: key,
                Type: GetDisplayString(unwrapped.Type),
                Kind: GetKindString(unwrapped)
            );
        }

        private static string GetDisplayString(ITypeSymbol? type)
        {
            return type?.ToPrettyDisplayString() ?? "object";
        }

        private static string GetKindString(IOperation operation)
        {
            return operation.ConstantValue.HasValue ? "Constant" : operation.Kind.ToString();
        }
    }
}
