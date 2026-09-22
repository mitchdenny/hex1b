using System.Runtime.CompilerServices;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Represents a managed window entry with its state.
/// </summary>
public sealed class WindowEntry
{
    internal WindowEntry(
        WindowManager manager,
        string id,
        string title,
        Func<WidgetContext<Hex1bWidget>, Hex1bWidget> contentBuilder,
        int width,
        int height,
        int? x,
        int? y,
        WindowPositionSpec positionSpec,
        bool isModal,
        bool isResizable,
        int minWidth,
        int minHeight,
        int? maxWidth,
        int? maxHeight,
        Action? onClose,
        Action? onActivated,
        Action? onDeactivated,
        Action<WindowEntry>? resultInvoker,
        bool showTitleBar,
        IReadOnlyList<WindowAction> leftTitleBarActions,
        IReadOnlyList<WindowAction> rightTitleBarActions,
        WindowEscapeBehavior escapeBehavior,
        int zIndex,
        bool allowOutOfBounds)
    {
        Manager = manager;
        Id = id;
        Title = title;
        ContentBuilder = contentBuilder;
        Width = width;
        Height = height;
        X = x;
        Y = y;
        PositionSpec = positionSpec;
        IsModal = isModal;
        IsResizable = isResizable;
        MinWidth = minWidth;
        MinHeight = minHeight;
        MaxWidth = maxWidth;
        MaxHeight = maxHeight;
        OnClose = onClose;
        OnActivated = onActivated;
        OnDeactivated = onDeactivated;
        ResultInvoker = resultInvoker;
        ShowTitleBar = showTitleBar;
        LeftTitleBarActions = leftTitleBarActions;
        RightTitleBarActions = rightTitleBarActions;
        EscapeBehavior = escapeBehavior;
        ZIndex = zIndex;
        AllowOutOfBounds = allowOutOfBounds;
    }

    internal WindowManager Manager { get; }

    /// <summary>
    /// The window handle for this entry.
    /// </summary>
    internal WindowHandle? Handle { get; set; }

    /// <summary>
    /// Internal identifier for this window.
    /// </summary>
    internal string Id { get; }

    /// <summary>
    /// The window title displayed in the title bar.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Builder function for the window content.
    /// </summary>
    internal Func<WidgetContext<Hex1bWidget>, Hex1bWidget> ContentBuilder { get; }

    /// <summary>
    /// Current width of the window.
    /// </summary>
    public int Width { get; internal set; }

    /// <summary>
    /// Current height of the window.
    /// </summary>
    public int Height { get; internal set; }

    /// <summary>
    /// Current X position (null = use PositionSpec).
    /// </summary>
    public int? X { get; internal set; }

    /// <summary>
    /// Current Y position (null = use PositionSpec).
    /// </summary>
    public int? Y { get; internal set; }

    /// <summary>
    /// The positioning specification for initial placement.
    /// Used when X and Y are null.
    /// </summary>
    public WindowPositionSpec PositionSpec { get; }

    /// <summary>
    /// Whether this is a modal window.
    /// </summary>
    public bool IsModal { get; }

    /// <summary>
    /// Whether this window can be resized.
    /// </summary>
    public bool IsResizable { get; }

    /// <summary>
    /// Minimum width for resize operations.
    /// </summary>
    public int MinWidth { get; }

    /// <summary>
    /// Minimum height for resize operations.
    /// </summary>
    public int MinHeight { get; }

    /// <summary>
    /// Maximum width for resize operations. Null means unbounded.
    /// </summary>
    public int? MaxWidth { get; }

    /// <summary>
    /// Maximum height for resize operations. Null means unbounded.
    /// </summary>
    public int? MaxHeight { get; }

    /// <summary>
    /// Whether this window can be moved outside the panel bounds.
    /// </summary>
    public bool AllowOutOfBounds { get; }

    /// <summary>
    /// Callback invoked when the window is closed.
    /// </summary>
    internal Action? OnClose { get; }

    /// <summary>
    /// Callback invoked when the window becomes active (brought to front).
    /// </summary>
    internal Action? OnActivated { get; }

    /// <summary>
    /// Callback invoked when the window loses active status.
    /// </summary>
    internal Action? OnDeactivated { get; }

    /// <summary>
    /// Whether the title bar is displayed.
    /// </summary>
    public bool ShowTitleBar { get; }

    /// <summary>
    /// Actions displayed on the left side of the title bar.
    /// </summary>
    public IReadOnlyList<WindowAction> LeftTitleBarActions { get; }

    /// <summary>
    /// Actions displayed on the right side of the title bar.
    /// </summary>
    public IReadOnlyList<WindowAction> RightTitleBarActions { get; }

    /// <summary>
    /// How Escape key is handled for this window.
    /// </summary>
    public WindowEscapeBehavior EscapeBehavior { get; }

    /// <summary>
    /// Z-order index (higher = on top).
    /// </summary>
    public int ZIndex { get; internal set; }

    /// <summary>
    /// The reconciled window node. Set by WindowPanelNode during reconciliation.
    /// </summary>
    internal WindowNode? Node { get; set; }

    /// <summary>
    /// Delegate that invokes the typed result callback without reflection.
    /// Captured at registration time by <see cref="WindowHandle.OnResult{T}"/>.
    /// </summary>
    internal Action<WindowEntry>? ResultInvoker { get; }

    /// <summary>
    /// The result value set by CloseWithResult. Null if cancelled.
    /// </summary>
    internal object? ResultValue { get; private set; }

    /// <summary>
    /// Whether a result was explicitly provided via CloseWithResult.
    /// </summary>
    internal bool ResultProvided { get; private set; }

    /// <summary>
    /// Closes this window.
    /// </summary>
    public void Close() => Manager.Close(this);

    /// <summary>
    /// Closes this modal window with a typed result value.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="result">The result value to return.</param>
    public void CloseWithResult<TResult>(TResult result)
    {
        ResultValue = result;
        ResultProvided = true;
        Manager.Close(this);
    }

    /// <summary>
    /// Internal method to invoke the result callback after the window is closed.
    /// Called by WindowManager.Close.
    /// </summary>
    internal void InvokeResultCallback()
    {
        ResultInvoker?.Invoke(this);
    }

    /// <summary>
    /// Brings this window to the front.
    /// </summary>
    public void BringToFront() => Manager.BringToFront(this);
}
