using LoggerUsage.Models;

namespace LoggerUsage.Utilities
{
    /// <summary>
    /// Utility class for creating MessageParameter instances with consistent formatting.
    /// </summary>
    internal static class MessageParameterFactory
    {
        /// <summary>
        /// Creates a MessageParameter with consistent formatting.
        /// </summary>
        /// <param name="name">The parameter name</param>
        /// <param name="type">The parameter type</param>
        /// <param name="kind">The parameter kind</param>
        /// <returns>A new MessageParameter instance</returns>
        public static MessageParameter CreateMessageParameter(string name, string type, string kind)
        {
            return new MessageParameter(
                Name: name,
                Type: type ?? "object",
                Kind: kind
            );
        }
    }
}
