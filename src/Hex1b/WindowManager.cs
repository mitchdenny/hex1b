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
///         id: "settings",
///         title: "Settings",
///         width: 60,
///         height: 20,
///         content: ctx =&gt; ctx.VStack(v =&gt; [
///             v.Text("Settings content here")
///         ])
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
    /// <param name="content">Builder function for window content. Called each reconciliation to build the widget.</param>
    /// <param name="width">Initial width of the window.</param>
    /// <param name="height">Initial height of the window.</param>
    /// <param name="x">Initial X position (null to use position parameter).</param>
    /// <param name="y">Initial Y position (null to use position parameter).</param>
    /// <param name="position">Positioning strategy when x/y are null. Defaults to Center.</param>
    /// <param name="isModal">Whether this is a modal window.</param>
    /// <param name="isResizable">Whether the window can be resized.</param>
    /// <param name="minWidth">Minimum width for resize operations.</param>
    /// <param name="minHeight">Minimum height for resize operations.</param>
    /// <param name="maxWidth">Maximum width for resize operations (null = unbounded).</param>
    /// <param name="maxHeight">Maximum height for resize operations (null = unbounded).</param>
    /// <param name="onClose">Callback when the window is closed.</param>
    /// <param name="onActivated">Callback when the window becomes active (brought to front).</param>
    /// <param name="onDeactivated">Callback when the window loses active status.</param>
    /// <param name="chromeStyle">The chrome style (buttons displayed). Defaults to TitleAndClose.</param>
    /// <param name="escapeBehavior">How Escape key is handled. Defaults to Close.</param>
    /// <param name="onMinimize">Callback when the window is minimized.</param>
    /// <param name="onMaximize">Callback when the window is maximized.</param>
    /// <param name="onRestore">Callback when the window is restored from minimized/maximized.</param>
    /// <returns>The window entry.</returns>
    public WindowEntry Open(
        string id,
        string title,
        Func<Hex1bWidget> content,
        int width = 40,
        int height = 15,
        int? x = null,
        int? y = null,
        WindowPositionSpec position = default,
        bool isModal = false,
        bool isResizable = false,
        int minWidth = 10,
        int minHeight = 5,
        int? maxWidth = null,
        int? maxHeight = null,
        Action? onClose = null,
        Action? onActivated = null,
        Action? onDeactivated = null,
        WindowChromeStyle chromeStyle = WindowChromeStyle.TitleAndClose,
        WindowEscapeBehavior escapeBehavior = WindowEscapeBehavior.Close,
        Action? onMinimize = null,
        Action? onMaximize = null,
        Action? onRestore = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(content);

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
                width: width,
                height: height,
                x: x,
                y: y,
                positionSpec: position,
                isModal: isModal,
                isResizable: isResizable,
                minWidth: minWidth,
                minHeight: minHeight,
                maxWidth: maxWidth,
                maxHeight: maxHeight,
                onClose: onClose,
                onActivated: onActivated,
                onDeactivated: onDeactivated,
                chromeStyle: chromeStyle,
                escapeBehavior: escapeBehavior,
                onMinimize: onMinimize,
                onMaximize: onMaximize,
                onRestore: onRestore,
                zIndex: _nextZIndex++
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
    /// <param name="width">Initial width of the window.</param>
    /// <param name="height">Initial height of the window.</param>
    /// <param name="chromeStyle">The chrome style (buttons displayed). Defaults to TitleAndClose.</param>
    /// <param name="escapeBehavior">How Escape key is handled. Defaults to Close (which will return default(TResult)).</param>
    /// <returns>A task that completes when the modal is closed, containing the result.</returns>
    /// <example>
    /// <code>
    /// var result = await e.Windows.OpenModalAsync&lt;bool&gt;(
    ///     "confirm", "Confirm",
    ///     () => ctx.VStack(v => [
    ///         v.Text("Are you sure?"),
    ///         v.HStack(h => [
    ///             h.Button("Yes").OnClick(e => e.Windows.Get("confirm")?.CloseWithResult(true)),
    ///             h.Button("No").OnClick(e => e.Windows.Get("confirm")?.CloseWithResult(false))
    ///         ])
    ///     ]),
    ///     width: 30, height: 8
    /// );
    /// if (result) { /* user confirmed */ }
    /// </code>
    /// </example>
    public Task<TResult?> OpenModalAsync<TResult>(
        string id,
        string title,
        Func<Hex1bWidget> content,
        int width = 40,
        int height = 15,
        WindowChromeStyle chromeStyle = WindowChromeStyle.TitleAndClose,
        WindowEscapeBehavior escapeBehavior = WindowEscapeBehavior.Close)
    {
        var tcs = new TaskCompletionSource<object?>();
        
        var entry = Open(
            id: id,
            title: title,
            content: content,
            width: width,
            height: height,
            isModal: true,
            chromeStyle: chromeStyle,
            escapeBehavior: escapeBehavior,
            onClose: () =>
            {
                // If closed without result (e.g., by Escape), complete with default
                tcs.TrySetResult(default);
            }
        );
        
        entry.ResultSource = tcs;
        
        return tcs.Task.ContinueWith(t => t.Result is TResult r ? r : default);
    }

    /// <summary>
    /// Opens a modal dialog and waits for it to be closed.
    /// This overload doesn't expect a specific result type.
    /// </summary>
    /// <param name="id">Unique identifier for the window.</param>
    /// <param name="title">The window title.</param>
    /// <param name="content">Builder function for window content.</param>
    /// <param name="width">Initial width of the window.</param>
    /// <param name="height">Initial height of the window.</param>
    /// <param name="chromeStyle">The chrome style (buttons displayed). Defaults to TitleAndClose.</param>
    /// <param name="escapeBehavior">How Escape key is handled. Defaults to Close.</param>
    /// <returns>A task that completes when the modal is closed.</returns>
    public Task OpenModalAsync(
        string id,
        string title,
        Func<Hex1bWidget> content,
        int width = 40,
        int height = 15,
        WindowChromeStyle chromeStyle = WindowChromeStyle.TitleAndClose,
        WindowEscapeBehavior escapeBehavior = WindowEscapeBehavior.Close)
    {
        var tcs = new TaskCompletionSource<object?>();
        
        var entry = Open(
            id: id,
            title: title,
            content: content,
            width: width,
            height: height,
            isModal: true,
            chromeStyle: chromeStyle,
            escapeBehavior: escapeBehavior,
            onClose: () => tcs.TrySetResult(null)
        );
        
        entry.ResultSource = tcs;
        
        return tcs.Task;
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

    /// <summary>
    /// Sets the window state (Normal, Minimized, Maximized).
    /// </summary>
    /// <param name="entry">The window entry.</param>
    /// <param name="newState">The new state.</param>
    public void SetWindowState(WindowEntry entry, WindowState newState)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_lock)
        {
            if (!_entries.Contains(entry))
                return;

            var oldState = entry.State;
            if (oldState == newState)
                return;

            // Handle state transition
            switch (newState)
            {
                case WindowState.Minimized:
                    entry.State = WindowState.Minimized;
                    entry.OnMinimize?.Invoke();
                    break;

                case WindowState.Maximized:
                    // Save current size/position for restore
                    if (oldState == WindowState.Normal)
                    {
                        entry.PreMaximizeSize = (entry.Width, entry.Height);
                        entry.PreMaximizePosition = entry.X.HasValue && entry.Y.HasValue
                            ? (entry.X.Value, entry.Y.Value)
                            : null;
                    }
                    entry.State = WindowState.Maximized;
                    entry.OnMaximize?.Invoke();
                    break;

                case WindowState.Normal:
                    // Restore previous size/position if available
                    if (oldState == WindowState.Maximized && entry.PreMaximizeSize.HasValue)
                    {
                        entry.Width = entry.PreMaximizeSize.Value.Width;
                        entry.Height = entry.PreMaximizeSize.Value.Height;
                        if (entry.PreMaximizePosition.HasValue)
                        {
                            entry.X = entry.PreMaximizePosition.Value.X;
                            entry.Y = entry.PreMaximizePosition.Value.Y;
                        }
                        entry.PreMaximizeSize = null;
                        entry.PreMaximizePosition = null;
                    }
                    entry.State = WindowState.Normal;
                    entry.OnRestore?.Invoke();
                    break;
            }
        }

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
        Func<Hex1bWidget> contentBuilder,
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
        WindowChromeStyle chromeStyle,
        WindowEscapeBehavior escapeBehavior,
        Action? onMinimize,
        Action? onMaximize,
        Action? onRestore,
        int zIndex)
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
        ChromeStyle = chromeStyle;
        EscapeBehavior = escapeBehavior;
        OnMinimize = onMinimize;
        OnMaximize = onMaximize;
        OnRestore = onRestore;
        ZIndex = zIndex;
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
    internal Func<Hex1bWidget> ContentBuilder { get; }

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
    /// The chrome style for this window.
    /// </summary>
    public WindowChromeStyle ChromeStyle { get; }

    /// <summary>
    /// How Escape key is handled for this window.
    /// </summary>
    public WindowEscapeBehavior EscapeBehavior { get; }

    /// <summary>
    /// Callback invoked when the window is minimized.
    /// </summary>
    internal Action? OnMinimize { get; }

    /// <summary>
    /// Callback invoked when the window is maximized.
    /// </summary>
    internal Action? OnMaximize { get; }

    /// <summary>
    /// Callback invoked when the window is restored from minimized/maximized.
    /// </summary>
    internal Action? OnRestore { get; }

    /// <summary>
    /// Z-order index (higher = on top).
    /// </summary>
    public int ZIndex { get; internal set; }

    /// <summary>
    /// The current window state.
    /// </summary>
    public WindowState State { get; internal set; } = WindowState.Normal;

    /// <summary>
    /// Stored size before maximizing, for restore.
    /// </summary>
    internal (int Width, int Height)? PreMaximizeSize { get; set; }

    /// <summary>
    /// Stored position before maximizing, for restore.
    /// </summary>
    internal (int X, int Y)? PreMaximizePosition { get; set; }

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

    /// <summary>
    /// Minimizes this window.
    /// </summary>
    public void Minimize() => Manager.SetWindowState(this, WindowState.Minimized);

    /// <summary>
    /// Maximizes this window.
    /// </summary>
    public void Maximize() => Manager.SetWindowState(this, WindowState.Maximized);

    /// <summary>
    /// Restores this window to normal state.
    /// </summary>
    public void Restore() => Manager.SetWindowState(this, WindowState.Normal);

    /// <summary>
    /// Toggles between maximized and normal state.
    /// </summary>
    public void ToggleMaximize()
    {
        if (State == WindowState.Maximized)
            Restore();
        else
            Maximize();
    }
}

/// <summary>
/// Represents the state of a window.
/// </summary>
public enum WindowState
{
    /// <summary>
    /// Normal window state.
    /// </summary>
    Normal,

    /// <summary>
    /// Window is minimized.
    /// </summary>
    Minimized,

    /// <summary>
    /// Window is maximized to fill the panel.
    /// </summary>
    Maximized
}

/// <summary>
/// Controls which chrome elements (title bar, buttons) are displayed on a window.
/// </summary>
public enum WindowChromeStyle
{
    /// <summary>
    /// No window chrome - just content with border.
    /// </summary>
    None,

    /// <summary>
    /// Title bar only, no buttons.
    /// </summary>
    TitleOnly,

    /// <summary>
    /// Title bar with close button.
    /// </summary>
    TitleAndClose,

    /// <summary>
    /// Title bar with close, minimize, and maximize buttons.
    /// </summary>
    Full
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
