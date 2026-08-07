using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;

namespace LoggerUsage.Analyzers;

internal sealed class SolutionAnalysisCache
{
    private readonly ConcurrentDictionary<DocumentId, Lazy<Task<SyntaxNode?>>> _roots = new();
    private readonly ConcurrentDictionary<DocumentId, Lazy<Task<SemanticModel?>>> _semanticModels = new();

    public Task<SyntaxNode?> GetRootAsync(Document document, CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            _roots,
            document.Id,
            () => document.GetSyntaxRootAsync(cancellationToken));

    public Task<SemanticModel?> GetSemanticModelAsync(
        Document document,
        CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            _semanticModels,
            document.Id,
            () => document.GetSemanticModelAsync(cancellationToken));

    private static async Task<T?> GetOrCreateAsync<T>(
        ConcurrentDictionary<DocumentId, Lazy<Task<T?>>> cache,
        DocumentId documentId,
        Func<Task<T?>> factory)
        where T : class
    {
        var value = cache.GetOrAdd(
            documentId,
            _ => new Lazy<Task<T?>>(factory, LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await value.Value;
        }
        catch
        {
            cache.TryRemove(new KeyValuePair<DocumentId, Lazy<Task<T?>>>(documentId, value));
            throw;
        }
    }
}
