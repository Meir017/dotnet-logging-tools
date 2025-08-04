using Microsoft.CodeAnalysis;

namespace LoggerUsage.Utilities;

internal static class SymbolExtensions
{
    private static readonly SymbolDisplayFormat _symbolDisplayFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static string ToPrettyDisplayString(this ITypeSymbol typeSymbol) => typeSymbol.ToDisplayString(_symbolDisplayFormat);

    internal static bool IsLoggerInterface(this ITypeSymbol typeSymbol, LoggingTypes loggingTypes)
    {
        if (loggingTypes.ILogger.Equals(typeSymbol, SymbolEqualityComparer.Default))
        {
            return true;
        }

        if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
        {
            return namedTypeSymbol.AllInterfaces.Any(i => i.Equals(loggingTypes.ILogger, SymbolEqualityComparer.Default));
        }

        return false;
    }

    internal static bool IsException(this ITypeSymbol typeSymbol, LoggingTypes loggingTypes)
    {
        if (loggingTypes.Exception.Equals(typeSymbol, SymbolEqualityComparer.Default))
        {
            return true;
        }

        if (typeSymbol.BaseType is not null)
        {
            return IsException(typeSymbol.BaseType, loggingTypes);
        }

        return false;
    }
}