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

    [Theory]
    [InlineData("src", "LoggerUsage.Cli", "LoggerUsage.Cli.csproj")]
    [InlineData("logging-usage.sln")]
    public async Task RunProgramWithPath(params string[] paths)
    {
        // Arrange
        var csprojPath = FindPathFromGitRoot(paths);
        var worker = Program.CreateWorker([csprojPath]);

        // Act
        var result = await worker.RunAsync();

        // Assert
        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData("output.json")]
    [InlineData("output.html")]
    public async Task RunProgramWithPathAndOutputPath(string outputFileName)
    {
        // Arrange
        var csprojPath = FindPathFromGitRoot("src", "LoggerUsage.Cli", "LoggerUsage.Cli.csproj");

        // Act & Assert
        await RunProgramWithFileAndOutputPath(csprojPath, outputFileName);
    }

    private static async Task RunProgramWithFileAndOutputPath(string inputPath, string outputFileName)
    {
        // Arrange
        using var tempDirectory = new TempDirectory();
        var outputPath = Path.Combine(tempDirectory.Path, outputFileName);
        var worker = Program.CreateWorker([inputPath, outputPath]);

        // Act
        var result = await worker.RunAsync();

        // Assert
        Assert.Equal(0, result);
        Assert.True(File.Exists(outputPath));
    }

    private static string FindPathFromGitRoot(params string[] paths)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        while (currentDirectory != null && !Directory.Exists(Path.Combine(currentDirectory, ".git")))
        {
            currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        }

        var gitRoot = currentDirectory ?? throw new InvalidOperationException("Git root not found");
        return Path.Combine([gitRoot, .. paths]);
    }

    private class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            Directory.Delete(Path, true);
        }
    }
}
