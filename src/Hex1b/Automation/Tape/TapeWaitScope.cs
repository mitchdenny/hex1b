namespace Hex1b.Automation;

/// <summary>Identifies the terminal text examined by a Wait instruction.</summary>
public enum TapeWaitScope
{
    /// <summary>The current cursor line.</summary>
    Line,
    /// <summary>The visible terminal screen.</summary>
    Screen
}
