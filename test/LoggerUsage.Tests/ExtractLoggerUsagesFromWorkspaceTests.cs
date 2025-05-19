using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Tests;

public class ExtractLoggerUsagesFromWorkspaceTests
{
    [Fact]
    public async Task Test_SimpleWorkspace()
    {
        // Arrange
        var workspace = await CreateTestWorkspace(new()
        {
            ["TestProject"] = 
            [
                ("TestDocument.cs", @"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger)
    {
        logger.LogInformation(""Test message"");
    }
}") ] });

        var extractor = new LoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesAsync(workspace);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
    }

    [Fact]
    public async Task Test_EmptyWorkspace()
    {
        // Arrange
        var workspace = await CreateTestWorkspace([]);
        var extractor = new LoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesAsync(workspace);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Empty(loggerUsages.Results);
    }

    [Fact]
    public async Task Test_WorkspaceWithMultipleProjects()
    {
        // Arrange
        var workspace = await CreateTestWorkspace(new()
        {
            ["ProjectA"] = 
            [
                ("DocumentA.cs", @"using Microsoft.Extensions.Logging;
namespace ProjectANamespace;
public class ProjectAClass
{
    public void ProjectAMethod(ILogger logger)
    {
        logger.LogInformation(""Project A message"");
    }
}"),
            ],
            ["ProjectB"] = 
            [
                ("DocumentB.cs", @"using Microsoft.Extensions.Logging;
namespace ProjectBNamespace;
public class ProjectBClass
{
    public void ProjectBMethod(ILogger logger)
    {
        logger.LogInformation(""Project B message"");
    }
}")
            ]
        });

        var extractor = new LoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesAsync(workspace);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Equal(2, loggerUsages.Results.Count);
    }

    private static async Task<Workspace> CreateTestWorkspace(Dictionary<string, (string FileName, string SourceCode)[]> projectDocuments)
    {
        var workspace = new AdhocWorkspace();
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, TestContext.Current.CancellationToken);
        references = references.Add(MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location));

        foreach (var (projectName, documents) in projectDocuments)
        {
            var projectId = ProjectId.CreateNewId(projectName);
            var projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Default,
                name: projectName,
                assemblyName: projectName,
                LanguageNames.CSharp,
                metadataReferences: references,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var proj = workspace.AddProject(projectInfo);

            foreach (var document in documents)
            {
                workspace.AddDocument(proj.Id, document.FileName, SourceText.From(document.SourceCode));
            }
        }

        var solution = workspace.CurrentSolution;
        foreach (var proj in solution.Projects)
        {
            var compilation = await proj.GetCompilationAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(compilation);
            var diagnostics = compilation.GetDiagnostics(TestContext.Current.CancellationToken);
            Assert.Empty(diagnostics);
        }

        return workspace;
    }
}