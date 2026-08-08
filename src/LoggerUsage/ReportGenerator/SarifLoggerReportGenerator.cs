using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoggerUsage.Diagnostics;
using LoggerUsage.Models;

namespace LoggerUsage.ReportGenerator;

internal sealed class SarifLoggerReportGenerator : ILoggerReportGenerator
{
    private const string SchemaUri = "https://json.schemastore.org/sarif-2.1.0.json";
    private const string SarifVersion = "2.1.0";
    private const string SourceRootId = "%SRCROOT%";
    private const string FingerprintName = "loggerUsage/v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string GenerateReport(LoggerUsageExtractionResult loggerUsage) =>
        GenerateReport(loggerUsage, new ReportGenerationContext());

    public string GenerateReport(
        LoggerUsageExtractionResult loggerUsage,
        ReportGenerationContext context)
    {
        ArgumentNullException.ThrowIfNull(loggerUsage);
        ArgumentNullException.ThrowIfNull(context);

        var findings = LoggerUsageFindingFactory.Create(loggerUsage);
        var sourceRoot = ResolveSourceRoot(findings, context.SourceRoot);
        var results = findings
            .Select(finding => CreateResult(finding, sourceRoot))
            .OrderBy(result => result.Locations[0].PhysicalLocation.ArtifactLocation.Uri, StringComparer.Ordinal)
            .ThenBy(result => result.Locations[0].PhysicalLocation.Region.StartLine)
            .ThenBy(result => result.RuleId, StringComparer.Ordinal)
            .ThenBy(result => result.Message.Text, StringComparer.Ordinal)
            .ToArray();

        var rules = LoggerUsageRules.All
            .OrderBy(rule => rule.Id, StringComparer.Ordinal)
            .Select(CreateRule)
            .ToArray();

        var run = new SarifRun(
            new SarifTool(new SarifDriver("LoggerUsage", rules)),
            sourceRoot is null
                ? null
                : new Dictionary<string, SarifArtifactLocation>(StringComparer.Ordinal)
                {
                    [SourceRootId] = new(PathToDirectoryUri(sourceRoot), null)
                },
            results);

        return JsonSerializer.Serialize(
            new SarifLog(SchemaUri, SarifVersion, [run]),
            JsonOptions);
    }

    private static SarifReportingDescriptor CreateRule(LoggerUsageRule rule) =>
        new(
            rule.Id,
            rule.Name,
            new SarifMessage(rule.ShortDescription),
            new SarifMessage(rule.FullDescription),
            rule.HelpUri,
            new SarifConfiguration(rule.DefaultLevel));

