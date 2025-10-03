namespace LoggerUsage.Models;

/// <summary>
/// Represents the classification level for data compliance and redaction purposes.
/// </summary>
public enum DataClassificationLevel
{
    /// <summary>
    /// Classification level is unknown or could not be determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// No classification specified.
    /// </summary>
    None = 1,

    /// <summary>
    /// Public data that can be freely shared.
    /// </summary>
    Public = 2,

    /// <summary>
    /// Internal data for organization use only.
    /// </summary>
    Internal = 3,

    /// <summary>
    /// Private data with restricted access.
    /// </summary>
    Private = 4,

    /// <summary>
    /// Sensitive data requiring special protection.
    /// </summary>
    Sensitive = 5,

    /// <summary>
    /// Custom classification level defined by user.
    /// </summary>
    Custom = 6
}
