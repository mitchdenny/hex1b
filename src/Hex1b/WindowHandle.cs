using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A factory/descriptor for creating windows using the fluent builder pattern.
/// WindowHandle captures the configuration and content callback, which are invoked
/// during reconciliation to build the actual widget tree.
/// </summary>
/// <remarks>
/// <para>
/// WindowHandle provides a fluent API for defining windows that integrates with
/// the widget builder experience. The handle itself serves as the identity for
/// the window - no string ID is required.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var window = e.Windows.Window(w =&gt; w.VStack(v =&gt; [
///     v.Text("Settings"),
///     v.Button("Close").OnClick(ev =&gt; ev.Windows.Close(w.Window))
/// ]))
/// .Title("Settings")
/// .Size(40, 20)
/// .RightTitleActions(t =&gt; [t.Close()]);
/// 
/// e.Windows.Open(window);
/// </code>
/// </para>
/// </remarks>
public sealed class WindowHandle
{
    private readonly Func<WindowContentContext<Hex1bWidget>, Hex1bWidget> _contentBuilder;
    private string _title = "";
    private int _width = 40;
    private int _height = 15;
    private int? _x;
    private int? _y;
    private WindowPositionSpec _positionSpec = new(WindowPosition.Center);
    private bool _isModal;
    private bool _isResizable;
    private int _minWidth = 10;
    private int _minHeight = 5;
    private int? _maxWidth;
    private int? _maxHeight;
    private bool _allowOutOfBounds;
    private bool _showTitleBar = true;
    private Func<TitleActionBuilder, IEnumerable<TitleAction>>? _leftTitleActionsBuilder;
    private Func<TitleActionBuilder, IEnumerable<TitleAction>>? _rightTitleActionsBuilder;
    private WindowEscapeBehavior _escapeBehavior = WindowEscapeBehavior.Close;
    private Action? _onClose;
    private Action? _onActivated;
    private Action? _onDeactivated;
    private object? _onResultCallback;
    private Type? _resultType;

    // Default right title actions (close button) when no builder is specified
    private static readonly IReadOnlyList<WindowAction> DefaultRightActions = [WindowAction.Close()];

    internal WindowHandle(Func<WindowContentContext<Hex1bWidget>, Hex1bWidget> contentBuilder)
    {
        _contentBuilder = contentBuilder ?? throw new ArgumentNullException(nameof(contentBuilder));
    }

    /// <summary>
    /// Sets the window title displayed in the title bar.
    /// </summary>
    /// <param name="title">The window title.</param>
    /// <returns>This handle for fluent chaining.</returns>
    public WindowHandle Title(string title)
    {
        _title = title ?? throw new ArgumentNullException(nameof(title));
        return this;
    }

    /// <summary>
    /// Sets the initial size of the window.
    /// </summary>
    /// <param name="width">The window width.</param>
    /// <param name="height">The window height.</param>
    /// <returns>This handle for fluent chaining.</returns>
    public WindowHandle Size(int width, int height)
    {
        _width = width;
        _height = height;
        return this;
    }

    /// <summary>
    /// Sets the initial position of the window.
    /// </summary>
    /// <param name="x">The X coordinate.</param>
    /// <param name="y">The Y coordinate.</param>
    /// <returns>This handle for fluent chaining.</returns>
    public WindowHandle Position(int x, int y)
    {
        _x = x;
        _y = y;
        return this;
    }

    /// <summary>
    /// Sets the positioning strategy for initial placement.
    /// </summary>
    /// <param name="position">The position specification.</param>
    /// <returns>This handle for fluent chaining.</returns>
    public WindowHandle Position(WindowPositionSpec position)
    {
        _positionSpec = position;
        return this;
    }

    /// <summary>
    /// Makes this a modal window that blocks interaction with other windows.
    /// </summary>
    /// <returns>This handle for fluent chaining.</returns>
    public WindowHandle Modal()
    {
        _isModal = true;
        return this;
    }

    /// <summary>
    /// Makes this window resizable by dragging edges and corners.
    /// </summary>
    /// <param name="minWidth">Minimum width constraint.</param>
    /// <param name="minHeight">Minimum height constraint.</param>
    /// <param name="maxWidth">Maximum width constraint (null = unbounded).</param>
    /// <param name="maxHeight">Maximum height constraint (null = unbounded).</param>
    /// <returns>This handle for fluent chaining.</returns>
    public WindowHandle Resizable(int minWidth = 10, int minHeight = 5, int? maxWidth = null, int? maxHeight = null)
    {
        _isResizable = true;
        _minWidth = minWidth;
        _minHeight = minHeight;
        _maxWidth = maxWidth;
        _maxHeight = maxHeight;
        return this;
    }

    /// <summary>
    /// Allows this window to be moved outside the panel bounds.
    /// </summary>
    /// <returns>This handle for fluent chaining.</returns>
    public WindowHandle AllowOutOfBounds()
    {
        _allowOutOfBounds = true;
        return this;
    }

