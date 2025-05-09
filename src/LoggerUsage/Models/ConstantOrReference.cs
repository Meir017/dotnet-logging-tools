namespace LoggerUsage.Models;

public record ConstantOrReference(string Kind, object? Value)
{
    public static ConstantOrReference Missing => new("Missing", null);
    public static ConstantOrReference Constant(object value) => new("Constant", value);
}
