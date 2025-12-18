#pragma warning disable HEX1B_SIXEL // Sixel API is experimental - internal usage is allowed

using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// The main entry point for building terminal UI applications.
/// </summary>
/// <example>
/// <para>Create a minimal Hex1b application:</para>
/// <code>
/// using Hex1b;
/// 
/// var app = new Hex1bApp(ctx =&gt;
///     ctx.VStack(v => [
///         v.Text("Hello, Hex1b!"),
///         v.Button("Quit", e => e.Context.RequestStop())
///     ])
/// );
/// 
/// await app.RunAsync();
/// </code>
/// </example>
/// <remarks>
/// Hex1bApp manages the render loop, input handling, focus management, and reconciliation
/// between widgets (immutable declarations) and nodes (mutable render state).
/// 
/// State management is handled via closures - simply capture your state variables
/// in the widget builder callback.
/// </remarks>
public class Hex1bApp : IDisposable
{
    private readonly Func<RootContext, Task<Hex1bWidget>> _rootComponent;
    private readonly Func<Hex1bTheme>? _themeProvider;
    private readonly IHex1bTerminal _terminal;
    private readonly Hex1bRenderContext _context;
    private readonly bool _ownsTerminal;
    private readonly RootContext _rootContext = new();
    private readonly FocusRing _focusRing = new();
    private Hex1bNode? _rootNode;
    
    // Mouse tracking
    private int _mouseX = -1;
    private int _mouseY = -1;
    private bool _mouseEnabled;
    
    // Hover tracking
    private Hex1bNode? _hoveredNode;
    
    // Click tracking for double/triple click detection
    private DateTime _lastClickTime;
    private int _lastClickX = -1;
    private int _lastClickY = -1;
    private MouseButton _lastClickButton = MouseButton.None;
    private int _currentClickCount;
    
    /// <summary>
    /// Maximum time between clicks (in milliseconds) to count as a multi-click.
    /// </summary>
    private const int DoubleClickThresholdMs = 500;
    
    /// <summary>
    /// Maximum distance (in cells) between clicks to count as a multi-click.
    /// </summary>
    private const int DoubleClickDistance = 1;
    
    // Drag state - when active, all mouse events route to the drag handler
    private DragHandler? _activeDragHandler;
    private int _dragStartX;
    private int _dragStartY;
    
