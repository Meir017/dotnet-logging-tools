# Release Specification: 1.0.0-preview.5

**Branch**: `009-preview-5-release`  
**Created**: 2026-08-08  
**Status**: Complete

## Goal

Publish the merged Roslyn pipeline hardening and SARIF work as the next preview release.

## Requirements

- Set the shared package version to `1.0.0-preview.5`.
- Build and pack all publishable .NET projects successfully.
- Publish the four NuGet packages from tag `v1.0.0-preview.5`.
- Create a GitHub prerelease with generated release notes.
- Do not reuse or overwrite an existing package version or Git tag.

## Validation

- The release build has no warnings or errors.
- Generated packages have version `1.0.0-preview.5`.
- NuGet.org lists `1.0.0-preview.5` for LoggerUsage, LoggerUsage.Cli, LoggerUsage.Mcp, and LoggerUsage.MSBuild.
- The GitHub release is marked as a prerelease and targets the same tag.
