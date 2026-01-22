namespace Hex1b.Widgets;

/// <summary>
/// Specifies the direction a drawer expands toward when opened.
/// </summary>
public enum DrawerDirection
{
    /// <summary>
    /// Drawer expands rightward (typically first child in HStack).
    /// </summary>
    Right,
    
    /// <summary>
    /// Drawer expands leftward (typically last child in HStack).
    /// </summary>
    Left,
    
    /// <summary>
    /// Drawer expands downward (typically first child in VStack).
    /// </summary>
    Down,
    
    /// <summary>
    /// Drawer expands upward (typically last child in VStack).
    /// </summary>
    Up
}
