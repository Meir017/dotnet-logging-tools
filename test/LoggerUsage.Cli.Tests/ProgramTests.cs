using Microsoft.Extensions.Hosting;

namespace LoggerUsage.Cli.Tests;

public class ProgramTests
{
    public static TheoryData<string[]> RunProgramWithoutPathData => new(
        [],
        ["fake-csproj.csproj"],
        ["fake-sln.sln"],
        ["fake-slnx.slnx"]
    );

    [Theory]
    [MemberData(nameof(RunProgramWithoutPathData))]
    public async Task RunProgramWithoutPath(string[] args)
    {
        // Arrange
        var worker = Program.CreateWorker(args);

        // Act
        var result = await worker.RunAsync();

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public async Task RunProgramWithPath()
    {
        // Arrange
        var csprojPath = GetCliCsprojPath();
        var worker = Program.CreateWorker([csprojPath]);

        // Act
        var result = await worker.RunAsync();

        // Assert
        Assert.Equal(0, result);
    }

    private static string GetCliCsprojPath()
    {
        var gitRoot = FindGitRoot();
        return Path.Combine(gitRoot, "src", "LoggerUsage.Cli", "LoggerUsage.Cli.csproj");

        static string FindGitRoot()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            while (currentDirectory != null && !Directory.Exists(Path.Combine(currentDirectory, ".git")))
            {
                currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
            }
            return currentDirectory ?? throw new InvalidOperationException("Git root not found");
        }
    }
}
