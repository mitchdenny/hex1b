namespace Hex1b;

/// <summary>
/// Configuration options for creating a window.
/// </summary>
/// <remarks>
/// <para>
/// WindowOptions provides configuration for window behavior and appearance.
/// All properties have sensible defaults, so you only need to specify what you want to customize.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Simple window with defaults
/// e.Windows.Open("my-window", "My Window", () => content);
/// 
/// // Window with custom options
/// var options = new WindowOptions
/// {
///     Width = 60,
///     Height = 20,
///     IsResizable = true,
///     RightTitleBarActions = [WindowAction.Close()]
/// };
/// 
/// e.Windows.Open("settings", "Settings", () => content, options);
/// </code>
/// </example>
public sealed class WindowOptions
{
    /// <summary>
    /// Initial width of the window. Defaults to 40.
    /// </summary>
    public int Width { get; init; } = 40;

    /// <summary>
    /// Initial height of the window. Defaults to 15.
    /// </summary>
    public int Height { get; init; } = 15;

    /// <summary>
    /// Initial X position. Null uses the <see cref="Position"/> specification.
    /// </summary>
    public int? X { get; init; }

    /// <summary>
    /// Initial Y position. Null uses the <see cref="Position"/> specification.
    /// </summary>
    public int? Y { get; init; }

    /// <summary>
    /// Positioning strategy when X/Y are null. Defaults to Center.
    /// </summary>
    public WindowPositionSpec Position { get; init; } = new(WindowPosition.Center);

    /// <summary>
    /// Whether this is a modal window that blocks interaction with other windows.
    /// Defaults to false.
    /// </summary>
    public bool IsModal { get; init; }

    /// <summary>
    /// Whether the window can be resized by dragging edges. Defaults to false.
    /// </summary>
    public bool IsResizable { get; init; }

    /// <summary>
    /// Minimum width for resize operations. Defaults to 10.
    /// </summary>
    public int MinWidth { get; init; } = 10;

    /// <summary>
    /// Minimum height for resize operations. Defaults to 5.
    /// </summary>
    public int MinHeight { get; init; } = 5;

    /// <summary>
    /// Maximum width for resize operations. Null means unbounded.
    /// </summary>
    public int? MaxWidth { get; init; }

    /// <summary>
    /// Maximum height for resize operations. Null means unbounded.
    /// </summary>
    public int? MaxHeight { get; init; }

    /// <summary>
    /// Whether this window can be moved outside the panel bounds.
    /// Defaults to false.
    /// </summary>
    public bool AllowOutOfBounds { get; init; }

    /// <summary>
    /// Callback invoked when the window is closed.
    /// </summary>
    public Action? OnClose { get; init; }

    /// <summary>
    /// Callback invoked when the window becomes active (brought to front).
    /// </summary>
    public Action? OnActivated { get; init; }

    /// <summary>
    /// Callback invoked when the window loses active status.
    /// </summary>
    public Action? OnDeactivated { get; init; }

    /// <summary>
    /// Whether to show the title bar. Defaults to true.
    /// </summary>
    public bool ShowTitleBar { get; init; } = true;

    /// <summary>
    /// Actions displayed on the left side of the title bar (after the border).
    /// </summary>
    public IReadOnlyList<WindowAction>? LeftTitleBarActions { get; init; }

    /// <summary>
    /// Actions displayed on the right side of the title bar (before the border).
    /// If null and ShowTitleBar is true, defaults to a close button.
    /// </summary>
    public IReadOnlyList<WindowAction>? RightTitleBarActions { get; init; }

    /// <summary>
    /// How Escape key is handled. Defaults to Close.
    /// </summary>
    public WindowEscapeBehavior EscapeBehavior { get; init; } = WindowEscapeBehavior.Close;

    /// <summary>
    /// Standard options for a simple window with close button.
    /// </summary>
    public static WindowOptions Default => new();

    /// <summary>
    /// Options for a dialog window (modal with close button).
    /// </summary>
    public static WindowOptions Dialog => new()
    {
        IsModal = true
    };
}
