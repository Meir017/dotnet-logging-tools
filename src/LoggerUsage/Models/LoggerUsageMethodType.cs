namespace LoggerUsage.Models;

/// <summary>
/// Defines the different types of logger method usages that can be detected.
/// </summary>
public enum LoggerUsageMethodType
{
    /// <summary>
    /// Direct logger method calls (e.g., ILogger.Log).
    /// </summary>
    LoggerMethod,

    /// <summary>
    /// Logger extension method calls (e.g., LogInformation, LogError).
    /// </summary>
    LoggerExtensions,

    /// <summary>
    /// Methods decorated with LoggerMessage attribute for source generation.
    /// </summary>
    LoggerMessageAttribute,

    /// <summary>
    /// Methods using LoggerMessage.Define for high-performance logging.
    /// </summary>
    LoggerMessageDefine,

    /// <summary>
    /// Logger scope creation calls (e.g., BeginScope).
    /// </summary>
    BeginScope
}