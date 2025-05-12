using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using LoggerUsage.Models;
using LoggerUsage.Analyzers;

namespace LoggerUsage
{
    public class LoggerUsageExtractor
    {
        private static readonly ILoggerUsageAnalyzer[] _analyzers =
        {
            new LogMethodAnalyzer(),
            new LoggerMessageAttributeAnalyzer()
        };

        public async Task<List<LoggerUsageInfo>> ExtractLoggerUsagesAsync(Workspace workspace)
        {
            var results = new List<LoggerUsageInfo>();

            foreach (var project in workspace.CurrentSolution.Projects)
            {
                if (project.Language != LanguageNames.CSharp)
                    continue;

                var compilation = await project.GetCompilationAsync();
                if (compilation == null)
                    continue;

                results.AddRange(ExtractLoggerUsages(compilation));
            }

            return results;
        }

        public List<LoggerUsageInfo> ExtractLoggerUsages(Compilation compilation)
        {
            var loggerInterface = compilation.GetTypeByMetadataName(typeof(ILogger).FullName!)!;
            if (loggerInterface == null) return [];

            var loggingTypes = new LoggingTypes(compilation, loggerInterface);
            var results = new List<LoggerUsageInfo>();

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var root = syntaxTree.GetRoot();
                var semanticModel = compilation.GetSemanticModel(syntaxTree);

                if (root == null || semanticModel == null) continue;

                foreach (var analyzer in _analyzers)
                {
                    results.AddRange(analyzer.Analyze(loggingTypes, root, semanticModel));
                }
            }

            return results;
        }
    }
}
