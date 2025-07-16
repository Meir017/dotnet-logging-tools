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
        private readonly IMethodSymbol _loggerBeginScopeMethod;


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
            _loggerBeginScopeMethod = types.ILogger.GetMembers().OfType<IMethodSymbol>()
                .First(m => m.Name == nameof(ILogger.BeginScope));
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

            // Exclude BeginScope methods as they are handled by BeginScopeAnalyzer
            if (IsBeginScopeMethod(method))
            {
                return false;
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

        internal bool IsBeginScopeMethod(IMethodSymbol method)
        {
            // Check if this is a BeginScope method by name
            if (method.Name != nameof(ILogger.BeginScope))
                return false;

            // Check if this is the core ILogger.BeginScope method
            if (SymbolEqualityComparer.Default.Equals(method, _loggerBeginScopeMethod))
            {
                return true;
            }

            // Check if the method is defined on an ILogger interface (handles ILogger<T> case)
            if (method.ContainingType != null && 
                (SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, _loggerBeginScopeMethod.ContainingType) ||
                 method.ContainingType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, _loggerBeginScopeMethod.ContainingType))))
            {
                return true;
            }

            // Check if this is a BeginScope extension method
            if (_extensionMethodsByName.TryGetValue(nameof(ILogger.BeginScope), out var extensionMethods))
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
