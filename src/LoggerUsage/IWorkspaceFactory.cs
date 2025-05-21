using Microsoft.CodeAnalysis;

namespace LoggerUsage;

public interface IWorkspaceFactory
{
    /// <summary>
    /// Creates a workspace for the specified path.
    /// </summary>
    /// <param name="fileInfo">The file information for the solution or project file.</param>
    /// <returns>The created workspace.</returns>
    Task<Workspace> Create(FileInfo fileInfo);
}