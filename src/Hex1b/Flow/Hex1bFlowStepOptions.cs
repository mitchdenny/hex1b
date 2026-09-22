using Hex1b.Widgets;

namespace Hex1b.Flow;

/// <summary>
/// Options for configuring an inline flow step.
/// </summary>
public sealed class Hex1bFlowStepOptions
{
    /// <summary>
    /// Maximum height in rows for the step. If null, defaults to terminal height.
    /// </summary>
    public int? MaxHeight { get; set; }

    /// <summary>
    /// Whether to enable mouse input for this step. Defaults to false.
    /// </summary>
    public bool EnableMouse { get; set; }
}
