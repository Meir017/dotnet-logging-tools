# SARIF 2.1.0 Contract

## Document

- `$schema`: `https://json.schemastore.org/sarif-2.1.0.json`
- `version`: `2.1.0`
- Exactly one run.
- Tool driver name: `LoggerUsage`.
- Rule array ordered by rule ID.
- Result array ordered by normalized path, start line, end line, rule ID, parameter name, and parameter type.
- No generation timestamp or absolute machine path.

## Rules

### LUT001

- Name: `ParameterTypeMismatch`
- Default level: `warning`
- Meaning: the same case-sensitive logging parameter name is associated with more than one extracted source type.
- Remediation: use one semantic type for the parameter or rename parameters that represent different concepts.

### LUT002

- Name: `ParameterCasingInconsistency`
- Default level: `note`
- Meaning: logging parameter names differ only by casing.
- Remediation: choose one canonical spelling and casing.

## Result

Each result contains:

```json
{
  "ruleId": "LUT001",
  "level": "warning",
  "message": {
    "text": "Logging parameter 'userId' is used with conflicting types: int, string."
  },
  "locations": [
    {
      "physicalLocation": {
        "artifactLocation": {
          "uri": "src/Service.cs",
          "uriBaseId": "%SRCROOT%"
        },
        "region": {
          "startLine": 42,
          "endLine": 42
        }
      }
    }
  ],
  "partialFingerprints": {
    "loggerUsage/v1": "<lowercase-sha256>"
  }
}
```

## Path rules

- Resolve the absolute extracted file path against `ReportGenerationContext.SourceRoot`.
- Use a relative URI with `/` separators.
- Reject or explicitly fall back for paths outside the source root; never emit `..` traversal in an artifact URI.
- Preserve path case as stored, but use a normalized comparison form for sorting and deduplication.

## Fingerprint rules

Canonical input uses UTF-8 with `\n` delimiters:

```text
loggerUsage/v1
<ruleId>
<normalized-relative-path>
<normalized-message-template>
<parameter-name>
<parameter-type-or-empty>
<sorted-conflict-name-and-type-pairs>
```

Hash with SHA-256 and encode as lowercase hexadecimal. Line numbers, timestamps, absolute paths, and result-list indexes are forbidden inputs.

## Validation

1. Parse as JSON with no duplicate properties.
2. Validate against SARIF 2.1.0 schema.
3. Assert rule references resolve to tool driver rules.
4. Assert all locations use positive one-based lines.
5. Assert all source-root artifact URIs are relative and contain no backslashes.
6. Generate twice from shuffled equivalent inputs and compare bytes.
7. Upload the fixture SARIF to GitHub code scanning twice and confirm alerts update rather than duplicate.