    /// <summary>
    /// Hides the title bar, creating a frameless window.
    /// </summary>
    /// <returns>This handle for fluent chaining.</returns>
    public WindowHandle NoTitleBar()
    {
        _showTitleBar = false;
        return this;
    }

    /// <summary>
    /// Configures the actions displayed on the left side of the title bar.
    /// </summary>
    /// <param name="builder">Builder function that receives a <see cref="TitleActionBuilder"/> and returns title actions.</param>
    /// <returns>This handle for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// window.LeftTitleActions(t =&gt; [
    ///     t.Action("📌", ctx =&gt; PinWindow()),
    ///     t.Action("📋", ctx =&gt; CopyContent())
    /// ]);
    /// </code>
    /// </example>
    public WindowHandle LeftTitleActions(Func<TitleActionBuilder, IEnumerable<TitleAction>> builder)
    {
        _leftTitleActionsBuilder = builder ?? throw new ArgumentNullException(nameof(builder));
        return this;
    }

    /// <summary>
    /// Configures the actions displayed on the right side of the title bar.
    /// If not called, defaults to a single close button.
    /// </summary>
    /// <param name="builder">Builder function that receives a <see cref="TitleActionBuilder"/> and returns title actions.</param>
    /// <returns>This handle for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// window.RightTitleActions(t =&gt; [
    ///     t.Action("?", ctx =&gt; ShowHelp()),
    ///     t.Close()
    /// ]);
    /// </code>
    /// </example>
    public WindowHandle RightTitleActions(Func<TitleActionBuilder, IEnumerable<TitleAction>> builder)
    {
        _rightTitleActionsBuilder = builder ?? throw new ArgumentNullException(nameof(builder));
        return this;
    }

