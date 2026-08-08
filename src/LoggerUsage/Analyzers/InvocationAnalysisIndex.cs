using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace LoggerUsage.Analyzers;

internal sealed class InvocationAnalysisIndex
{
    private InvocationAnalysisIndex(
        IReadOnlyList<IInvocationOperation> invocations,
        int candidateCount)
    {
        Invocations = invocations;
        CandidateCount = candidateCount;
    }

    public IReadOnlyList<IInvocationOperation> Invocations { get; }

    public int CandidateCount { get; }

    public int OperationBindCount => CandidateCount;

    public static InvocationAnalysisIndex Create(
        SyntaxNode root,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var invocations = new List<IInvocationOperation>();
        var candidateCount = 0;

        foreach (var invocationSyntax in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            candidateCount++;

            if (semanticModel.GetOperation(invocationSyntax, cancellationToken) is IInvocationOperation invocation)
            {
                invocations.Add(invocation);
            }
        }

        return new InvocationAnalysisIndex(invocations, candidateCount);
    }
}
