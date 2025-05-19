using Microsoft.CodeAnalysis;

namespace LoggerUsage;

public static class SymbolExtensions
{
    private static readonly SymbolDisplayFormat _symbolDisplayFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static string ToPrettyDisplayString(this ITypeSymbol typeSymbol) => typeSymbol.ToDisplayString(_symbolDisplayFormat);
}