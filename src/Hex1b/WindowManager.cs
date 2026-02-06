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
///     e.Context.Windows.Open(
///         "settings",
///         "Settings",
///         () =&gt; ctx.VStack(v =&gt; [
///             v.Text("Settings content here")
///         ]),
///         new WindowOptions { Width = 60, Height = 20 }
///     );
/// });
/// </code>
/// </example>
public sealed class WindowManager
{
    private readonly List<WindowEntry> _entries = [];
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
    /// Opens a new window.
    /// </summary>
    /// <param name="id">Unique identifier for the window. If a window with this ID exists, it is brought to front instead.</param>
    /// <param name="title">The window title.</param>
    /// <param name="content">Builder function for window content. Receives a context for fluent widget construction.</param>
    /// <param name="options">Configuration options for the window. If null, uses <see cref="WindowOptions.Default"/>.</param>
    /// <returns>The window entry.</returns>
    /// <example>
    /// <code>
    /// // Simple window with fluent content
    /// e.Windows.Open("my-window", "My Window", w => w.Text("Hello!"));
    /// 
    /// // Window with custom options
    /// e.Windows.Open("settings", "Settings", w => w.VStack(v => [
    ///     v.Text("Settings content")
    /// ]), new WindowOptions
    /// {
    ///     Width = 60,
    ///     Height = 20,
    ///     IsResizable = true,
    ///     Position = new WindowPositionSpec(WindowPosition.TopRight)
    /// });
    /// </code>
    /// </example>
    public WindowEntry Open(
        string id,
        string title,
        Func<WidgetContext<Hex1bWidget>, Hex1bWidget> content,
        WindowOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(content);

        options ??= WindowOptions.Default;

        lock (_lock)
        {
            // Check if window with this ID already exists
            var existing = _entries.FirstOrDefault(e => e.Id == id);
            if (existing != null)
            {
                BringToFrontInternal(existing);
                return existing;
            }

            var entry = new WindowEntry(
                manager: this,
                id: id,
                title: title,
                contentBuilder: content,
                width: options.Width,
                height: options.Height,
                x: options.X,
                y: options.Y,
                positionSpec: options.Position,
                isModal: options.IsModal,
                isResizable: options.IsResizable,
                minWidth: options.MinWidth,
                minHeight: options.MinHeight,
                maxWidth: options.MaxWidth,
                maxHeight: options.MaxHeight,
                onClose: options.OnClose,
                onActivated: options.OnActivated,
                onDeactivated: options.OnDeactivated,
                showTitleBar: options.ShowTitleBar,
                leftTitleBarActions: options.LeftTitleBarActions ?? [],
                rightTitleBarActions: options.RightTitleBarActions ?? [WindowAction.Close()],
                escapeBehavior: options.EscapeBehavior,
                zIndex: _nextZIndex++,
                allowOutOfBounds: options.AllowOutOfBounds
            );

            _entries.Add(entry);
        }

        Changed?.Invoke();
        return _entries.Last();
    }

    /// <summary>
    /// Opens a modal dialog and waits for it to be closed with a result.
    /// Use <see cref="WindowEntry.CloseWithResult"/> to close the dialog and return a value.
    /// </summary>
    /// <typeparam name="TResult">The type of result expected from the dialog.</typeparam>
    /// <param name="id">Unique identifier for the window.</param>
    /// <param name="title">The window title.</param>
    /// <param name="content">Builder function for window content.</param>
    /// <param name="options">Configuration options. <see cref="WindowOptions.IsModal"/> is automatically set to true.</param>
    /// <returns>A task that completes when the modal is closed, containing the result.</returns>
    /// <remarks>
    /// <para>
    /// <b>Warning:</b> Do not await this method directly in UI event handlers (such as button click
    /// or menu item handlers). Awaiting in a handler blocks the UI from processing input, which means
    /// the modal cannot receive clicks to close it, causing a deadlock.
    /// </para>
    /// <para>
    /// Instead, use the regular <see cref="Open"/> method with <c>IsModal = true</c> and handle the
    /// result in button click handlers or the <c>OnClose</c> callback:
    /// </para>
    /// <code>
    /// // ✓ Correct: Use Open with callbacks
    /// e.Windows.Open("confirm", "Confirm", () => ..., new WindowOptions { IsModal = true, OnClose = () => { /* handle close */ } });
    /// 
    /// // ✗ Wrong: Don't await in event handlers
    /// var result = await e.Windows.OpenModalAsync&lt;bool&gt;(...); // DEADLOCK!
    /// </code>
    /// <para>
    /// This method is suitable for use in background tasks or when you have full control of the
    /// execution context (e.g., in tests or non-UI code that can yield to the UI thread).
    /// </para>
    /// </remarks>
    public Task<TResult?> OpenModalAsync<TResult>(
        string id,
        string title,
        Func<WidgetContext<Hex1bWidget>, Hex1bWidget> content,
        WindowOptions? options = null)
    {
        var tcs = new TaskCompletionSource<object?>();
        options ??= WindowOptions.Dialog;
        
        // Create a new options instance that forces IsModal and captures the close callback
        var existingOnClose = options.OnClose;
        var modalOptions = new WindowOptions
        {
            Width = options.Width,
            Height = options.Height,
            X = options.X,
            Y = options.Y,
            Position = options.Position,
            IsModal = true, // Always modal for this method
            IsResizable = options.IsResizable,
            MinWidth = options.MinWidth,
            MinHeight = options.MinHeight,
            MaxWidth = options.MaxWidth,
            MaxHeight = options.MaxHeight,
            AllowOutOfBounds = options.AllowOutOfBounds,
            OnActivated = options.OnActivated,
            OnDeactivated = options.OnDeactivated,
            ShowTitleBar = options.ShowTitleBar,
            LeftTitleBarActions = options.LeftTitleBarActions,
            RightTitleBarActions = options.RightTitleBarActions,
            EscapeBehavior = options.EscapeBehavior,
            OnClose = () =>
            {
                existingOnClose?.Invoke();
                // If closed without result (e.g., by Escape), complete with default
                tcs.TrySetResult(default);
            }
        };
        
        var entry = Open(id, title, content, modalOptions);
        entry.ResultSource = tcs;
        
        return tcs.Task.ContinueWith(t => t.Result is TResult r ? r : default);
    }

