namespace Hex1b;

/// <summary>
/// Specifies the shape of the terminal cursor.
/// </summary>
public enum CursorShape
{
    /// <summary>Use the terminal's default cursor shape.</summary>
    Default = 0,
    /// <summary>Blinking block cursor (▓).</summary>
    BlinkingBlock = 1,
    /// <summary>Steady block cursor (█).</summary>
    SteadyBlock = 2,
    /// <summary>Blinking underline cursor (_).</summary>
    BlinkingUnderline = 3,
    /// <summary>Steady underline cursor (_).</summary>
    SteadyUnderline = 4,
    /// <summary>Blinking bar cursor (│).</summary>
    BlinkingBar = 5,
    /// <summary>Steady bar cursor (│).</summary>
    SteadyBar = 6
}
