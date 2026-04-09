namespace Hex1b.Widgets;

/// <summary>
/// Represents the result of a field validation.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>
    /// Whether the field value is valid.
    /// </summary>
    public bool IsValid { get; }
    
    /// <summary>
    /// The error message if validation failed, or null if valid.
    /// </summary>
    public string? ErrorMessage { get; }
    
    private ValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }
    
    /// <summary>
    /// A successful validation result with no error.
    /// </summary>
    public static readonly ValidationResult Valid = new(true, null);
    
    /// <summary>
    /// Creates a failed validation result with the specified error message.
    /// </summary>
    public static ValidationResult Error(string message) => new(false, message);
}