    // Channel for signaling that a re-render is needed (from Invalidate() calls)
    private readonly Channel<bool> _invalidateChannel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) 
        { 
            FullMode = BoundedChannelFullMode.DropOldest 
        });
    
    // Rescue (error boundary) support
    private readonly bool _rescueEnabled;
    private readonly Func<RescueState, Hex1bWidget>? _rescueFallbackBuilder;
    private readonly IReadOnlyList<RescueAction> _rescueActions;
    private readonly RescueState _rescueState = new();
    
    // Stop request flag - set by InputBindingActionContext.RequestStop()
    private volatile bool _stopRequested;
    
    // Default CTRL-C binding option
    private readonly bool _enableDefaultCtrlCExit;

    /// <summary>
    /// Creates a Hex1bApp with an async widget builder.
    /// </summary>
    /// <param name="builder">A function that builds the widget tree.</param>
    /// <param name="options">Optional configuration options.</param>
    public Hex1bApp(
        Func<RootContext, Task<Hex1bWidget>> builder,
        Hex1bAppOptions? options = null)
    {
        options ??= new Hex1bAppOptions();
        
        _rootComponent = builder;
        _themeProvider = options.ThemeProvider;
        
        // Check if mouse is enabled in options
        _mouseEnabled = options.EnableMouse;
        
        // Create terminal with mouse support if enabled
        if (options.Terminal != null)
        {
            _terminal = options.Terminal;
        }
        else
        {
            _terminal = new ConsoleHex1bTerminal(enableMouse: _mouseEnabled);
        }
        _ownsTerminal = options.OwnsTerminal ?? (options.Terminal == null);
        
        var initialTheme = options.ThemeProvider?.Invoke() ?? options.Theme;
        _context = new Hex1bRenderContext(_terminal, initialTheme);
        
        // Rescue (error boundary) options
        _rescueEnabled = options.EnableRescue;
        _rescueFallbackBuilder = options.RescueFallbackBuilder;
        _rescueActions = options.RescueActions ?? [];
        
        // Default CTRL-C binding option
        _enableDefaultCtrlCExit = options.EnableDefaultCtrlCExit;
    }

    /// <summary>
    /// Creates a Hex1bApp with a synchronous widget builder.
    /// </summary>
    /// <param name="builder">A function that builds the widget tree.</param>
    /// <param name="options">Optional configuration options.</param>
    public Hex1bApp(
        Func<RootContext, Hex1bWidget> builder,
        Hex1bAppOptions? options = null)
        : this(ctx => Task.FromResult(builder(ctx)), options)
    {
    }

    /// <summary>
    /// Signals that the UI should be re-rendered. 
    /// Call this when external state changes (network events, timers, etc.).
    /// This method is thread-safe and can be called from any thread.
    /// </summary>
    /// <remarks>
    /// Multiple rapid calls are coalesced into a single re-render.
    /// </remarks>
    public void Invalidate()
    {
        // TryWrite with DropOldest ensures we don't block and coalesce rapid invalidations
        _invalidateChannel.Writer.TryWrite(true);
    }

    /// <summary>
    /// Requests the application to stop. The RunAsync call will exit gracefully
    /// after the current frame completes.
    /// </summary>
    public void RequestStop()
    {
        _stopRequested = true;
        // Also signal invalidation to wake up the main loop immediately
        Invalidate();
    }

    /// <summary>
    /// Runs the application until cancellation is requested.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _context.EnterAlternateScreen();
        try
        {
            // Initial render
            await RenderFrameAsync(cancellationToken);

            // React to input events and invalidation signals
            while (!cancellationToken.IsCancellationRequested && !_stopRequested)
            {
                // Wait for either an input event or an invalidation signal
                var inputTask = _terminal.InputEvents.ReadAsync(cancellationToken).AsTask();
                var invalidateTask = _invalidateChannel.Reader.ReadAsync(cancellationToken).AsTask();
                
                var completedTask = await Task.WhenAny(inputTask, invalidateTask);
                
                if (completedTask == inputTask)
                {
                    var inputEvent = await inputTask;
                    
                    switch (inputEvent)
                    {
                        // Terminal capability events (e.g., DA1 for Sixel detection) are handled at app level
                        case Hex1bTerminalEvent terminalEvent:
                            Nodes.SixelNode.HandleDA1Response(terminalEvent.Response);
                            // Re-render to reflect the updated capability detection
                            await RenderFrameAsync(cancellationToken);
                            continue;
                        
                        // Resize events trigger a re-layout and re-render
                        case Hex1bResizeEvent:
                            // Just re-render - the terminal's Width/Height properties will reflect the new size
                            break;
                        
                        // Key events are routed to the focused node through the tree
                        case Hex1bKeyEvent keyEvent when _rootNode != null:
                            // Use input routing system - routes to focused node, checks bindings, then calls HandleInput
                            await InputRouter.RouteInputAsync(_rootNode, keyEvent, _focusRing, RequestStop, cancellationToken);
                            break;
                        
                        // Mouse events: update cursor position and handle clicks/drags
                        case Hex1bMouseEvent mouseEvent:
                            _mouseX = mouseEvent.X;
                            _mouseY = mouseEvent.Y;
                            
                            // Update hover state on any mouse event
                            UpdateHoverState(mouseEvent.X, mouseEvent.Y);
                            
                            // If a drag is active, route all events to the drag handler
                            if (_activeDragHandler != null)
                            {
                                // Handle both Move (no button) and Drag (button held) actions
                                if (mouseEvent.Action == MouseAction.Move || mouseEvent.Action == MouseAction.Drag)
                                {
                                    var deltaX = mouseEvent.X - _dragStartX;
                                    var deltaY = mouseEvent.Y - _dragStartY;
                                    _activeDragHandler.OnMove?.Invoke(deltaX, deltaY);
                                }
                                else if (mouseEvent.Action == MouseAction.Up)
                                {
                                    _activeDragHandler.OnEnd?.Invoke();
                                    _activeDragHandler = null;
                                }
                                // While dragging, skip normal event handling
                                break;
                            }
                            
                            // Handle click events (button down) - may start a drag
                            if (mouseEvent.Action == MouseAction.Down && mouseEvent.Button != MouseButton.None)
                            {
                                await HandleMouseClickAsync(mouseEvent, cancellationToken);
                            }
                            break;
                    }
                }
                // If invalidateTask completed, we just need to re-render (no input to handle)

                // Re-render after handling input or invalidation (state may have changed)
                await RenderFrameAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal cancellation, exit gracefully
        }
        catch (ChannelClosedException)
        {
            // Terminal input channel closed, exit gracefully
        }
        finally
        {
            // Always exit alternate buffer, even on error
            _context.ExitAlternateScreen();
        }
    }

    private async Task RenderFrameAsync(CancellationToken cancellationToken)
    {
        // Update theme if we have a dynamic theme provider
        if (_themeProvider != null)
        {
            _context.Theme = _themeProvider();
        }

        // Update the cancellation token on the root context
        _rootContext.CancellationToken = cancellationToken;

        // Step 1: Call the root component to get the widget tree
        Hex1bWidget widgetTree;
        try
        {
            widgetTree = await _rootComponent(_rootContext);
        }
        catch (Exception ex) when (_rescueEnabled)
        {
            // Build phase failed - capture and use rescue fallback
            _rescueState.SetError(ex, RescueErrorPhase.Build);
            widgetTree = BuildRescueFallback();
        }
        
        // Step 2: Wrap in rescue widget if enabled (catches Reconcile/Measure/Arrange/Render)
        if (_rescueEnabled && !_rescueState.HasError)
        {
            widgetTree = new RescueWidget(widgetTree, _rescueState, _rescueFallbackBuilder, actions: _rescueActions);
        }

        // Step 3: Reconcile - update the node tree to match the widget tree
        _rootNode = Reconcile(_rootNode, widgetTree);

        // Step 4: Layout - measure and arrange the node tree
        if (_rootNode != null)
        {
            var terminalSize = new Size(_context.Width, _context.Height);
            var constraints = Constraints.Tight(terminalSize);
            _rootNode.Measure(constraints);
            _rootNode.Arrange(Rect.FromSize(terminalSize));
        }

        // Step 5: Rebuild focus ring from the current node tree
        _focusRing.Rebuild(_rootNode);
        _focusRing.EnsureFocus();

        // Step 6: Update render context with mouse position for hover rendering
        _context.MouseX = _mouseX;
        _context.MouseY = _mouseY;

        // Step 7: Render the node tree to the terminal
        _context.Clear();
        _rootNode?.Render(_context);
        
        // Step 7: Render mouse cursor overlay if enabled
        RenderMouseCursor();
    }
    
    /// <summary>
    /// Builds the fallback widget when the rescue catches an error.
    /// </summary>
    private Hex1bWidget BuildRescueFallback()
    {
        if (_rescueFallbackBuilder != null)
        {
            return _rescueFallbackBuilder(_rescueState);
        }
        
        return RescueNode.BuildDefaultFallback(_rescueState, actions: _rescueActions);
    }
    
    /// <summary>
    /// Renders the mouse cursor overlay at the current mouse position.
    /// </summary>
    private void RenderMouseCursor()
    {
        if (!_mouseEnabled || _mouseX < 0 || _mouseY < 0) return;
        if (_mouseX >= _context.Width || _mouseY >= _context.Height) return;
        
        var showCursor = _context.Theme.Get(MouseTheme.ShowCursor);
        if (!showCursor) return;
        
        var fgColor = _context.Theme.Get(MouseTheme.CursorForegroundColor);
        var bgColor = _context.Theme.Get(MouseTheme.CursorBackgroundColor);
        
        // Position cursor and render a highlighted block
        // We use a special marker character or just change the colors at that position
        _context.SetCursorPosition(_mouseX, _mouseY);
        
        var colorCodes = "";
        if (!fgColor.IsDefault) colorCodes += fgColor.ToForegroundAnsi();
        if (!bgColor.IsDefault) colorCodes += bgColor.ToBackgroundAnsi();
        
        // Render a visible cursor marker (block cursor style)
        // Using a space with background color, or a special character
        _context.Write($"{colorCodes} \x1b[0m");
    }
    
    /// <summary>
    /// Handles a mouse click by hit testing and routing through bindings.
    /// May initiate a drag if a drag binding matches.
    /// </summary>
    private async Task HandleMouseClickAsync(Hex1bMouseEvent mouseEvent, CancellationToken cancellationToken)
    {
        // Compute click count for double/triple click detection
        var clickCount = ComputeClickCount(mouseEvent);
        var eventWithClickCount = mouseEvent.WithClickCount(clickCount);
        
        // Find the focusable node at the click position
        var hitNode = _focusRing.HitTest(mouseEvent.X, mouseEvent.Y);
        
        if (hitNode == null) return;
        
        // Focus the clicked node (if not already focused)
        if (!hitNode.IsFocused)
        {
            _focusRing.Focus(hitNode);
        }
        
        // Calculate local coordinates for this node
        var localX = mouseEvent.X - hitNode.Bounds.X;
        var localY = mouseEvent.Y - hitNode.Bounds.Y;
        
        // Create action context for mouse bindings (includes mouse coordinates)
        var actionContext = new InputBindingActionContext(_focusRing, RequestStop, cancellationToken, mouseEvent.X, mouseEvent.Y);
        
        // Check if the node has a drag binding for this event (checked first)
        var builder = hitNode.BuildBindings();
        foreach (var dragBinding in builder.DragBindings)
        {
            if (dragBinding.Matches(eventWithClickCount))
            {
                // Start the drag - capture mouse until release
                _activeDragHandler = dragBinding.StartDrag(localX, localY);
                _dragStartX = mouseEvent.X;
                _dragStartY = mouseEvent.Y;
                return;
            }
        }
        
        // Check mouse bindings in order of decreasing click count
        // This ensures double-click bindings are checked before single-click bindings
        var sortedBindings = builder.MouseBindings
            .OrderByDescending(b => b.ClickCount)
            .ToList();
        
        foreach (var mouseBinding in sortedBindings)
        {
            if (mouseBinding.Matches(eventWithClickCount))
            {
                await mouseBinding.ExecuteAsync(actionContext);
                return; // First match wins
            }
        }
        
        // No binding matched - call the node's HandleMouseClick with local coordinates
        hitNode.HandleMouseClick(localX, localY, eventWithClickCount);
    }
    
    /// <summary>
    /// Computes the click count for a mouse down event based on timing and position.
    /// </summary>
    private int ComputeClickCount(Hex1bMouseEvent mouseEvent)
    {
        var now = DateTime.UtcNow;
        var timeSinceLastClick = (now - _lastClickTime).TotalMilliseconds;
        var distanceX = Math.Abs(mouseEvent.X - _lastClickX);
        var distanceY = Math.Abs(mouseEvent.Y - _lastClickY);
        
        // Check if this is a continuation of a multi-click sequence
        if (mouseEvent.Button == _lastClickButton &&
            timeSinceLastClick <= DoubleClickThresholdMs &&
            distanceX <= DoubleClickDistance &&
            distanceY <= DoubleClickDistance)
        {
            // Increment click count (cap at 3 for triple-click)
            _currentClickCount = Math.Min(_currentClickCount + 1, 3);
        }
        else
        {
            // New click sequence
            _currentClickCount = 1;
        }
        
        // Update tracking state
        _lastClickTime = now;
        _lastClickX = mouseEvent.X;
        _lastClickY = mouseEvent.Y;
        _lastClickButton = mouseEvent.Button;
        
        return _currentClickCount;
    }
    
    /// <summary>
    /// Updates the hover state based on the current mouse position.
    /// Clears hover from the previously hovered node and sets it on the new one.
    /// </summary>
    private void UpdateHoverState(int mouseX, int mouseY)
    {
        // Find the focusable node at the mouse position
        var hitNode = _focusRing.HitTest(mouseX, mouseY);
        
        // If hover hasn't changed, nothing to do
        if (ReferenceEquals(hitNode, _hoveredNode))
        {
            return;
        }
        
        // Clear hover from the previous node
        if (_hoveredNode != null)
        {
            _hoveredNode.IsHovered = false;
        }
        
        // Set hover on the new node
        _hoveredNode = hitNode;
        if (_hoveredNode != null)
        {
            _hoveredNode.IsHovered = true;
        }
    }

    /// <summary>
    /// Reconciles a node with a widget, creating/updating/replacing as needed.
    /// This is the core of the "diffing" algorithm.
    /// </summary>
    private Hex1bNode? Reconcile(Hex1bNode? existingNode, Hex1bWidget? widget)
    {
        if (widget is null)
        {
            return null;
        }

        // Create the root reconcile context
        var context = ReconcileContext.CreateRoot();
        context.IsNew = existingNode is null || existingNode.GetType() != widget.GetExpectedNodeType();
        
        // Delegate to the widget's own Reconcile method
        var node = widget.Reconcile(existingNode, context);

        // Set common properties on the reconciled node
        node.Parent = null; // Root has no parent
        
        // Inject default CTRL-C binding if enabled
        if (_enableDefaultCtrlCExit)
        {
            var userConfigurator = widget.BindingsConfigurator;
            node.BindingsConfigurator = builder =>
            {
                // Add default CTRL-C binding first
                builder.Ctrl().Key(Hex1bKey.C).Action(_ => RequestStop(), "Exit application");
                
                // Then apply user's bindings (later registrations override earlier ones in trie)
                userConfigurator?.Invoke(builder);
            };
        }
        else
        {
            node.BindingsConfigurator = widget.BindingsConfigurator;
        }
        
        node.WidthHint = widget.WidthHint;
        node.HeightHint = widget.HeightHint;

        return node;
    }

    public void Dispose()
    {
        // Complete the invalidate channel
        _invalidateChannel.Writer.TryComplete();
        
        if (_ownsTerminal && _terminal is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
