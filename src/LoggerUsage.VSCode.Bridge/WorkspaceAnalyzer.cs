using System.Diagnostics;
using System.Text.Json;
using LoggerUsage.VSCode.Bridge.Models;
using Microsoft.CodeAnalysis;

namespace LoggerUsage.VSCode.Bridge;

/// <summary>
/// Orchestrates C# workspace analysis using LoggerUsageExtractor
/// </summary>
public class WorkspaceAnalyzer
{
    private readonly IWorkspaceFactory _workspaceFactory;
    private readonly LoggerUsageExtractor _loggerUsageExtractor;

    public WorkspaceAnalyzer(
        IWorkspaceFactory workspaceFactory,
        LoggerUsageExtractor loggerUsageExtractor)
    {
        _workspaceFactory = workspaceFactory;
        _loggerUsageExtractor = loggerUsageExtractor;
    }

    /// <summary>
    /// Analyzes the entire workspace (solution or project)
    /// </summary>
    /// <param name="request">Analysis request with workspace/solution path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis success or error response</returns>
    public async Task<IBridgeResponse> AnalyzeWorkspaceAsync(
        AnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Determine the file to load (solution or workspace)
            FileInfo? fileToLoad;
            try
            {
                fileToLoad = !string.IsNullOrWhiteSpace(request.SolutionPath)
                    ? new FileInfo(request.SolutionPath)
                    : FindSolutionOrProject(request.WorkspacePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                return new AnalysisErrorResponse
                {
                    Message = "Access denied to workspace directory",
                    Details = $"You don't have permission to access: {request.WorkspacePath ?? request.SolutionPath}\n{ex.Message}",
                    ErrorCode = "FILE_SYSTEM_ERROR"
                };
            }
            catch (IOException ex)
            {
                return new AnalysisErrorResponse
                {
                    Message = "File system error accessing workspace",
                    Details = $"I/O error accessing: {request.WorkspacePath ?? request.SolutionPath}\n{ex.Message}",
                    ErrorCode = "FILE_SYSTEM_ERROR"
                };
            }

            if (fileToLoad == null || !fileToLoad.Exists)
            {
                return new AnalysisErrorResponse
                {
                    Message = "No solution or project file found",
                    Details = $"Could not find a .sln, .slnx, or .csproj file in workspace: {request.WorkspacePath}",
                    ErrorCode = "NO_SOLUTION"
                };
            }

            // Report initial progress
            ReportProgress(0, "Loading solution...", null);

            // Load the workspace
            Workspace? workspace = null;
            try
            {
                workspace = await _workspaceFactory.Create(fileToLoad);
            }
            catch (InvalidOperationException ex)
            {
                return new AnalysisErrorResponse
                {
                    Message = "The solution file is invalid or corrupted",
                    Details = ex.Message,
                    ErrorCode = "INVALID_SOLUTION"
                };
            }
            catch (FileNotFoundException ex)
            {
                return new AnalysisErrorResponse
                {
                    Message = "Solution or project file not found",
                    Details = ex.Message,
                    ErrorCode = "FILE_NOT_FOUND"
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                return new AnalysisErrorResponse
                {
                    Message = "Access denied to solution or project file",
                    Details = $"You don't have permission to access: {fileToLoad.FullName}\n{ex.Message}",
                    ErrorCode = "FILE_SYSTEM_ERROR"
                };
            }
            catch (IOException ex)
            {
                return new AnalysisErrorResponse
                {
                    Message = "File system error loading solution",
                    Details = $"I/O error loading: {fileToLoad.FullName}\n{ex.Message}",
                    ErrorCode = "FILE_SYSTEM_ERROR"
                };
            }
            catch (Exception ex)
            {
                return new AnalysisErrorResponse
                {
                    Message = "Failed to load solution",
                    Details = $"Error loading {fileToLoad.FullName}: {ex.Message}",
                    ErrorCode = "SOLUTION_LOAD_ERROR"
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Count total projects for progress tracking
            var projects = workspace.CurrentSolution.Projects
                .Where(p => p.Language == LanguageNames.CSharp)
                .ToList();

            var totalProjects = projects.Count;

            if (totalProjects == 0)
            {
                stopwatch.Stop();
                return new AnalysisSuccessResponse
                {
                    Result = new AnalysisResult
                    {
                        Insights = [],
                        Summary = new AnalysisSummaryDto
                        {
                            TotalInsights = 0,
                            ByMethodType = [],
                            ByLogLevel = [],
                            InconsistenciesCount = 0,
                            FilesAnalyzed = 0,
                            AnalysisTimeMs = stopwatch.ElapsedMilliseconds,
                            WarningsCount = 0
                        }
                    }
                };
            }

            ReportProgress(10, $"Analyzing {totalProjects} projects...", null);

            // Create progress handler for VS Code
            var progress = new Progress<LoggerUsage.Models.LoggerUsageProgress>(p =>
            {
                ReportProgress(p.PercentComplete, p.OperationDescription, p.CurrentFilePath);
            });

            // Run the analysis with progress reporting
            var extractionResult = await _loggerUsageExtractor.ExtractLoggerUsagesAsync(workspace, progress, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Count compilation warnings/errors from all projects
            var warningsCount = 0;
            var hasMissingDependencies = false;
            foreach (var project in projects)
            {
                var compilation = await project.GetCompilationAsync(cancellationToken);
                if (compilation != null)
                {
                    var diagnostics = compilation.GetDiagnostics()
                        .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning ||
                                   d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                        .ToList();

                    if (diagnostics.Any())
                    {
                        warningsCount += diagnostics.Count;

                        // Check for CS0246 errors (missing type or namespace - indicates missing NuGet packages)
                        var missingTypeErrors = diagnostics.Where(d => d.Id == "CS0246").ToList();
                        if (missingTypeErrors.Any())
                        {
                            hasMissingDependencies = true;
                            // Log a few missing type errors
                            foreach (var diagnostic in missingTypeErrors.Take(3))
                            {
                                ReportProgress(
                                    50,
                                    $"Missing dependency: {diagnostic.GetMessage()}",
                                    diagnostic.Location.SourceTree?.FilePath
                                );
                            }
                        }

                        // Log compilation diagnostics to progress stream
                        foreach (var diagnostic in diagnostics.Take(5)) // Limit to first 5 per project
                        {
                            ReportProgress(
                                50,
                                $"Compilation {diagnostic.Severity}: {diagnostic.GetMessage()}",
                                diagnostic.Location.SourceTree?.FilePath
                            );
                        }
                    }
                }
            }

            // If missing dependencies detected, return specific error
            if (hasMissingDependencies)
            {
                return new AnalysisErrorResponse
                {
                    Message = "Missing NuGet packages detected",
                    Details = "Some types or namespaces could not be found. Try running 'dotnet restore' to restore missing NuGet packages.",
                    ErrorCode = "MISSING_DEPENDENCIES"
                };
            }

            ReportProgress(90, "Generating insights...", null);

            // Map results to DTOs
            var insights = extractionResult.Results
                .Select(LoggerUsageMapper.ToDto)
                .ToList();

            // Count files analyzed
            var filesAnalyzed = extractionResult.Results
                .Select(r => r.Location.FilePath)
                .Distinct()
                .Count();

            stopwatch.Stop();

            // Generate summary
            var summary = GenerateSummary(insights, filesAnalyzed, stopwatch.Elapsed, warningsCount);

            ReportProgress(100, "Analysis complete", null);

            return new AnalysisSuccessResponse
            {
                Result = new AnalysisResult
                {
                    Insights = insights,
                    Summary = summary
                }
            };
        }
        catch (OperationCanceledException)
        {
            return new AnalysisErrorResponse
            {
                Message = "Analysis cancelled",
                Details = "The analysis operation was cancelled by the user",
                ErrorCode = "CANCELLED"
            };
        }
        catch (Exception ex)
        {
            return new AnalysisErrorResponse
            {
                Message = "Analysis failed",
                Details = $"Unexpected error during analysis: {ex.Message}\n{ex.StackTrace}",
                ErrorCode = "UNKNOWN_ERROR"
            };
        }
    }

    /// <summary>
    /// Analyzes a single file incrementally
    /// </summary>
    /// <param name="request">Incremental analysis request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis success or error response</returns>
    public async Task<IBridgeResponse> AnalyzeFileAsync(
        IncrementalAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Load solution
            FileInfo? fileToLoad;
            try
            {
                fileToLoad = !string.IsNullOrWhiteSpace(request.SolutionPath)
                    ? new FileInfo(request.SolutionPath)
                    : FindSolutionOrProject(Path.GetDirectoryName(request.FilePath)!);
            }
            catch (UnauthorizedAccessException ex)
            {
                return new AnalysisErrorResponse
                {
                    Message = "Access denied to workspace directory",
                    Details = $"You don't have permission to access the directory containing: {request.FilePath}\n{ex.Message}",
                    ErrorCode = "FILE_SYSTEM_ERROR"
                };
            }
            catch (IOException ex)
            {
                return new AnalysisErrorResponse
                {
                    Message = "File system error accessing workspace",
                    Details = $"I/O error accessing directory containing: {request.FilePath}\n{ex.Message}",
                    ErrorCode = "FILE_SYSTEM_ERROR"
                };
            }

            if (fileToLoad == null || !fileToLoad.Exists)
            {
                return new AnalysisErrorResponse
                {
                    Message = "No solution found for file",
                    Details = $"Could not find solution for file: {request.FilePath}",
                    ErrorCode = "NO_SOLUTION"
                };
            }

            ReportProgress(0, "Loading solution...", null);

            Workspace? workspace = null;
            try
            {
                workspace = await _workspaceFactory.Create(fileToLoad);
            }
            catch (InvalidOperationException ex)
            {
                return new AnalysisErrorResponse
                {
                    Message = "The solution file is invalid or corrupted",
                    Details = ex.Message,
                    ErrorCode = "INVALID_SOLUTION"
                };
            }
            catch (FileNotFoundException ex)
            {
                return new AnalysisErrorResponse
                {
                    Message = "Solution or project file not found",
                    Details = ex.Message,
                    ErrorCode = "FILE_NOT_FOUND"
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                return new AnalysisErrorResponse
                {
                    Message = "Access denied to solution or project file",
                    Details = $"You don't have permission to access: {fileToLoad.FullName}\n{ex.Message}",
                    ErrorCode = "FILE_SYSTEM_ERROR"
                };
            }
            catch (IOException ex)
            {
                return new AnalysisErrorResponse
                {
                    Message = "File system error loading solution",
                    Details = $"I/O error loading: {fileToLoad.FullName}\n{ex.Message}",
                    ErrorCode = "FILE_SYSTEM_ERROR"
                };
            }
            catch (Exception ex)
            {
                return new AnalysisErrorResponse
                {
                    Message = "Failed to load solution",
                    Details = $"Error loading {fileToLoad.FullName}: {ex.Message}",
                    ErrorCode = "SOLUTION_LOAD_ERROR"
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            ReportProgress(30, "Analyzing file...", request.FilePath);

            // Create progress handler for VS Code
            var progress = new Progress<LoggerUsage.Models.LoggerUsageProgress>(p =>
            {
                ReportProgress(p.PercentComplete, p.OperationDescription, p.CurrentFilePath ?? request.FilePath);
            });

            // Find the document in the solution
            var document = workspace.CurrentSolution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.FilePath?.Equals(request.FilePath, StringComparison.OrdinalIgnoreCase) == true);

            if (document == null)
            {
                return new AnalysisErrorResponse
                {
                    Message = "File not found in solution",
                    Details = $"File {request.FilePath} is not part of the loaded solution",
                    ErrorCode = "FILE_NOT_IN_SOLUTION"
                };
            }

            // Get compilation for the project containing this document
            var compilation = await document.Project.GetCompilationAsync(cancellationToken);
            if (compilation == null)
            {
                return new AnalysisErrorResponse
                {
                    Message = "Failed to compile project",
                    Details = $"Could not get compilation for project {document.Project.Name}",
                    ErrorCode = "COMPILATION_ERROR"
                };
            }

            // Extract usages from the specific file
            var extractionResult = await _loggerUsageExtractor.ExtractLoggerUsagesWithSolutionAsync(
                compilation,
                workspace.CurrentSolution,
                progress,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Filter results to only include this file
            var fileInsights = extractionResult.Results
                .Where(r => r.Location.FilePath.Equals(request.FilePath, StringComparison.OrdinalIgnoreCase))
                .Select(LoggerUsageMapper.ToDto)
                .ToList();

            stopwatch.Stop();

            var summary = GenerateSummary(fileInsights, 1, stopwatch.Elapsed);

            ReportProgress(100, "File analysis complete", request.FilePath);

            return new AnalysisSuccessResponse
            {
                Result = new AnalysisResult
                {
                    Insights = fileInsights,
                    Summary = summary
                }
            };
        }
        catch (OperationCanceledException)
        {
            return new AnalysisErrorResponse
            {
                Message = "Analysis cancelled",
                Details = "The file analysis was cancelled by the user",
                ErrorCode = "CANCELLED"
            };
        }
        catch (Exception ex)
        {
            return new AnalysisErrorResponse
            {
                Message = "File analysis failed",
                Details = $"Unexpected error during file analysis: {ex.Message}\n{ex.StackTrace}",
                ErrorCode = "UNKNOWN_ERROR"
            };
        }
    }

    /// <summary>
    /// Reports progress by writing JSON progress message to stdout
    /// </summary>
    private void ReportProgress(int percentage, string message, string? currentFile)
    {
        var progress = new AnalysisProgress
        {
            Percentage = percentage,
            Message = message,
            CurrentFile = currentFile
        };

        var json = JsonSerializer.Serialize<IBridgeResponse>(progress, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Console.WriteLine(json);
    }

    /// <summary>
    /// Generates analysis summary statistics
    /// </summary>
    private AnalysisSummaryDto GenerateSummary(
        List<LoggingInsightDto> insights,
        int filesAnalyzed,
        TimeSpan duration,
        int warningsCount = 0)
    {
        var byMethodType = insights
            .GroupBy(i => i.MethodType)
            .ToDictionary(g => g.Key, g => g.Count());

        var byLogLevel = insights
            .Where(i => i.LogLevel != null)
            .GroupBy(i => i.LogLevel!)
            .ToDictionary(g => g.Key, g => g.Count());

        var inconsistenciesCount = insights
            .Count(i => i.HasInconsistencies);

        return new AnalysisSummaryDto
        {
            TotalInsights = insights.Count,
            ByMethodType = byMethodType,
            ByLogLevel = byLogLevel,
            InconsistenciesCount = inconsistenciesCount,
            FilesAnalyzed = filesAnalyzed,
            AnalysisTimeMs = (long)duration.TotalMilliseconds,
            WarningsCount = warningsCount
        };
    }

    /// <summary>
    /// Finds the first .sln, .slnx, or .csproj file in the workspace
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the directory is denied</exception>
    /// <exception cref="IOException">Thrown when I/O error occurs</exception>
    private FileInfo? FindSolutionOrProject(string workspacePath)
    {
        var directory = new DirectoryInfo(workspacePath);
        if (!directory.Exists)
        {
            return null;
        }

        // Look for solution files first
        // These operations can throw UnauthorizedAccessException or IOException
        var solutionFile = directory.GetFiles("*.sln").FirstOrDefault()
            ?? directory.GetFiles("*.slnx").FirstOrDefault();

        if (solutionFile != null)
        {
            return solutionFile;
        }

        // Fall back to first .csproj
        return directory.GetFiles("*.csproj").FirstOrDefault();
    }
}
