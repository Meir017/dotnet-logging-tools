# NuGet Packaging Plan for dotnet-logging-usage

## Overview

This document outlines the plan to create NuGet packages for the four main components of the dotnet-logging-usage project:

1. **LoggerUsage** - Core library package
2. **LoggerUsage.Cli** - Command-line tool package  
3. **LoggerUsage.Mcp** - Model Context Protocol server package
4. **LoggerUsage.MSBuild** - MSBuild integration package

## Package Structure & Naming

### 1. LoggerUsage (Core Library)

- **Package ID**: `LoggerUsage`
- **Description**: Core library for analyzing .NET logging usage patterns
- **Package Type**: Library
- **Target Audience**: Developers who want to integrate logging analysis into their applications

### 2. LoggerUsage.Cli (CLI Tool)

- **Package ID**: `LoggerUsage.Cli`
- **Description**: Command-line tool for analyzing logging usage in .NET projects
- **Package Type**: Tool (dotnet tool)
- **Target Audience**: Developers who want to analyze logging from command line or CI/CD pipelines

### 3. LoggerUsage.Mcp (MCP Server)

- **Package ID**: `LoggerUsage.Mcp`
- **Description**: Model Context Protocol server for logging analysis integration
- **Package Type**: Tool/Application
- **Target Audience**: AI/ML developers using MCP for code analysis

### 4. LoggerUsage.MSBuild (MSBuild Integration)

- **Package ID**: `LoggerUsage.MSBuild`
- **Description**: MSBuild integration library for workspace and compilation utilities
- **Package Type**: Library
- **Target Audience**: Developers who need MSBuild-specific functionality for logging analysis

## Required Project File Updates

### 1. Common Properties (Directory.Build.props)

Add shared NuGet package properties:

```xml
<PropertyGroup>
  <!-- Version Management -->
  <Version>1.0.0-preview.1</Version>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
  
  <!-- Package-specific version overrides -->
  <!-- All packages use the same preview version for initial release -->
  
  <!-- Package Metadata -->
  <Authors>Meir017</Authors>
  <Company>Meir017</Company>
  <Product>dotnet-logging-usage</Product>
  <Copyright>Copyright © Meir017 2025</Copyright>
  
  <!-- Repository Information -->
  <RepositoryUrl>https://github.com/Meir017/dotnet-logging-usage</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  <RepositoryBranch>main</RepositoryBranch>
  
  <!-- Documentation -->
  <PackageProjectUrl>https://github.com/Meir017/dotnet-logging-usage</PackageProjectUrl>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  
  <!-- Package Properties -->
  <PackageRequireLicenseAcceptance>false</PackageRequireLicenseAcceptance>
  <PackageTags>logging;dotnet;analysis;csharp;msbuild;cli;mcp</PackageTags>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
</PropertyGroup>
```

### 2. LoggerUsage.csproj Updates

```xml
<PropertyGroup>
  <!-- Package-specific metadata -->
  <PackageId>LoggerUsage</PackageId>
  <Title>Logger Usage Analysis Library</Title>
  <Description>Core library for analyzing .NET logging usage patterns. Supports ILogger extensions, LoggerMessage attributes, and structured logging analysis.</Description>
  <PackageTags>$(PackageTags);library;core</PackageTags>
  
  <!-- Library-specific settings -->
  <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>

<ItemGroup>
  <None Include="..\..\README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

### 3. LoggerUsage.Cli.csproj Updates

```xml
<PropertyGroup>
  <!-- Package-specific metadata -->
  <PackageId>LoggerUsage.Cli</PackageId>
  <Title>Logger Usage Analysis CLI Tool</Title>
  <Description>Command-line tool for analyzing logging usage in .NET projects. Generates HTML and JSON reports of logging patterns and inconsistencies.</Description>
  <PackageTags>$(PackageTags);cli;tool;commandline</PackageTags>
  
  <!-- Tool-specific settings -->
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>logger-usage</ToolCommandName>
  <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
</PropertyGroup>

<ItemGroup>
  <None Include="..\..\README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

### 4. LoggerUsage.Mcp.csproj Updates

```xml
<PropertyGroup>
  <!-- Package-specific metadata -->
  <PackageId>LoggerUsage.Mcp</PackageId>
  <Title>Logger Usage MCP Server</Title>
  <Description>Model Context Protocol server for logging analysis integration with AI tools and IDEs.</Description>
  <PackageTags>$(PackageTags);mcp;server;ai;integration</PackageTags>
  <Version>1.0.0-preview.1</Version>
  
  <!-- Application-specific settings -->
  <IsPackable>true</IsPackable>
  <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>false</SelfContained>
</PropertyGroup>

<ItemGroup>
  <None Include="..\..\README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

### 5. LoggerUsage.MSBuild.csproj Updates

```xml
<PropertyGroup>
  <!-- Package-specific metadata -->
  <PackageId>LoggerUsage.MSBuild</PackageId>
  <Title>Logger Usage MSBuild Integration</Title>
  <Description>MSBuild integration library for analyzing logging usage in .NET projects. Provides workspace and compilation utilities for MSBuild-based projects.</Description>
  <PackageTags>$(PackageTags);msbuild;integration;workspace</PackageTags>
  
  <!-- Library-specific settings -->
  <IsPackable>true</IsPackable>
  <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
</PropertyGroup>

<ItemGroup>
  <None Include="..\..\README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

## Build and Release Pipeline

### 1. Local Development

```bash
# Build all packages
dotnet pack --configuration Release

# Build specific package
dotnet pack src/LoggerUsage/LoggerUsage.csproj --configuration Release
dotnet pack src/LoggerUsage.Cli/LoggerUsage.Cli.csproj --configuration Release
dotnet pack src/LoggerUsage.Mcp/LoggerUsage.Mcp.csproj --configuration Release
```

