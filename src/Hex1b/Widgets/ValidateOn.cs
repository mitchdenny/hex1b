namespace Hex1b.Widgets;

/// <summary>
/// Specifies when field validation should be triggered.
/// </summary>
public enum ValidateOn
{
    /// <summary>
    /// Validate on every text change (default).
    /// </summary>
    Change,
    
    /// <summary>
    /// Validate when the field loses focus.
    /// </summary>
    Blur
}
