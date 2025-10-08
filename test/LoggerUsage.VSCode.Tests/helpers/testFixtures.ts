import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';

/**
 * Test fixture infrastructure for creating temporary workspaces
 * and sample files for integration tests.
 */

export interface TestWorkspace {
  rootPath: string;
  solutionPath: string;
  projectPath: string;
  cleanup: () => Promise<void>;
}

/**
 * Creates a temporary test workspace with a sample solution and project.
 * @param workspaceName - Name for the temporary workspace
 * @returns TestWorkspace object with paths and cleanup function
 */
export async function createTestWorkspace(workspaceName: string = 'test-workspace'): Promise<TestWorkspace> {
  const tempDir = os.tmpdir();
  const rootPath = path.join(tempDir, `logger-usage-test-${workspaceName}-${Date.now()}`);

  // Create directory structure
  await fs.promises.mkdir(rootPath, { recursive: true });
  const srcPath = path.join(rootPath, 'src');
  await fs.promises.mkdir(srcPath, { recursive: true });

  // Create solution file
  const solutionPath = path.join(rootPath, `${workspaceName}.sln`);
  const solutionContent = `
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "${workspaceName}", "src\\${workspaceName}.csproj", "{12345678-1234-1234-1234-123456789012}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{12345678-1234-1234-1234-123456789012}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{12345678-1234-1234-1234-123456789012}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{12345678-1234-1234-1234-123456789012}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{12345678-1234-1234-1234-123456789012}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
EndGlobal
`.trim();
  await fs.promises.writeFile(solutionPath, solutionContent, 'utf-8');

  // Create project file
  const projectPath = path.join(srcPath, `${workspaceName}.csproj`);
  const projectContent = `
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
  </ItemGroup>
</Project>
`.trim();
  await fs.promises.writeFile(projectPath, projectContent, 'utf-8');

  // Create a basic C# file with logging
  const programPath = path.join(srcPath, 'Program.cs');
  const programContent = createSampleCSharpFile();
  await fs.promises.writeFile(programPath, programContent, 'utf-8');

  // Cleanup function
  const cleanup = async () => {
    try {
      await fs.promises.rm(rootPath, { recursive: true, force: true });
    } catch (error) {
      console.error(`Failed to cleanup test workspace at ${rootPath}:`, error);
    }
  };

  return {
    rootPath,
    solutionPath,
    projectPath,
    cleanup
  };
}

/**
 * Creates a sample C# file with logging calls.
 * @param logCalls - Number of log calls to include (default: 5)
 * @returns C# source code as string
 */
export function createSampleCSharpFile(logCalls: number = 5): string {
  const logStatements: string[] = [];

  for (let i = 0; i < logCalls; i++) {
    const level = ['Information', 'Warning', 'Error', 'Debug', 'Trace'][i % 5];
    logStatements.push(`        _logger.Log${level}("Sample log message {Count}", ${i});`);
  }

  return `
using Microsoft.Extensions.Logging;

namespace TestWorkspace;

public class SampleService
{
    private readonly ILogger<SampleService> _logger;

    public SampleService(ILogger<SampleService> logger)
    {
        _logger = logger;
    }

    public void ProcessData()
    {
${logStatements.join('\n')}
    }
}
`.trim();
}

/**
 * Creates a corrupted/invalid solution file for error testing.
 * @param workspacePath - Root path where the corrupted solution should be created
 * @param fileName - Name of the solution file (default: 'Corrupted.sln')
 * @returns Path to the corrupted solution file
 */
export async function createCorruptedSolution(workspacePath: string, fileName: string = 'Corrupted.sln'): Promise<string> {
  const solutionPath = path.join(workspacePath, fileName);

  // Create invalid solution content (missing required sections)
  const corruptedContent = `
This is not a valid solution file.
It lacks proper structure and will fail to parse.
Microsoft Visual Studio Solution File, Format Version 99.99
# Invalid Solution
  `.trim();

  await fs.promises.writeFile(solutionPath, corruptedContent, 'utf-8');
  return solutionPath;
}

/**
 * Cleans up a test workspace by removing all files and directories.
 * @param workspacePath - Root path of the workspace to clean up
 */
export async function cleanupTestWorkspace(workspacePath: string): Promise<void> {
  try {
    await fs.promises.rm(workspacePath, { recursive: true, force: true });
  } catch (error) {
    console.error(`Failed to cleanup test workspace at ${workspacePath}:`, error);
    throw error;
  }
}

/**
 * Creates a C# file with specific logging patterns for testing.
 * @param filePath - Path where the file should be created
 * @param options - Configuration for the generated logging patterns
 */
export async function createCSharpFileWithLogging(
  filePath: string,
  options: {
    loggerExtensionMethods?: number;
    loggerMessageAttributes?: number;
    inconsistencies?: boolean;
  } = {}
): Promise<void> {
  const {
    loggerExtensionMethods = 3,
    loggerMessageAttributes = 2,
    inconsistencies = false
  } = options;

  const extensionMethods: string[] = [];
  const loggerMessages: string[] = [];

  // Generate logger extension method calls
  for (let i = 0; i < loggerExtensionMethods; i++) {
    const level = ['Information', 'Warning', 'Error'][i % 3];
    if (inconsistencies && i === 0) {
      // Create parameter mismatch
      extensionMethods.push(`        _logger.Log${level}("Message with {Param1} and {Param2}", value1);`);
    } else {
      extensionMethods.push(`        _logger.Log${level}("Message {Value}", value${i});`);
    }
  }

  // Generate LoggerMessage attribute methods
  for (let i = 0; i < loggerMessageAttributes; i++) {
    const level = ['Information', 'Warning'][i % 2];
    const eventId = 1000 + i;
    loggerMessages.push(`
    [LoggerMessage(EventId = ${eventId}, Level = LogLevel.${level}, Message = "LoggerMessage pattern {Name}")]
    private partial void LogMessage${i}(string name);
`);
  }

  const content = `
using Microsoft.Extensions.Logging;

namespace TestWorkspace;

public partial class TestService
{
    private readonly ILogger<TestService> _logger;

    public TestService(ILogger<TestService> logger)
    {
        _logger = logger;
    }

    public void ExecuteMethod()
    {
        var value1 = 1;
        var value2 = 2;
${extensionMethods.join('\n')}
    }

${loggerMessages.join('\n')}
}
`.trim();

  // Ensure directory exists
  const dir = path.dirname(filePath);
  await fs.promises.mkdir(dir, { recursive: true });

  await fs.promises.writeFile(filePath, content, 'utf-8');
}
