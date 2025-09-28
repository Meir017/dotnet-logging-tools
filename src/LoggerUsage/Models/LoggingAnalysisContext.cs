using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Models;

/// <summary>
/// Provides context information for analyzing logger usage patterns in C# source code.
/// </summary>
public class LoggingAnalysisContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingAnalysisContext"/> class.
    /// </summary>
    /// <param name="loggingTypes">The logging types configuration used for analysis.</param>
    /// <param name="root">The root syntax node of the syntax tree to analyze.</param>
    /// <param name="semanticModel">The semantic model providing type information for the syntax tree.</param>
    /// <param name="solution">Optional solution for cross-project analysis.</param>
    /// <param name="logger">Logger for diagnostic logging during analysis.</param>
    public LoggingAnalysisContext(
        LoggingTypes loggingTypes,
        SyntaxNode root,
        SemanticModel semanticModel,
        Solution? solution = null,
        ILogger? logger = null)
    {
        LoggingTypes = loggingTypes ?? throw new ArgumentNullException(nameof(loggingTypes));
        Root = root ?? throw new ArgumentNullException(nameof(root));
        SemanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        Solution = solution;
        Logger = logger;
    }

    /// <summary>
    /// Gets the logging types configuration used for analysis.
    /// </summary>
    public LoggingTypes LoggingTypes { get; }

    /// <summary>
    /// Gets the root syntax node of the syntax tree to analyze.
    /// </summary>
    public SyntaxNode Root { get; }

    /// <summary>
    /// Gets the semantic model providing type information for the syntax tree.
    /// </summary>
    public SemanticModel SemanticModel { get; }

    /// <summary>
    /// Gets the optional solution for cross-project analysis.
    /// </summary>
    public Solution? Solution { get; }

    /// <summary>
    /// Gets the optional logger for diagnostic logging during analysis.
    /// </summary>
    public ILogger? Logger { get; }

    /// <summary>
    /// Creates a new analysis context for workspace analysis.
    /// </summary>
    /// <param name="loggingTypes">The logging types configuration used for analysis.</param>
    /// <param name="root">The root syntax node of the syntax tree to analyze.</param>
    /// <param name="semanticModel">The semantic model providing type information for the syntax tree.</param>
    /// <param name="solution">The solution for cross-project analysis.</param>
    /// <param name="logger">Logger for diagnostic logging during analysis.</param>
    /// <returns>A new analysis context configured for workspace analysis.</returns>
    public static LoggingAnalysisContext CreateForWorkspace(
        LoggingTypes loggingTypes,
        SyntaxNode root,
        SemanticModel semanticModel,
        Solution solution,
        ILogger? logger = null)
    {
        return new LoggingAnalysisContext(loggingTypes, root, semanticModel, solution, logger);
    }

    /// <summary>
    /// Creates a new analysis context for compilation analysis.
    /// </summary>
    /// <param name="loggingTypes">The logging types configuration used for analysis.</param>
    /// <param name="root">The root syntax node of the syntax tree to analyze.</param>
    /// <param name="semanticModel">The semantic model providing type information for the syntax tree.</param>
    /// <param name="logger">Logger for diagnostic logging during analysis.</param>
    /// <returns>A new analysis context configured for compilation analysis.</returns>
    public static LoggingAnalysisContext CreateForCompilation(
        LoggingTypes loggingTypes,
        SyntaxNode root,
        SemanticModel semanticModel,
        ILogger? logger = null)
    {
        return new LoggingAnalysisContext(loggingTypes, root, semanticModel, null, logger);
    }
}