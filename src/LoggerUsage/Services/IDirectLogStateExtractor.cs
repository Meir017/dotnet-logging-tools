using LoggerUsage.Models;
using Microsoft.CodeAnalysis;

namespace LoggerUsage.Services;

internal interface IDirectLogStateExtractor
{
    bool TryExtract(
        IOperation state,
        LoggingTypes loggingTypes,
        out string? messageTemplate,
        out List<MessageParameter> messageParameters);
}
