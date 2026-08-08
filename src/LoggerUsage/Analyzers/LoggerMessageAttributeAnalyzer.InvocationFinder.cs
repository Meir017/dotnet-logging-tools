using Microsoft.CodeAnalysis;
using LoggerUsage.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LoggerUsage.Analyzers
{
    // Partial class containing invocation-finding functionality
    internal partial class LoggerMessageAttributeAnalyzer
    {
        /// <summary>
        /// Phase 2: Find all invocations of a specific LoggerMessage method
        /// </summary>
        private async Task<List<LoggerMessageInvocation>> FindLoggerMessageInvocations(LoggerMessageDeclaration declaration, LoggingAnalysisContext context)
        {
            var invocations = new List<LoggerMessageInvocation>();

            // If we have a solution, use cross-project analysis with SymbolFinder
            if (context.Solution is not null)
            {
                var crossSolutionInvocations = await FindInvocationsAcrossSolution(declaration, context);
                invocations.AddRange(crossSolutionInvocations);

                logger.LogTrace("Found {Count} invocations across solution for {MethodName}",
                    crossSolutionInvocations.Count, declaration.MethodSymbol.Name);

                return invocations;
            }

            // Otherwise, analyze the current syntax tree only
            var localInvocations = FindInvocationsInSyntaxTree(declaration, context);
            invocations.AddRange(localInvocations);

            logger.LogTrace("Found {Count} local invocations for {MethodName}",
                localInvocations.Count, declaration.MethodSymbol.Name);

            return invocations;
        }

        /// <summary>
        /// Finds invocations across the entire solution using SymbolFinder
        /// </summary>
        private async Task<List<LoggerMessageInvocation>> FindInvocationsAcrossSolution(
            LoggerMessageDeclaration declaration,
            LoggingAnalysisContext context)
        {
            var invocations = new List<LoggerMessageInvocation>();
            var searchStart = Stopwatch.GetTimestamp();
            var callers = await SymbolFinder.FindCallersAsync(
                declaration.MethodSymbol,
                context.Solution!,
                context.CancellationToken);
            logger.LogDebug(
                "SymbolFinder returned {CallerCount} callers for {MethodName} in {Duration}ms",
                callers.Count(),
                declaration.MethodSymbol.Name,
                Stopwatch.GetElapsedTime(searchStart).TotalMilliseconds);

            foreach (var caller in callers)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                foreach (var location in caller.Locations)
                {
                    if (location.IsInSource && location.SourceTree != null)
                    {
                        if (context.Solution is not null)
                        {
                            var document = context.Solution.GetDocument(location.SourceTree);
                            if (document is null)
                            {
                                continue;
                            }

                            var syntaxRoot = context.SolutionCache is null
                                ? await document.GetSyntaxRootAsync(context.CancellationToken)
                                : await context.SolutionCache.GetRootAsync(document, context.CancellationToken);
                            if (syntaxRoot is null)
                            {
                                continue;
                            }

                            var invocationExpression = syntaxRoot
                                .FindNode(location.SourceSpan)
                                .FirstAncestorOrSelf<InvocationExpressionSyntax>();
                            if (invocationExpression is null)
                            {
                                continue;
                            }

                            var semanticModel = context.SolutionCache is null
                                ? await document.GetSemanticModelAsync(context.CancellationToken)
                                : await context.SolutionCache.GetSemanticModelAsync(document, context.CancellationToken);
                            if (semanticModel?.GetOperation(invocationExpression, context.CancellationToken) is IInvocationOperation operation)
                            {
                                invocations.Add(CreateLoggerMessageInvocation(operation, invocationExpression, syntaxRoot, semanticModel));
                            }
                        }
                    }
                }
            }

            return invocations;
        }

        /// <summary>
        /// Finds invocations within the current syntax tree
        /// </summary>
        private List<LoggerMessageInvocation> FindInvocationsInSyntaxTree(
            LoggerMessageDeclaration declaration,
            LoggingAnalysisContext context)
        {
            var invocations = new List<LoggerMessageInvocation>();
            foreach (var operation in context.InvocationIndex.Invocations)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (IsLoggerMessageMethodInvocation(operation, declaration))
                {
                    var invocationNode = (InvocationExpressionSyntax)operation.Syntax;
                    var invocation = CreateLoggerMessageInvocation(operation, invocationNode, context.Root, context.SemanticModel);
                    invocations.Add(invocation);

                    logger.LogTrace("Found invocation of {MethodName} in {ContainingType} at line {LineNumber}",
                        declaration.MethodSymbol.Name,
                        invocation.ContainingType,
                        invocation.InvocationLocation.StartLineNumber);
                }
            }

            return invocations;
        }

        /// <summary>
        /// Determines if an invocation operation is calling the specified LoggerMessage method
        /// </summary>
        private static bool IsLoggerMessageMethodInvocation(
            IInvocationOperation operation,
            LoggerMessageDeclaration declaration)
        {
            var targetMethod = operation.TargetMethod;

            // Check if the target method matches our LoggerMessage method
            // This includes both the partial definition and any generated implementation
            if (SymbolEqualityComparer.Default.Equals(targetMethod.OriginalDefinition, declaration.MethodSymbol.OriginalDefinition))
            {
                return true;
            }

            // Check if this is a generated method that matches our signature
            if (IsGeneratedLoggerMessageMethod(targetMethod, declaration))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a method is a generated LoggerMessage method matching the declaration
        /// </summary>
        private static bool IsGeneratedLoggerMessageMethod(
            IMethodSymbol targetMethod,
            LoggerMessageDeclaration declaration)
        {
            // Must have the same name and containing type
            if (targetMethod.Name != declaration.MethodSymbol.Name)
            {
                return false;
            }

            if (!SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, declaration.MethodSymbol.ContainingType))
            {
                return false;
            }

            // Check parameter signatures match
            if (targetMethod.Parameters.Length != declaration.MethodSymbol.Parameters.Length)
            {
                return false;
            }

            for (int i = 0; i < targetMethod.Parameters.Length; i++)
            {
                if (!SymbolEqualityComparer.Default.Equals(
                    targetMethod.Parameters[i].Type,
                    declaration.MethodSymbol.Parameters[i].Type))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Creates a LoggerMessageInvocation from an invocation operation
        /// </summary>
        private static LoggerMessageInvocation CreateLoggerMessageInvocation(
            IInvocationOperation operation,
            InvocationExpressionSyntax invocationSyntax,
            SyntaxNode root,
            SemanticModel semanticModel)
        {
            var containingType = GetContainingTypeName(invocationSyntax, semanticModel);
            var location = LocationHelper.CreateFromInvocation(invocationSyntax);
            var arguments = ExtractInvocationArguments(operation);

            return new LoggerMessageInvocation
            {
                ContainingType = containingType,
                InvocationLocation = location,
                Arguments = arguments
            };
        }

        /// <summary>
        /// Gets the fully qualified name of the type containing the invocation
        /// </summary>
        private static string GetContainingTypeName(InvocationExpressionSyntax invocationSyntax, SemanticModel semanticModel)
        {
            var containingSymbol = semanticModel.GetEnclosingSymbol(invocationSyntax.SpanStart);

            while (containingSymbol != null)
            {
                if (containingSymbol is INamedTypeSymbol typeSymbol)
                {
                    return typeSymbol.ToDisplayString();
                }
                containingSymbol = containingSymbol.ContainingSymbol;
            }

            return "Unknown";
        }

        /// <summary>
        /// Extracts argument information from an invocation operation
        /// </summary>
        private static List<MessageParameter> ExtractInvocationArguments(IInvocationOperation operation)
        {
            var arguments = new List<MessageParameter>();

            foreach (var argument in operation.Arguments)
            {
                var parameterName = argument.Parameter?.Name ?? "Unknown";
                var parameterType = argument.Parameter?.Type?.ToDisplayString() ?? "Unknown";

                // For now, we'll create a simple representation
                // This can be enhanced later to analyze the actual argument expressions
                arguments.Add(new MessageParameter(parameterName, parameterType, "InvocationArgument"));
            }

            return arguments;
        }
    }
}
