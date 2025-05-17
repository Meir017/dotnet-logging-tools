namespace LoggerUsage.Models;

public class MethodCallLocation
{
    public required string FilePath { get; set; }
    public required int StartLineNumber { get; set; }
    public required int EndLineNumber { get; set; }
}