    private static SarifResult CreateResult(LoggerUsageFinding finding, string? sourceRoot)
    {
        if (finding.Location.StartLineNumber < 1
            || finding.Location.EndLineNumber < finding.Location.StartLineNumber)
        {
            throw new InvalidOperationException(
                $"The SARIF location for '{finding.Location.FilePath}' has an invalid line range.");
        }

        var relativePath = NormalizePath(finding.Location.FilePath, sourceRoot);
        var fingerprint = CreateFingerprint(finding, relativePath);

        return new SarifResult(
            finding.Rule.Id,
            finding.Rule.DefaultLevel,
            new SarifMessage(finding.Message),
            [
                new SarifLocation(
                    new SarifPhysicalLocation(
                        new SarifArtifactLocation(relativePath, sourceRoot is null ? null : SourceRootId),
                        new SarifRegion(
                            finding.Location.StartLineNumber,
                            finding.Location.EndLineNumber)))
            ],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [FingerprintName] = fingerprint
            });
    }

    private static string? ResolveSourceRoot(
        IReadOnlyList<LoggerUsageFinding> findings,
        string? requestedSourceRoot)
    {
        if (!string.IsNullOrWhiteSpace(requestedSourceRoot))
        {
            return Path.GetFullPath(requestedSourceRoot);
        }

        var paths = findings
            .Select(finding => Path.GetFullPath(finding.Location.FilePath))
            .ToArray();
        if (paths.Length == 0)
        {
            return null;
        }

        var commonDirectory = Path.GetDirectoryName(paths[0])!;
        foreach (var path in paths.Skip(1))
        {
            while (!IsWithinRoot(path, commonDirectory))
            {
                commonDirectory = Directory.GetParent(commonDirectory)?.FullName
                    ?? Path.GetPathRoot(commonDirectory)!;
            }
        }

        return commonDirectory;
    }

    private static string NormalizePath(string filePath, string? sourceRoot)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (sourceRoot is null)
        {
            return fullPath.Replace('\\', '/');
        }

        if (!IsWithinRoot(fullPath, sourceRoot))
        {
            throw new InvalidOperationException(
                $"The source file '{fullPath}' is outside the SARIF source root '{sourceRoot}'.");
        }

        return Path.GetRelativePath(sourceRoot, fullPath).Replace('\\', '/');
    }

    private static bool IsWithinRoot(string filePath, string sourceRoot)
    {
        var relativePath = Path.GetRelativePath(
            Path.GetFullPath(sourceRoot),
            Path.GetFullPath(filePath));
        return !Path.IsPathRooted(relativePath)
            && !string.Equals(relativePath, "..", StringComparison.Ordinal)
            && !relativePath.StartsWith(
                ".." + Path.DirectorySeparatorChar,
                StringComparison.Ordinal);
    }

    private static string PathToDirectoryUri(string sourceRoot) =>
        new Uri(Path.TrimEndingDirectorySeparator(sourceRoot) + Path.DirectorySeparatorChar)
            .AbsoluteUri;

    private static string CreateFingerprint(LoggerUsageFinding finding, string relativePath)
    {
        var conflicts = string.Join(
            "\n",
            finding.Conflicts.Select(conflict => $"{conflict.Name}\u001f{conflict.Type}"));
        var canonicalValue = string.Join(
            "\n",
            FingerprintName,
            finding.Rule.Id,
            relativePath,
            finding.MessageTemplate ?? string.Empty,
            finding.ParameterName,
            finding.ParameterType ?? string.Empty,
            conflicts);

        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonicalValue)))
            .ToLowerInvariant();
    }

    private sealed record SarifLog(
        [property: JsonPropertyName("$schema")] string Schema,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("runs")] IReadOnlyList<SarifRun> Runs);

    private sealed record SarifRun(
        [property: JsonPropertyName("tool")] SarifTool Tool,
        [property: JsonPropertyName("originalUriBaseIds")] IReadOnlyDictionary<string, SarifArtifactLocation>? OriginalUriBaseIds,
        [property: JsonPropertyName("results")] IReadOnlyList<SarifResult> Results);

    private sealed record SarifTool(
        [property: JsonPropertyName("driver")] SarifDriver Driver);

    private sealed record SarifDriver(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("rules")] IReadOnlyList<SarifReportingDescriptor> Rules);

    private sealed record SarifReportingDescriptor(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("shortDescription")] SarifMessage ShortDescription,
        [property: JsonPropertyName("fullDescription")] SarifMessage FullDescription,
        [property: JsonPropertyName("helpUri")] string HelpUri,
        [property: JsonPropertyName("defaultConfiguration")] SarifConfiguration DefaultConfiguration);

    private sealed record SarifConfiguration(
        [property: JsonPropertyName("level")] string Level);

    private sealed record SarifResult(
        [property: JsonPropertyName("ruleId")] string RuleId,
        [property: JsonPropertyName("level")] string Level,
        [property: JsonPropertyName("message")] SarifMessage Message,
        [property: JsonPropertyName("locations")] IReadOnlyList<SarifLocation> Locations,
        [property: JsonPropertyName("partialFingerprints")] IReadOnlyDictionary<string, string> PartialFingerprints);

    private sealed record SarifMessage(
        [property: JsonPropertyName("text")] string Text);

    private sealed record SarifLocation(
        [property: JsonPropertyName("physicalLocation")] SarifPhysicalLocation PhysicalLocation);

    private sealed record SarifPhysicalLocation(
        [property: JsonPropertyName("artifactLocation")] SarifArtifactLocation ArtifactLocation,
        [property: JsonPropertyName("region")] SarifRegion Region);

    private sealed record SarifArtifactLocation(
        [property: JsonPropertyName("uri")] string Uri,
        [property: JsonPropertyName("uriBaseId")] string? UriBaseId);

    private sealed record SarifRegion(
        [property: JsonPropertyName("startLine")] int StartLine,
        [property: JsonPropertyName("endLine")] int EndLine);
}
