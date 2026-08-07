using Microsoft.CodeAnalysis;

namespace LoggerUsage;

/// <summary>
/// Factory interface for creating workspaces.
/// </summary>
public interface IWorkspaceFactory
{
    /// <summary>
    /// Creates a workspace for the specified path.
    /// </summary>
    /// <param name="fileInfo">The file information for the solution or project file.</param>
    /// <returns>The created workspace.</returns>
    Task<Workspace> Create(FileInfo fileInfo);

    /// <summary>
    /// Creates a workspace for the specified path.
    /// </summary>
    /// <param name="fileInfo">The file information for the solution or project file.</param>
    /// <param name="cancellationToken">Token used to cancel workspace loading.</param>
    /// <returns>The created workspace.</returns>
    Task<Workspace> Create(FileInfo fileInfo, CancellationToken cancellationToken) => Create(fileInfo);
}