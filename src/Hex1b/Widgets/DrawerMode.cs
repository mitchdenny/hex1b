namespace Hex1b.Widgets;

/// <summary>
/// Specifies the rendering mode for an expanded drawer.
/// </summary>
public enum DrawerMode
{
    /// <summary>
    /// Drawer content is rendered inline, pushing adjacent content.
    /// Similar to a docked sidebar in VS Code.
    /// </summary>
    Inline,
    
    /// <summary>
    /// Drawer content floats above other content as an overlay.
    /// Similar to a mobile hamburger menu or slide-out panel.
    /// </summary>
    Overlay
}
