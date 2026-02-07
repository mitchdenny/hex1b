namespace Hex1b;

/// <summary>
/// Interface for nodes that can host floating windows.
/// Implemented by WindowPanelNode to allow window discovery from anywhere in the tree.
/// </summary>
public interface IWindowHost
{
    /// <summary>
    /// The window manager for this host.
    /// </summary>
    WindowManager Windows { get; }
}