    /// <summary>
    /// Opens a modal dialog and waits for it to be closed.
    /// This overload doesn't expect a specific result type.
    /// </summary>
    /// <param name="id">Unique identifier for the window.</param>
    /// <param name="title">The window title.</param>
    /// <param name="content">Builder function for window content. Receives a context for fluent widget construction.</param>
    /// <param name="options">Configuration options. <see cref="WindowOptions.IsModal"/> is automatically set to true.</param>
    /// <returns>A task that completes when the modal is closed.</returns>
    /// <remarks>
    /// <para>
    /// <b>Warning:</b> Do not await this method directly in UI event handlers. See 
    /// <see cref="OpenModalAsync{TResult}"/> for details on proper usage.
    /// </para>
    /// </remarks>
    public Task OpenModalAsync(
        string id,
        string title,
        Func<WidgetContext<Hex1bWidget>, Hex1bWidget> content,
        WindowOptions? options = null)
    {
        return OpenModalAsync<object>(id, title, content, options);
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
            onClose = entry.OnClose;
        }

        onClose?.Invoke();
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Closes a window by its ID.
    /// </summary>
    /// <param name="id">The window ID.</param>
    /// <returns>True if the window was found and closed.</returns>
    public bool Close(string id)
    {
        WindowEntry? entry;
        
        lock (_lock)
        {
            entry = _entries.FirstOrDefault(e => e.Id == id);
        }

        if (entry == null)
            return false;

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
    /// Brings a window to the front by ID.
    /// </summary>
    /// <param name="id">The window ID.</param>
    public void BringToFront(string id)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry != null)
            {
                BringToFrontInternal(entry);
            }
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Gets a window entry by ID.
    /// </summary>
    /// <param name="id">The window ID.</param>
    /// <returns>The window entry, or null if not found.</returns>
    public WindowEntry? Get(string id)
    {
        lock (_lock)
        {
            return _entries.FirstOrDefault(e => e.Id == id);
        }
    }

    /// <summary>
    /// Checks if a window with the given ID is open.
    /// </summary>
    /// <param name="id">The window ID.</param>
    /// <returns>True if the window is open.</returns>
    public bool IsOpen(string id)
    {
        lock (_lock)
        {
            return _entries.Any(e => e.Id == id);
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
        ShowTitleBar = showTitleBar;
        LeftTitleBarActions = leftTitleBarActions;
        RightTitleBarActions = rightTitleBarActions;
        EscapeBehavior = escapeBehavior;
        ZIndex = zIndex;
        AllowOutOfBounds = allowOutOfBounds;
    }

    internal WindowManager Manager { get; }

    /// <summary>
    /// Unique identifier for this window.
    /// </summary>
    public string Id { get; }

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
    /// Task completion source for modal result pattern.
    /// </summary>
    internal TaskCompletionSource<object?>? ResultSource { get; set; }

    /// <summary>
    /// Closes this window.
    /// </summary>
    public void Close() => Manager.Close(this);

    /// <summary>
    /// Closes this modal window with a result value.
    /// If this window was opened with OpenModalAsync, the awaiting task will complete with this result.
    /// </summary>
    /// <param name="result">The result value to return.</param>
    public void CloseWithResult(object? result)
    {
        ResultSource?.TrySetResult(result);
        Manager.Close(this);
    }

    /// <summary>
    /// Closes this modal window with a typed result value.
    /// If this window was opened with OpenModalAsync&lt;TResult&gt;, the awaiting task will complete with this result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="result">The result value to return.</param>
    public void CloseWithResult<TResult>(TResult result)
    {
        ResultSource?.TrySetResult(result);
        Manager.Close(this);
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
