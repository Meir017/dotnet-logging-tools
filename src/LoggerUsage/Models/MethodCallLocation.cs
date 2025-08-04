namespace LoggerUsage.Models;

/// <summary>
/// Contains location information for where a logger method call was made in the source code.
/// </summary>
public class MethodCallLocation
{
    /// <summary>
    /// Gets or sets the file path where the method call is located.
    /// </summary>
    public required string FilePath { get; set; }
    
    /// <summary>
    /// Gets or sets the starting line number of the method call.
    /// </summary>
    public required int StartLineNumber { get; set; }
    
    /// <summary>
    /// Gets or sets the ending line number of the method call.
    /// </summary>
    public required int EndLineNumber { get; set; }
}
