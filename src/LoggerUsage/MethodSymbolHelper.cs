using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LoggerUsage
{
    public static class MethodSymbolHelper
    {
        public static (IMethodSymbol? method, Optional<object?>[] constantValues) GetMethodSymbol(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(invocation);

            if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
            {
                return (methodSymbol, GetConstantValues());
            }

            // Overload resolution failed, fall back to candidate analysis
            if (symbolInfo.CandidateReason == CandidateReason.OverloadResolutionFailure)
            {
                var candidates = symbolInfo.CandidateSymbols.OfType<IMethodSymbol>();

                // Get the argument types from the syntax
                var arguments = invocation.ArgumentList.Arguments
                    .Select(arg => semanticModel.GetOperation(arg.Expression))
                    .ToArray();

                var argumentTypes = arguments.Select(arg => arg?.Type).ToArray();

                foreach (var candidate in candidates)
                {
                    if (candidate.IsExtensionMethod && IsMatch(candidate.ReducedFrom!, argumentTypes, isExtensionMethod: true))
                    {
                        return (candidate.ReducedFrom, GetConstantValues());
                    }
                    else if (IsMatch(candidate, argumentTypes, isExtensionMethod: false))
                    {
                        return (candidate, GetConstantValues());
                    }
                }
            }

            return (null, []);

            Optional<object?>[] GetConstantValues()
            {
                return [.. invocation.ArgumentList.Arguments.Select(arg => semanticModel.GetConstantValue(arg.Expression))];
            }
        }

        private static bool IsMatch(IMethodSymbol method, ITypeSymbol?[] argumentTypes, bool isExtensionMethod)
        {
            var parameters = method.Parameters;
            if (isExtensionMethod)
            {
                // Skip the first parameter for extension methods
                parameters = [.. parameters.Skip(1)];
            }

            // Handle optional/params later; start with minimum match
            int fixedParamCount = parameters.Count(p => !p.IsParams);

            if (argumentTypes.Length < fixedParamCount)
                return false;

            for (int i = 0; i < fixedParamCount; i++)
            {
                var argType = argumentTypes[i];
                var paramType = parameters[i].Type;

                if (!IsAssignable(argType, paramType))
                    return false;
            }

            // Handle params
            if (parameters.LastOrDefault()?.IsParams == true)
            {
                var paramType = ((IArrayTypeSymbol)parameters.Last().Type).ElementType;
                for (int i = fixedParamCount; i < argumentTypes.Length; i++)
                {
                    if (!IsAssignable(argumentTypes[i], paramType))
                        return false;
                }

                return true;
            }

            return argumentTypes.Length == parameters.Length;
        }

        private static bool IsAssignable(ITypeSymbol? from, ITypeSymbol to)
        {
            // This is a simplistic check; you might want to include conversion classification
            if (from == null || to == null)
                return false;

            if (to.SpecialType is SpecialType.System_Object)
            {
                return true; // Any type can be assigned to object
            }

            if (SymbolEqualityComparer.Default.Equals(from, to))
            {
                return true;
            }

            if (from.AllInterfaces.Contains(to, SymbolEqualityComparer.Default))
            {
                return true; // Handle interfaces
            }

            return to.GetMembers().OfType<IMethodSymbol>().Any(x 
                => x.IsStatic && x.Name is "op_Implicit"
                && SymbolEqualityComparer.Default.Equals(x.ReturnType, to)
                && x.Parameters is { Length: 1 }
                && x.Parameters[0].Type is { } paramType && IsAssignable(from, paramType));
        }
    }
}
