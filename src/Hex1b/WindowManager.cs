using System.Runtime.CompilerServices;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Manages floating windows within a <see cref="WindowPanelNode"/>.
/// Handles window registration, z-ordering, and modal window stacking.
/// </summary>
/// <remarks>
/// <para>
/// The window manager is the central coordinator for all floating windows in an application.
/// It tracks:
/// <list type="bullet">
///   <item><description>All open windows and their z-order</description></item>
///   <item><description>The currently active (focused) window</description></item>
///   <item><description>Modal window stack for blocking interaction</description></item>
/// </list>
/// </para>
/// <para>
/// Access the window manager from event handlers via <c>e.Context.Windows</c>
/// or through the <see cref="Input.InputBindingActionContext.Windows"/> property.
/// </para>
/// </remarks>
/// <example>
/// <para>Opening a window from a button click:</para>
/// <code>
/// ctx.Button("Open Settings").OnClick(e =&gt; {
///     var window = e.Windows.Window(w =&gt; w.VStack(v =&gt; [
///         v.Text("Settings content here"),
///         v.Button("Close").OnClick(ev =&gt; ev.Windows.Close(w.Window))
///     ]))
///     .Title("Settings")
///     .Size(60, 20);
///     
///     e.Windows.Open(window);
/// });
/// </code>
/// </example>
public sealed class WindowManager
{
    private readonly List<WindowEntry> _entries = [];
    private readonly Dictionary<WindowHandle, WindowEntry> _handleToEntry = new();
    private readonly object _lock = new();
    private int _nextZIndex = 0;

    /// <summary>
    /// Event raised when the window collection or state changes.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Gets all open windows in z-order (bottom to top).
    /// </summary>
    public IReadOnlyList<WindowEntry> All
    {
        get
        {
            lock (_lock)
            {
                return _entries.OrderBy(e => e.ZIndex).ToList();
            }
        }
    }

    /// <summary>
    /// Gets the count of open windows.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Gets the currently active (topmost non-modal or topmost modal) window.
    /// </summary>
    public WindowEntry? ActiveWindow
    {
        get
        {
            lock (_lock)
            {
                // Modal windows take precedence
                var topModal = _entries
                    .Where(e => e.IsModal)
                    .OrderByDescending(e => e.ZIndex)
                    .FirstOrDefault();
                    
                if (topModal != null)
                    return topModal;
                    
                return _entries.OrderByDescending(e => e.ZIndex).FirstOrDefault();
            }
        }
    }

    /// <summary>
    /// Creates a new window handle with the specified content builder.
    /// The window is not opened until <see cref="Open(WindowHandle)"/> is called.
    /// </summary>
    /// <param name="content">Builder function for window content. Receives a <see cref="WindowContentContext{TParentWidget}"/> 
    /// that provides access to the window handle via the <c>Window</c> property.</param>
    /// <returns>A window handle that can be configured with fluent methods and opened.</returns>
    /// <example>
    /// <code>
    /// var window = e.Windows.Window(w =&gt; w.VStack(v =&gt; [
    ///     v.Text("Settings"),
    ///     v.Button("Close").OnClick(ev =&gt; ev.Windows.Close(w.Window))
    /// ]))
    /// .Title("Settings")
    /// .Size(60, 20);
    /// 
    /// e.Windows.Open(window);
    /// </code>
    /// </example>
    public WindowHandle Window(Func<WindowContentContext<Hex1bWidget>, Hex1bWidget> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new WindowHandle(content);
    }

