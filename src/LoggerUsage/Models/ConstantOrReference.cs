namespace LoggerUsage.Models;

/// <summary>
/// Represents a value that can be either a constant value or a reference to a variable.
/// </summary>
/// <param name="Kind">The kind of the value (e.g., "Constant", "Missing").</param>
/// <param name="Value">The actual value, which can be null.</param>
public record ConstantOrReference(string Kind, object? Value)
{
    /// <summary>
    /// Gets a singleton instance representing a missing value.
    /// </summary>
    public static ConstantOrReference Missing { get; } = new("Missing", null);
    
    /// <summary>
    /// Creates a new instance representing a constant value.
    /// </summary>
    /// <param name="value">The constant value.</param>
    /// <returns>A new <see cref="ConstantOrReference"/> instance.</returns>
    public static ConstantOrReference Constant(object value) => new("Constant", value);
}
