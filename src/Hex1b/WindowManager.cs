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
    /// <param name="onClose">Callback when the window is closed.</param>
    /// <param name="onActivated">Callback when the window becomes active (brought to front).</param>
    /// <param name="onDeactivated">Callback when the window loses active status.</param>
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
        Action? onClose = null,
        Action? onActivated = null,
        Action? onDeactivated = null)
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
                onClose: onClose,
                onActivated: onActivated,
                onDeactivated: onDeactivated,
                zIndex: _nextZIndex++
            );

            _entries.Add(entry);
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
    /// </summary>
    internal void UpdateSize(WindowEntry entry, int width, int height)
    {
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
        Func<Hex1bWidget> contentBuilder,
        int width,
        int height,
        int? x,
        int? y,
        WindowPositionSpec positionSpec,
        bool isModal,
        bool isResizable,
        Action? onClose,
        Action? onActivated,
        Action? onDeactivated,
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
        OnClose = onClose;
        OnActivated = onActivated;
        OnDeactivated = onDeactivated;
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
    /// Z-order index (higher = on top).
    /// </summary>
    public int ZIndex { get; internal set; }

    /// <summary>
    /// The current window state.
    /// </summary>
    public WindowState State { get; internal set; } = WindowState.Normal;

    /// <summary>
    /// The reconciled window node. Set by WindowPanelNode during reconciliation.
    /// </summary>
    internal WindowNode? Node { get; set; }

    /// <summary>
    /// Closes this window.
    /// </summary>
    public void Close() => Manager.Close(this);

    /// <summary>
    /// Brings this window to the front.
    /// </summary>
    public void BringToFront() => Manager.BringToFront(this);
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