    /// <summary>
    /// Opens a window from a window handle.
    /// If the window is already open, it is brought to front instead.
    /// </summary>
    /// <param name="handle">The window handle created by <see cref="Window"/>.</param>
    /// <returns>The window entry.</returns>
    /// <example>
    /// <code>
    /// var window = e.Windows.Window(w =&gt; w.Text("Hello!"))
    ///     .Title("My Window")
    ///     .Size(40, 15);
    /// 
    /// e.Windows.Open(window);
    /// </code>
    /// </example>
    public WindowEntry Open(WindowHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        lock (_lock)
        {
            // Check if this handle is already open
            if (_handleToEntry.TryGetValue(handle, out var existing))
            {
                BringToFrontInternal(existing);
                return existing;
            }

            // Generate a unique ID based on the handle's identity
            var id = $"__wh_{RuntimeHelpers.GetHashCode(handle)}_{_nextZIndex}";

            var entry = new WindowEntry(
                manager: this,
                id: id,
                title: handle.TitleValue,
                contentBuilder: _ => handle.BuildContent(),
                width: handle.WidthValue,
                height: handle.HeightValue,
                x: handle.XValue,
                y: handle.YValue,
                positionSpec: handle.PositionSpecValue,
                isModal: handle.IsModalValue,
                isResizable: handle.IsResizableValue,
                minWidth: handle.MinWidthValue,
                minHeight: handle.MinHeightValue,
                maxWidth: handle.MaxWidthValue,
                maxHeight: handle.MaxHeightValue,
                onClose: handle.OnCloseValue,
                onActivated: handle.OnActivatedValue,
                onDeactivated: handle.OnDeactivatedValue,
                onResultCallback: handle.OnResultCallbackValue,
                resultType: handle.ResultTypeValue,
                showTitleBar: handle.ShowTitleBarValue,
                leftTitleBarActions: handle.BuildLeftTitleActions(),
                rightTitleBarActions: handle.BuildRightTitleActions(),
                escapeBehavior: handle.EscapeBehaviorValue,
                zIndex: _nextZIndex++,
                allowOutOfBounds: handle.AllowOutOfBoundsValue
            );

            entry.Handle = handle;
            handle.Entry = entry;
            _entries.Add(entry);
            _handleToEntry[handle] = entry;
        }

        Changed?.Invoke();
        return _entries.Last();
    }

