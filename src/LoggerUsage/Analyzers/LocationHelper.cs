using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using LoggerUsage.Models;

namespace LoggerUsage.Analyzers
{
    internal static class LocationHelper
    {
        public static MethodCallLocation CreateFromSyntaxNode(SyntaxNode syntaxNode)
        {
            var location = syntaxNode.GetLocation();
            var lineSpan = location.GetLineSpan();
            
            return new MethodCallLocation
            {
                StartLineNumber = lineSpan.StartLinePosition.Line,
                EndLineNumber = lineSpan.EndLinePosition.Line,
                FilePath = location.SourceTree?.FilePath ?? string.Empty
            };
        }

        public static MethodCallLocation CreateFromInvocation(InvocationExpressionSyntax invocation)
        {
            return CreateFromSyntaxNode(invocation);
        }

        public static MethodCallLocation CreateFromMethodDeclaration(MethodDeclarationSyntax methodDeclaration, SyntaxNode root)
        {
            var location = methodDeclaration.GetLocation();
            var lineSpan = location.GetLineSpan();
            
            return new MethodCallLocation
            {
                StartLineNumber = lineSpan.StartLinePosition.Line,
                EndLineNumber = lineSpan.EndLinePosition.Line,
                FilePath = root.SyntaxTree.FilePath ?? string.Empty
            };
        }
    }
}
