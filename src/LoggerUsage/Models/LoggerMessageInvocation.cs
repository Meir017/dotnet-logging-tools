namespace LoggerUsage.Models;

/// <summary>
/// Represents an invocation/call site of a LoggerMessage-attributed method.
/// </summary>
public class LoggerMessageInvocation
{
    /// <summary>
    /// The fully qualified name of the containing type where the invocation occurs.
    /// </summary>
    public required string ContainingType { get; set; }

    /// <summary>
    /// Location where the method was invoked.
    /// </summary>
    public required MethodCallLocation InvocationLocation { get; set; }

    /// <summary>
    /// Arguments passed to the invocation (reusing existing MessageParameter model).
    /// </summary>
    public List<MessageParameter> Arguments { get; set; } = [];
}
