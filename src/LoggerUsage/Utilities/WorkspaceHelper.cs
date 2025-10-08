using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Utilities;

/// <summary>
/// Utility class for AdhocWorkspace creation and management.
/// </summary>
internal static class WorkspaceHelper
{
    /// <summary>
    /// Ensures a valid Solution is available, creating an AdhocWorkspace if necessary.
    /// </summary>
    /// <param name="compilation">The compilation to analyze.</param>
    /// <param name="solution">Optional existing solution. If null, an AdhocWorkspace will be created.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    /// <returns>A tuple containing the solution and an optional disposable workspace.</returns>
    public static async Task<(Solution?, IDisposable?)> EnsureSolutionAsync(
        Compilation compilation,
        Solution? solution,
        ILogger logger)
    {
        if (solution != null)
        {
            logger.LogDebug("Using provided solution for analysis");
            return (solution, null);
        }

        logger.LogInformation("Creating AdhocWorkspace for compilation '{AssemblyName}'", compilation.AssemblyName);

        try
        {
            var workspace = new AdhocWorkspace();

            var projectInfo = ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Default,
                name: compilation.AssemblyName ?? "Project",
                assemblyName: compilation.AssemblyName ?? "Project",
                language: LanguageNames.CSharp,
                compilationOptions: compilation.Options,
                metadataReferences: compilation.References);

            var project = workspace.AddProject(projectInfo);

            // Add syntax trees as documents
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var fileName = string.IsNullOrEmpty(syntaxTree.FilePath)
                    ? $"Document{project.Documents.Count()}.cs"
                    : Path.GetFileName(syntaxTree.FilePath);

                var text = await syntaxTree.GetTextAsync();

                // Add document with text content
                project = project.AddDocument(fileName, text, filePath: syntaxTree.FilePath).Project;
            }

            // Apply all changes to workspace
            if (!workspace.TryApplyChanges(project.Solution))
            {
                logger.LogWarning("Failed to apply changes to AdhocWorkspace for compilation '{AssemblyName}'", compilation.AssemblyName);
            }

            var finalSolution = workspace.CurrentSolution;
            var finalProject = finalSolution.GetProject(project.Id);

            // IMPORTANT: Force compilation to be created in the workspace
            // This ensures SymbolFinder can properly resolve symbols across documents
            if (finalProject != null)
            {
                var workspaceCompilation = await finalProject.GetCompilationAsync();
                logger.LogInformation("AdhocWorkspace created successfully with {DocumentCount} documents, compilation has {TreeCount} trees",
                    finalProject.Documents.Count(),
                    workspaceCompilation?.SyntaxTrees.Count() ?? 0);
            }

            return (finalSolution, workspace);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create AdhocWorkspace for compilation '{AssemblyName}'. " +
                "Solution APIs will not be available.", compilation.AssemblyName);
            return (null, null);
        }
    }
}
