using LoggerUsage.Models;
using LoggerUsage.VSCode.Bridge.Models;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.VSCode.Bridge;

/// <summary>
/// Maps LoggerUsageInfo from the LoggerUsage library to LoggingInsightDto for the VS Code extension
/// and detects logging inconsistencies
/// </summary>
public class LoggerUsageMapper
{
    /// <summary>
    /// Convert LoggerUsageInfo to LoggingInsightDto with inconsistency detection
    /// </summary>
    public static LoggingInsightDto ToDto(LoggerUsageInfo usage)
    {
        var location = MapLocation(usage.Location);
        var id = GenerateInsightId(location);
        var inconsistencies = DetectInconsistencies(usage);

        // Extract data classifications from parameters
        var dataClassifications = usage.MessageParameters
            .Where(p => p.DataClassification != null)
            .Select(p => new { p.Name, Classification = p.DataClassification! })
            .Select(x => MapDataClassification(x.Name, x.Classification))
            .ToList();

        return new LoggingInsightDto
        {
            Id = id,
            MethodType = MapMethodType(usage.MethodType),
            MessageTemplate = usage.MessageTemplate ?? string.Empty,
            LogLevel = usage.LogLevel?.ToString(),
            EventId = MapEventId(usage.EventId),
            Parameters = [.. usage.MessageParameters.Select(p => p.Name)],
            Location = location,
            Tags = [], // Tags will be populated from other sources if needed
            DataClassifications = dataClassifications,
            HasInconsistencies = inconsistencies.Count > 0,
            Inconsistencies = inconsistencies.Count > 0 ? inconsistencies : null
        };
    }

    /// <summary>
    /// Generate unique insight ID from location
    /// </summary>
    private static string GenerateInsightId(LocationDto location)
    {
        return $"{location.FilePath}:{location.StartLine}:{location.StartColumn}";
    }

    /// <summary>
    /// Map location from LoggerUsage model to DTO
    /// </summary>
    private static LocationDto MapLocation(MethodCallLocation location)
    {
        return new LocationDto
        {
            FilePath = location.FilePath,
            StartLine = location.StartLineNumber,
            StartColumn = 0, // MethodCallLocation doesn't have column info
            EndLine = location.EndLineNumber,
            EndColumn = 0
        };
    }

    /// <summary>
    /// Map EventId to DTO
    /// </summary>
    private static EventIdDto? MapEventId(EventIdBase? eventId)
    {
        if (eventId == null)
        {
            return null;
        }

        // EventIdBase is polymorphic - could be EventIdDetails or EventIdRef
        if (eventId is EventIdDetails details)
        {
            return new EventIdDto
            {
                Id = details.Id.Value as int?,
                Name = details.Name.Value as string
            };
        }

        // For EventIdRef, we can't resolve the actual values
        return null;
    }

    /// <summary>
    /// Map data classification to DTO
    /// </summary>
    private static DataClassificationDto MapDataClassification(string parameterName, DataClassificationInfo info)
    {
        return new DataClassificationDto
        {
            ParameterName = parameterName,
            ClassificationType = info.ClassificationTypeName
        };
    }

    /// <summary>
    /// Map method type enum to string
    /// </summary>
    private static string MapMethodType(LoggerUsageMethodType methodType)
    {
        return methodType switch
        {
            LoggerUsageMethodType.LoggerMethod => "LoggerMethod",
            LoggerUsageMethodType.LoggerExtensions => "LoggerExtension",
            LoggerUsageMethodType.LoggerMessageAttribute => "LoggerMessageAttribute",
            LoggerUsageMethodType.LoggerMessageDefine => "LoggerMessageDefine",
            LoggerUsageMethodType.BeginScope => "BeginScope",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Detect inconsistencies in logging usage
    /// </summary>
    private static List<ParameterInconsistencyDto> DetectInconsistencies(LoggerUsageInfo usage)
    {
        var inconsistencies = new List<ParameterInconsistencyDto>();

        // 1. Detect parameter name mismatches
        var templateParams = ExtractTemplateParameters(usage.MessageTemplate);
        var actualParams = usage.MessageParameters
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var templateParam in templateParams)
        {
            if (!actualParams.Contains(templateParam))
            {
                inconsistencies.Add(new ParameterInconsistencyDto
                {
                    Type = "NameMismatch",
                    Message = $"Template parameter '{{{templateParam}}}' does not match any method parameter name",
                    Severity = "Warning",
                    Location = MapLocation(usage.Location)
                });
            }
        }

        // 2. Detect missing EventIds
        if (usage.EventId == null && usage.LogLevel != LogLevel.Trace && usage.LogLevel != LogLevel.Debug)
        {
            inconsistencies.Add(new ParameterInconsistencyDto
            {
                Type = "MissingEventId",
                Message = "Log statement is missing an EventId. Consider adding one for better filtering and monitoring.",
                Severity = "Warning",
                Location = MapLocation(usage.Location)
            });
        }

        // 3. Detect sensitive data in logs
        var parametersWithClassifications = usage.MessageParameters
            .Where(p => p.DataClassification != null)
            .ToList();

        if (parametersWithClassifications.Any())
        {
            foreach (var param in parametersWithClassifications)
            {
                inconsistencies.Add(new ParameterInconsistencyDto
                {
                    Type = "SensitiveDataInLog",
                    Message = $"Parameter '{param.Name}' is marked as '{param.DataClassification!.ClassificationTypeName}' and may contain sensitive data",
                    Severity = "Warning",
                    Location = MapLocation(usage.Location)
                });
            }
        }

        return inconsistencies;
    }

    /// <summary>
    /// Extract parameter names from message template
    /// </summary>
    private static HashSet<string> ExtractTemplateParameters(string? messageTemplate)
    {
        if (string.IsNullOrWhiteSpace(messageTemplate))
        {
            return [];
        }

        var parameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var template = messageTemplate;
        var startIndex = 0;

        while (startIndex < template.Length)
        {
            var openBrace = template.IndexOf('{', startIndex);
            if (openBrace == -1)
            {
                break;
            }

            var closeBrace = template.IndexOf('}', openBrace);
            if (closeBrace == -1)
            {
                break;
            }

            var paramName = template.Substring(openBrace + 1, closeBrace - openBrace - 1);

            // Remove format specifiers (e.g., {name:000} -> name)
            var colonIndex = paramName.IndexOf(':');
            if (colonIndex != -1)
            {
                paramName = paramName.Substring(0, colonIndex);
            }

            // Remove alignment specifiers (e.g., {name,10} -> name)
            var commaIndex = paramName.IndexOf(',');
            if (commaIndex != -1)
            {
                paramName = paramName.Substring(0, commaIndex);
            }

            paramName = paramName.Trim();

            if (!string.IsNullOrWhiteSpace(paramName))
            {
                parameters.Add(paramName);
            }

            startIndex = closeBrace + 1;
        }

        return parameters;
    }
}
