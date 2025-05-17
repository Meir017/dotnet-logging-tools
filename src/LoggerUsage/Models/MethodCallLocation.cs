namespace LoggerUsage.Models;

public class MethodCallLocation
{
    public required string FilePath { get; set; }
    public required int LineNumber { get; set; }
    public required int ColumnNumber { get; set; }
}
