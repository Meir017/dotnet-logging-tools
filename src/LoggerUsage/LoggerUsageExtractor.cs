using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;

namespace LoggerUsage
{
    public class LoggerUsageInfo
    {
        public required string MethodName { get; set; }
        public string? MessageTemplate { get; set; }
        public LogLevel LogLevel { get; set; }
        public string? EventId { get; set; }
        public List<MessageParameter> MessageParameters { get; set; } = new();
        public required Location Location { get; set; }
    }

    public class MessageParameter
    {
        public required string Name { get; set; }
        public string? Type { get; set; }
        public string? Value { get; set; }
    }

    public class LoggerUsageExtractor
    {
        public async Task<List<LoggerUsageInfo>> ExtractLoggerUsages(Project project)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null) return [];

            var loggerInterface = compilation.GetTypeByMetadataName(typeof(ILogger).FullName!)!;
            if (loggerInterface == null) return [];

            var loggingTypes = new LoggingTypes(compilation, loggerInterface);
            if (loggingTypes.ILogger == null) return [];

            var results = new List<LoggerUsageInfo>();
            foreach (var document in project.Documents)
            {
                var root = await document.GetSyntaxRootAsync();
                var semanticModel = await document.GetSemanticModelAsync();

                if (root == null || semanticModel == null) continue;

                // Fallback: walk the tree for invocations
                var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var invocation in invocations)
                {
                    if (semanticModel.GetOperation(invocation) is not IInvocationOperation operation) continue;

                    var method = operation.TargetMethod;
                    if (!loggingTypes.LoggerExtensionModeler.IsLoggerMethod(method)) continue;

                    var usage = new LoggerUsageInfo
                    {
                        MethodName = method.Name,
                        Location = invocation.GetLocation(),
                        MessageTemplate = ExtractMessageTemplate(operation)
                    };
                    if (TryExtractEventId(operation, out var eventId))
                    {
                        usage.EventId = eventId;
                    }
                    if (TryExtractLogLevel(operation, out var logLevel))
                    {
                        usage.LogLevel = logLevel;
                    }
                    usage.MessageParameters = ExtractArguments(operation);

                    results.Add(usage);
                }
            }
            return results;
        }

        private static string? ExtractMessageTemplate(IInvocationOperation operation)
        {
            foreach (var arg in operation.Arguments)
            {
                if (arg.Parameter?.Name?.Equals("message") == true)
                {
                    return arg.Value.ConstantValue.HasValue ? arg.Value.ConstantValue.Value?.ToString() : null;
                }
            }
            return null;
        }

        private static bool TryExtractEventId(IInvocationOperation operation, out string eventId)
        {
            foreach (var arg in operation.Arguments)
            {
                if (arg.Parameter?.Type?.Name == "EventId")
                {
                    eventId = arg.Value.Syntax.ToString();
                    return true;
                }
            }
            eventId = null!;
            return false;
        }

        private bool TryExtractLogLevel(IInvocationOperation operation, out LogLevel logLevel)
        {
            foreach (var arg in operation.Arguments)
            {
                if (arg.Parameter?.Type?.Name == "LogLevel")
                {
                    logLevel = (LogLevel)arg.Value.ConstantValue.Value!;
                    return true;
                }
            }
            logLevel = default;
            return false;
        }

        private static List<MessageParameter> ExtractArguments(IInvocationOperation operation)
        {
            var args = new List<MessageParameter>();
            foreach (var arg in operation.Arguments)
            {
                if (arg.Parameter?.Name != null && !arg.Parameter.Name.ToLowerInvariant().Contains("message") && arg.Parameter.Type?.Name != "EventId")
                {
                    args.Add(new MessageParameter
                    {
                        Name = arg.Parameter.Name,
                        Type = arg.Parameter.Type?.Name,
                        Value = arg.Value.ConstantValue.HasValue ? arg.Value.ConstantValue.Value?.ToString() : null
                    });
                }
            }
            return args;
        }
    }
}
