namespace LoggerUsage.Models;

/// <summary>
/// Represents the classification level for data compliance and redaction purposes.
/// </summary>
public enum DataClassificationLevel
{
    /// <summary>
    /// Classification level is unknown or could not be determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// No classification specified.
    /// </summary>
    None,

    /// <summary>
    /// Public data that can be freely shared.
    /// </summary>
    Public,

    /// <summary>
    /// Internal data for organization use only.
    /// </summary>
    Internal,

    /// <summary>
    /// Private data with restricted access.
    /// </summary>
    Private,

    /// <summary>
    /// Sensitive data requiring special protection.
    /// </summary>
    Sensitive,

    /// <summary>
    /// Custom classification level defined by user.
    /// </summary>
    Custom
}
