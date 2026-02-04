namespace Hex1b.Widgets;

/// <summary>
/// Specifies the position of tabs in a TabPanel.
/// </summary>
public enum TabPosition
{
    /// <summary>
    /// Auto-detect based on position in parent VStack.
    /// First child = Top, Last child = Bottom, otherwise Top.
    /// </summary>
    Auto,

    /// <summary>
    /// Tabs are positioned at the top of the panel.
    /// </summary>
    Top,

    /// <summary>
    /// Tabs are positioned at the bottom of the panel.
    /// </summary>
    Bottom
}
