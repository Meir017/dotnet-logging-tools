using AwesomeAssertions;
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

        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesAsync(workspace);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().ContainSingle();
    }

    [Fact]
    public async Task Test_EmptyWorkspace()
    {
        // Arrange
        var workspace = await CreateTestWorkspace([]);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesAsync(workspace);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().BeEmpty();
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

        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesAsync(workspace);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().HaveCount(2);
    }

    private static async Task<Workspace> CreateTestWorkspace(Dictionary<string, (string FileName, string SourceCode)[]> projectDocuments)
    {
        var workspace = new AdhocWorkspace();
        foreach (var (projectName, documents) in projectDocuments)
        {
            var projectId = ProjectId.CreateNewId(projectName);
            var projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Default,
                name: projectName,
                assemblyName: projectName,
                LanguageNames.CSharp,
                metadataReferences: await TestUtils.GetMetadataReferencesAsync(),
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var proj = workspace.AddProject(projectInfo);

            foreach (var (fileName, sourceCode) in documents)
            {
                workspace.AddDocument(proj.Id, fileName, SourceText.From(sourceCode));
            }
        }

        var solution = workspace.CurrentSolution;
        foreach (var proj in solution.Projects)
        {
            var compilation = await proj.GetCompilationAsync(TestContext.Current.CancellationToken);
            compilation.Should().NotBeNull();
            var diagnostics = compilation.GetDiagnostics(TestContext.Current.CancellationToken);
            diagnostics.Should().BeEmpty();
        }

        return workspace;
    }
}
