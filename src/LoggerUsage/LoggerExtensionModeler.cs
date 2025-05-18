using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace LoggerUsage
{
    /// <summary>
    /// Models all extension methods from LoggerExtensions and provides mapping to IMethodSymbol.
    /// </summary>
    internal class LoggerExtensionModeler
    {
        private readonly Dictionary<string, List<IMethodSymbol>> _extensionMethodsByName = new();
        private readonly IMethodSymbol _loggerIsEnabledMethod;
        private readonly IMethodSymbol _loggerLogMethod;


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

            _loggerIsEnabledMethod = types.ILogger.GetMembers().OfType<IMethodSymbol>()
                .First(m => m.Name == nameof(ILogger.IsEnabled));
            _loggerLogMethod = types.ILogger.GetMembers().OfType<IMethodSymbol>()
                .First(m => m.Name == nameof(ILogger.Log));
        }

        internal bool IsLoggerMethod(IMethodSymbol method)
        {
            if (SymbolEqualityComparer.Default.Equals(method, _loggerIsEnabledMethod))
            {
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(method, _loggerLogMethod))
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
