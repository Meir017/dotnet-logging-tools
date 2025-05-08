using Microsoft.CodeAnalysis;

namespace LoggerUsage
{
    /// <summary>
    /// Models all extension methods from LoggerExtensions and provides mapping to IMethodSymbol.
    /// </summary>
    internal class LoggerExtensionModeler
    {
        private readonly Dictionary<string, List<IMethodSymbol>> _extensionMethodsByName = new();
        private readonly Dictionary<string, IMethodSymbol> _loggerMethodsByName;

        public LoggerExtensionModeler(LoggingTypes types)
        {
            foreach (var method in types.LoggerExtensions.GetMembers().OfType<IMethodSymbol>())
            {
                if (!method.IsExtensionMethod)
                    continue;
                if (!_extensionMethodsByName.TryGetValue(method.Name, out var list))
                {
                    list = new List<IMethodSymbol>();
                    _extensionMethodsByName[method.Name] = list;
                }
                list.Add(method);
            }

            _loggerMethodsByName = types.ILogger.GetMembers().OfType<IMethodSymbol>()
                .ToDictionary(m => m.Name, m => m);
        }

        internal bool IsLoggerMethod(IMethodSymbol method)
        {
            if (_loggerMethodsByName.TryGetValue(method.Name, out var loggerMethod)
                && SymbolEqualityComparer.Default.Equals(method, loggerMethod))
            {
                return true;
            }

            if (_extensionMethodsByName.TryGetValue(method.Name, out var extensionMethods))
            {
                foreach (var extensionMethod in extensionMethods)
                {
                    if (SymbolEqualityComparer.Default.Equals(method, extensionMethod))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