    /// <summary>
    /// Sets the escape key behavior for this window.
    /// </summary>
    /// <param name="behavior">The escape behavior.</param>
    /// <returns>This handle for fluent chaining.</returns>
    public WindowHandle EscapeBehavior(WindowEscapeBehavior behavior)
    {
        _escapeBehavior = behavior;
        return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when the window is closed.
    /// </summary>
    /// <param name="onClose">The callback to invoke on close.</param>
    /// <returns>This handle for fluent chaining.</returns>
    public WindowHandle OnClose(Action onClose)
    {
        _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));
        return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when the window becomes active (brought to front).
    /// </summary>
    /// <param name="onActivated">The callback to invoke on activation.</param>
    /// <returns>This handle for fluent chaining.</returns>
    public WindowHandle OnActivated(Action onActivated)
    {
        _onActivated = onActivated ?? throw new ArgumentNullException(nameof(onActivated));
        return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when the window loses active status.
    /// </summary>
    /// <param name="onDeactivated">The callback to invoke on deactivation.</param>
    /// <returns>This handle for fluent chaining.</returns>
    public WindowHandle OnDeactivated(Action onDeactivated)
    {
        _onDeactivated = onDeactivated ?? throw new ArgumentNullException(nameof(onDeactivated));
        return this;
    }

    /// <summary>
    /// Builds the content widget by invoking the content callback.
    /// Called during reconciliation.
    /// </summary>
    internal Hex1bWidget BuildContent()
    {
        var context = new WindowContentContext<Hex1bWidget>(this);
        return _contentBuilder(context);
    }

    /// <summary>
    /// Builds the left title bar actions.
    /// </summary>
    internal IReadOnlyList<WindowAction> BuildLeftTitleActions()
    {
        if (_leftTitleActionsBuilder == null)
            return [];

        var builder = new TitleActionBuilder();
        return _leftTitleActionsBuilder(builder)
            .Select(ta => ta.ToWindowAction())
            .ToList();
    }

    /// <summary>
    /// Builds the right title bar actions.
    /// </summary>
    internal IReadOnlyList<WindowAction> BuildRightTitleActions()
    {
        if (_rightTitleActionsBuilder == null)
            return DefaultRightActions;

        var builder = new TitleActionBuilder();
        return _rightTitleActionsBuilder(builder)
            .Select(ta => ta.ToWindowAction())
            .ToList();
    }

    // Internal property accessors for WindowEntry creation
    internal string TitleValue => _title;
    internal int WidthValue => _width;
    internal int HeightValue => _height;
    internal int? XValue => _x;
    internal int? YValue => _y;
    internal WindowPositionSpec PositionSpecValue => _positionSpec;
    internal bool IsModalValue => _isModal;
    internal bool IsResizableValue => _isResizable;
    internal int MinWidthValue => _minWidth;
    internal int MinHeightValue => _minHeight;
    internal int? MaxWidthValue => _maxWidth;
    internal int? MaxHeightValue => _maxHeight;
    internal bool AllowOutOfBoundsValue => _allowOutOfBounds;
    internal bool ShowTitleBarValue => _showTitleBar;
    internal WindowEscapeBehavior EscapeBehaviorValue => _escapeBehavior;
    internal Action? OnCloseValue => _onClose;
    internal Action? OnActivatedValue => _onActivated;
    internal Action? OnDeactivatedValue => _onDeactivated;
    internal object? OnResultCallbackValue => _onResultCallback;
    internal Type? ResultTypeValue => _resultType;

    /// <summary>
    /// The associated WindowEntry when this handle is opened.
    /// Set internally by WindowManager.Open.
    /// </summary>
    internal WindowEntry? Entry { get; set; }

    /// <summary>
    /// Registers a callback to be invoked when the window closes with a typed result.
    /// </summary>
    /// <typeparam name="T">The type of result expected from the dialog.</typeparam>
    /// <param name="callback">The callback to invoke with the result context.</param>
    /// <returns>This handle for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// The callback receives a <see cref="WindowResultContext{T}"/> with:
    /// <list type="bullet">
    ///   <item><see cref="WindowResultContext{T}.Value"/> - the result value (default if cancelled)</item>
    ///   <item><see cref="WindowResultContext{T}.IsCancelled"/> - true if closed without a result</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// e.Windows.Window(w =&gt; w.VStack(v =&gt; [
    ///     v.Text("Delete this item?"),
    ///     v.HStack(h =&gt; [
    ///         h.Button("Yes").OnClick(_ =&gt; w.Window.CloseWithResult(true)),
    ///         h.Button("No").OnClick(_ =&gt; w.Window.CloseWithResult(false))
    ///     ])
    /// ]))
    /// .Title("Confirm Delete")
    /// .Modal()
    /// .OnResult&lt;bool&gt;(result =&gt; {
    ///     if (!result.IsCancelled &amp;&amp; result.Value)
    ///         DeleteItem();
    /// })
    /// .Open(e.Windows);
    /// </code>
    /// </example>
    public WindowHandle OnResult<T>(Action<WindowResultContext<T>> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _onResultCallback = callback;
        _resultType = typeof(T);
        return this;
    }

    /// <summary>
    /// Closes this window with a typed result value.
    /// Used for modal dialogs that return a result to the caller.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="result">The result value to return.</param>
    /// <exception cref="InvalidOperationException">Thrown if the window has not been opened.</exception>
    /// <example>
    /// <code>
    /// e.Windows.Window(w =&gt; w.VStack(v =&gt; [
    ///     v.Text("Confirm?"),
    ///     v.Button("Yes").OnClick(_ =&gt; w.Window.CloseWithResult(true)),
    ///     v.Button("No").OnClick(_ =&gt; w.Window.CloseWithResult(false))
    /// ]))
    /// .Title("Confirm")
    /// .Modal()
    /// .OnResult&lt;bool&gt;(result =&gt; { /* handle result */ })
    /// .Open(e.Windows);
    /// </code>
    /// </example>
    public void CloseWithResult<T>(T result)
    {
        if (Entry == null)
        {
            throw new InvalidOperationException("Cannot close a window that has not been opened.");
        }
        Entry.CloseWithResult(result);
    }

    /// <summary>
    /// Closes this window without a result, signaling cancellation.
    /// The OnResult callback will receive IsCancelled = true.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the window has not been opened.</exception>
    /// <example>
    /// <code>
    /// e.Windows.Window(w =&gt; w.VStack(v =&gt; [
    ///     v.Text("Enter name:"),
    ///     v.TextBox(nameState),
    ///     v.HStack(h =&gt; [
    ///         h.Button("OK").OnClick(_ =&gt; w.Window.CloseWithResult(nameState.Text)),
    ///         h.Button("Cancel").OnClick(_ =&gt; w.Window.Cancel())
    ///     ])
    /// ]))
    /// .Title("Input")
    /// .Modal()
    /// .OnResult&lt;string&gt;(result =&gt; {
    ///     if (!result.IsCancelled)
    ///         ProcessName(result.Value);
    /// })
    /// .Open(e.Windows);
    /// </code>
    /// </example>
    public void Cancel()
    {
        if (Entry == null)
        {
            throw new InvalidOperationException("Cannot cancel a window that has not been opened.");
        }
        Entry.Close();
    }

    /// <summary>
    /// Opens this window using the specified window manager.
    /// Convenience method for fluent chaining.
    /// </summary>
    /// <param name="manager">The window manager to open the window with.</param>
    /// <returns>The opened window entry.</returns>
    /// <example>
    /// <code>
    /// e.Windows.Window(w =&gt; w.Text("Hello!"))
    ///     .Title("My Window")
    ///     .Open(e.Windows);
    /// </code>
    /// </example>
    public WindowEntry Open(WindowManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        return manager.Open(this);
    }
}