### 2. Version Management Strategy

- Use semantic versioning (SemVer): `MAJOR.MINOR.PATCH[-PRERELEASE]`
- All packages use the same version for consistency
- Initial release is `1.0.0-preview.1` to accommodate prerelease dependencies
- Future releases:
  - Preview: `1.0.0-preview.2`, `1.0.0-preview.3`, etc.
  - Stable: `1.0.0` when all dependencies are stable
  - Major: Breaking changes
  - Minor: New features, backward compatible
  - Patch: Bug fixes, backward compatible

### 3. Release Process

1. Update version in `Directory.Build.props`
2. Update CHANGELOG.md with release notes
3. Create git tag: `git tag v1.0.0`
4. Build packages: `dotnet pack --configuration Release`
5. Push to NuGet.org: `dotnet nuget push`

### 4. GitHub Actions CI/CD (✅ Implemented)

The workflow file `.github/workflows/publish-packages.yml` includes:

**Build and Test Job:**
- Runs on every push and pull request
- Sets up .NET 10.0 with prerelease support
- Restores dependencies, builds, and runs tests
- Creates NuGet packages and uploads as artifacts
- Collects code coverage data

**GitHub Packages Publishing:**
- Runs on pushes to `main` branch or version tags
- Automatically publishes to GitHub Packages registry
- Uses `GITHUB_TOKEN` (automatically available)
- Skips duplicate packages

**NuGet.org Publishing:**
- Runs only on version tags (e.g., `v1.0.0-preview.1`)
- Requires manual approval via GitHub environment protection
- Uses `NUGET_API_KEY` secret (needs to be configured)
- Publishes to the public NuGet.org registry

**Usage:**
```bash
# Trigger GitHub Packages publish (preview/development)
git push origin main

# Trigger full release to both GitHub Packages and NuGet.org
git tag v1.0.0-preview.1
git push origin v1.0.0-preview.1
```

## Installation Instructions

### Core Library

```bash
dotnet add package LoggerUsage --version 1.0.0-preview.1
```

### CLI Tool

```bash
# Install globally
dotnet tool install -g LoggerUsage.Cli --version 1.0.0-preview.1

# Use the tool
logger-usage path/to/project.sln report.html
```

### MCP Server

```bash
dotnet add package LoggerUsage.Mcp --version 1.0.0-preview.1
```

### MSBuild Integration

```bash
dotnet add package LoggerUsage.MSBuild --version 1.0.0-preview.1
```

## Additional Considerations

### 1. Documentation

- Include comprehensive XML documentation in code
- Create separate documentation packages if needed
- Consider DocFX for generating documentation websites

### 2. Testing Strategy

- Ensure all packages have comprehensive tests
- Test package installation and basic functionality
- Consider integration tests for CLI tool

### 3. Security

- Sign packages with strong name key
- Consider code signing certificates
- Regular security updates for dependencies

### 4. Compatibility

- Support multiple .NET versions if needed
- Clear compatibility matrix in documentation
- Breaking changes communication strategy

### 5. Package Dependencies

- Minimize external dependencies where possible
- Use PackageReference with specific versions
- Regular dependency updates and security patches

## Repository Setup for Publishing

### GitHub Packages Configuration

GitHub Packages will work automatically with the workflow, but you may want to configure:

1. **Repository Settings > Actions > General**:
   - Enable "Read and write permissions" for `GITHUB_TOKEN`
   - Allow actions to create and approve pull requests

2. **Repository Settings > Packages**:
   - Configure package visibility (public/private)
   - Set up package access permissions

### NuGet.org Publishing Setup

For publishing to NuGet.org, you need to:

1. **Get a NuGet API Key**:
   - Go to [nuget.org](https://www.nuget.org)
   - Sign in and go to Account Settings > API Keys
   - Create a new API key with push permissions
   - Scope it to the specific package names if desired

2. **Add Repository Secret**:
   - Go to Repository Settings > Secrets and variables > Actions
   - Add a new repository secret named `NUGET_API_KEY`
   - Paste your NuGet.org API key as the value

3. **Set up Environment Protection** (Recommended):
   - Go to Repository Settings > Environments
   - Create an environment named `production`
   - Add protection rules (require reviews, restrict to main branch, etc.)
   - This ensures manual approval before publishing to NuGet.org

### Package Installation from GitHub Packages

Users can install packages from GitHub Packages by adding the package source:

```bash
# Add GitHub Packages source (one-time setup)
dotnet nuget add source "https://nuget.pkg.github.com/Meir017/index.json" --name "github-meir017" --username YOUR_GITHUB_USERNAME --password YOUR_GITHUB_TOKEN

# Install packages
dotnet add package LoggerUsage --version 1.0.0-preview.1 --source "github-meir017"
dotnet tool install -g LoggerUsage.Cli --version 1.0.0-preview.1 --add-source "github-meir017"
```

## Success Metrics

- Package download count
- User feedback and issues
- Integration success rate
- Community contributions

## Next Steps

1. ✅ Create this plan document
2. ✅ Update project files with package metadata
3. ✅ Test local package building
4. ✅ Enable packaging for LoggerUsage.MSBuild
5. ✅ Align all packages to use preview version
6. ✅ Set up GitHub Actions workflow
7. ⏳ Configure GitHub repository settings
8. ⏳ Create initial release (v1.0.0-preview.1)
9. ⏳ Test GitHub Packages publishing
10. ⏳ Set up NuGet.org API key for public publishing
11. ⏳ Update documentation with installation instructions
12. ⏳ Monitor package usage and feedback