    /// <summary>
    /// Closes a window by its entry.
    /// </summary>
    /// <param name="entry">The window entry to close.</param>
    /// <returns>True if the window was found and closed.</returns>
    public bool Close(WindowEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Action? onClose = null;

        lock (_lock)
        {
            if (!_entries.Remove(entry))
            {
                return false;
            }
            
            // Clean up handle mapping if this entry was opened via WindowHandle
            if (entry.Handle != null)
            {
                entry.Handle.Entry = null;
                _handleToEntry.Remove(entry.Handle);
            }
            
            onClose = entry.OnClose;
        }

        onClose?.Invoke();
        
        // Invoke result callback if registered
        entry.InvokeResultCallback();
        
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Closes a window by its handle.
    /// </summary>
    /// <param name="handle">The window handle.</param>
    /// <returns>True if the window was found and closed.</returns>
    public bool Close(WindowHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        WindowEntry? entry;
        
        lock (_lock)
        {
            if (!_handleToEntry.TryGetValue(handle, out entry))
            {
                return false;
            }
        }

        return Close(entry);
    }

    /// <summary>
    /// Closes all windows.
    /// </summary>
    public void CloseAll()
    {
        List<WindowEntry> toClose;

        lock (_lock)
        {
            toClose = [.. _entries];
            _entries.Clear();
            _handleToEntry.Clear();
        }

        foreach (var entry in toClose)
        {
            entry.OnClose?.Invoke();
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Brings a window to the front (highest z-index).
    /// </summary>
    /// <param name="entry">The window to bring to front.</param>
    public void BringToFront(WindowEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_lock)
        {
            if (!_entries.Contains(entry))
                return;
                
            BringToFrontInternal(entry);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Brings a window to the front by its handle.
    /// </summary>
    /// <param name="handle">The window handle.</param>
    public void BringToFront(WindowHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        lock (_lock)
        {
            if (_handleToEntry.TryGetValue(handle, out var entry))
            {
                BringToFrontInternal(entry);
            }
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Gets a window entry by its handle.
    /// </summary>
    /// <param name="handle">The window handle.</param>
    /// <returns>The window entry, or null if not found.</returns>
    public WindowEntry? Get(WindowHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        lock (_lock)
        {
            return _handleToEntry.GetValueOrDefault(handle);
        }
    }

    /// <summary>
    /// Checks if a window with the given handle is open.
    /// </summary>
    /// <param name="handle">The window handle.</param>
    /// <returns>True if the window is open.</returns>
    public bool IsOpen(WindowHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        lock (_lock)
        {
            return _handleToEntry.ContainsKey(handle);
        }
    }

    /// <summary>
    /// Checks if there are any modal windows open.
    /// When modal windows are open, non-modal windows should not receive input.
    /// </summary>
    public bool HasModalWindow
    {
        get
        {
            lock (_lock)
            {
                return _entries.Any(e => e.IsModal);
            }
        }
    }

    private void BringToFrontInternal(WindowEntry entry)
    {
        // Find current active window before changing z-order
        var previousActive = _entries.OrderByDescending(e => e.ZIndex).FirstOrDefault();
        
        // Don't do anything if already at front
        if (ReferenceEquals(previousActive, entry))
            return;
        
        // Update z-index
        entry.ZIndex = _nextZIndex++;
        
        // Fire deactivation on previous active window
        previousActive?.OnDeactivated?.Invoke();
        
        // Fire activation on new active window
        entry.OnActivated?.Invoke();
    }

    /// <summary>
    /// Internal method to update window position. Called by WindowNode during drag.
    /// </summary>
    internal void UpdatePosition(WindowEntry entry, int x, int y)
    {
        entry.X = x;
        entry.Y = y;
        Changed?.Invoke();
    }

    /// <summary>
    /// Internal method to update window size. Called by WindowNode during resize.
    /// Applies min/max constraints.
    /// </summary>
    internal void UpdateSize(WindowEntry entry, int width, int height)
    {
        // Apply min/max constraints
        width = Math.Max(entry.MinWidth, width);
        height = Math.Max(entry.MinHeight, height);
        
        if (entry.MaxWidth.HasValue)
            width = Math.Min(entry.MaxWidth.Value, width);
        if (entry.MaxHeight.HasValue)
            height = Math.Min(entry.MaxHeight.Value, height);
        
        entry.Width = width;
        entry.Height = height;
        Changed?.Invoke();
    }

}

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
        object? onResultCallback,
        Type? resultType,
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
        OnResultCallback = onResultCallback;
        ResultType = resultType;
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
    /// Callback invoked when the window closes with a result (boxed Action&lt;WindowResultContext&lt;T&gt;&gt;).
    /// </summary>
    internal object? OnResultCallback { get; }

    /// <summary>
    /// The type T for the result callback.
    /// </summary>
    internal Type? ResultType { get; }

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
        if (OnResultCallback == null || ResultType == null)
            return;

        // Create WindowResultContext<T> and invoke the callback
        var contextType = typeof(WindowResultContext<>).MakeGenericType(ResultType);
        var context = Activator.CreateInstance(
            contextType, 
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            [this, ResultProvided ? ResultValue : GetDefault(ResultType), !ResultProvided],
            null);

        // Invoke the callback
        var callbackType = typeof(Action<>).MakeGenericType(contextType);
        callbackType.GetMethod("Invoke")!.Invoke(OnResultCallback, [context]);
    }

    private static object? GetDefault(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    /// <summary>
    /// Brings this window to the front.
    /// </summary>
    public void BringToFront() => Manager.BringToFront(this);
}

/// <summary>
/// Controls how the Escape key behaves for a window.
/// </summary>
public enum WindowEscapeBehavior
{
    /// <summary>
    /// Escape closes the window (default).
    /// </summary>
    Close,

    /// <summary>
    /// Escape is ignored - window stays open.
    /// </summary>
    Ignore,

    /// <summary>
    /// Escape only closes non-modal windows.
    /// </summary>
    CloseNonModal
}
