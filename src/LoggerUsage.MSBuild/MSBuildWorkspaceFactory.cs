using System.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.Build.Locator;

namespace LoggerUsage.MSBuild;

public partial class MSBuildWorkspaceFactory : IWorkspaceFactory
{
    private static bool _msBuildLocatorRegistered;

    private readonly ILogger<MSBuildWorkspaceFactory> _logger;

    public MSBuildWorkspaceFactory(ILogger<MSBuildWorkspaceFactory> logger)
    {
        _logger = logger;

        if (Interlocked.CompareExchange(ref _msBuildLocatorRegistered, true, false) == false)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }

    public async Task<Workspace> Create(FileInfo fileInfo)
    {
        var workspace = MSBuildWorkspace.Create();
        if (fileInfo.Extension == ".sln" || fileInfo.Extension == ".slnx")
        {
            var start = Stopwatch.GetTimestamp();
            LogInfoLoadingSolution(_logger, fileInfo.FullName);
            var solution = await workspace.OpenSolutionAsync(fileInfo.FullName, new ProjectProgress(_logger));
            _logger.LogInformation("Loaded solution '{path}' with {count} projects in {duration}ms", solution.FilePath, solution.Projects.Count(), Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        else if (fileInfo.Extension == ".csproj")
        {
            var start = Stopwatch.GetTimestamp();
            LogInfoLoadingProject(_logger, fileInfo.FullName);
            var project = await workspace.OpenProjectAsync(fileInfo.FullName, new ProjectProgress(_logger));
            _logger.LogInformation("Loaded project '{path}' with {count} documents in {duration}ms", project.FilePath, project.Documents.Count(), Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        else
        {
            workspace.Dispose();
            throw new NotSupportedException($"Unsupported file extension: {fileInfo.Extension}");
        }

        return workspace;
    }

    private class ProjectProgress(ILogger logger) : IProgress<ProjectLoadProgress>
    {
        public void Report(ProjectLoadProgress value)
        {
            logger.LogInformation("Project '{ProjectName}' reached {Status} in {Duration}ms", value.FilePath, value.Operation, Math.Floor(value.ElapsedTime.TotalMilliseconds));
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Loading solution '{Path}'"
    )]
    private static partial void LogInfoLoadingSolution(ILogger logger, string path);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Loading project '{Path}'"
    )]
    private static partial void LogInfoLoadingProject(ILogger logger, string path);
}
