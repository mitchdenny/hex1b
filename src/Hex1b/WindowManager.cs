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
                resultInvoker: handle.ResultInvokerValue,
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
