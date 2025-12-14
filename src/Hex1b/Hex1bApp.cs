#pragma warning disable HEX1B_SIXEL // Sixel API is experimental - internal usage is allowed

using System.ComponentModel;
using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A Hex1bApp with typed state management.
/// </summary>
/// <typeparam name="TState">The application state type.</typeparam>
public class Hex1bApp<TState> : IDisposable
{
    private readonly Func<RootContext<TState>, CancellationToken, Task<Hex1bWidget>> _rootComponent;
    private readonly Func<Hex1bTheme>? _themeProvider;
    private readonly IHex1bTerminal _terminal;
    private readonly Hex1bRenderContext _context;
    private readonly bool _ownsTerminal;
    private readonly RootContext<TState> _rootContext;
    private readonly FocusRing _focusRing = new();
    private Hex1bNode? _rootNode;
    
    // Mouse tracking
    private int _mouseX = -1;
    private int _mouseY = -1;
    private bool _mouseEnabled;
    
    // Channel for signaling that a re-render is needed (from Invalidate() calls)
    private readonly Channel<bool> _invalidateChannel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) 
        { 
            FullMode = BoundedChannelFullMode.DropOldest 
        });
    
    // Track if we're subscribed to INotifyPropertyChanged for cleanup
    private readonly PropertyChangedEventHandler? _propertyChangedHandler;

    /// <summary>
    /// The application state, accessible for external state mutations.
    /// </summary>
    public TState State { get; }

    /// <summary>
    /// Creates a Hex1bApp with typed state and an async widget builder.
    /// </summary>
    public Hex1bApp(
        TState state,
        Func<RootContext<TState>, CancellationToken, Task<Hex1bWidget>> builder,
        Hex1bAppOptions? options = null)
    {
        options ??= new Hex1bAppOptions();
        
        State = state;
        _rootContext = new RootContext<TState>(state);
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
        
        // Auto-subscribe to INotifyPropertyChanged if state implements it
        if (state is INotifyPropertyChanged notifyPropertyChanged)
        {
            _propertyChangedHandler = (_, _) => Invalidate();
            notifyPropertyChanged.PropertyChanged += _propertyChangedHandler;
        }
    }

    /// <summary>
    /// Creates a Hex1bApp with typed state and a synchronous widget builder.
    /// </summary>
    public Hex1bApp(
        TState state,
        Func<RootContext<TState>, Hex1bWidget> builder,
        Hex1bAppOptions? options = null)
        : this(state, (ctx, ct) => Task.FromResult(builder(ctx)), options)
    {
    }

    /// <summary>
    /// Signals that the UI should be re-rendered. 
    /// Call this when external state changes (network events, timers, etc.).
    /// This method is thread-safe and can be called from any thread.
    /// </summary>
    /// <remarks>
    /// If the state implements <see cref="INotifyPropertyChanged"/>, this is called
    /// automatically when properties change. For other state changes, call this manually.
    /// Multiple rapid calls are coalesced into a single re-render.
    /// </remarks>
    public void Invalidate()
    {
        // TryWrite with DropOldest ensures we don't block and coalesce rapid invalidations
        _invalidateChannel.Writer.TryWrite(true);
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
            while (!cancellationToken.IsCancellationRequested)
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
                            InputRouter.RouteInput(_rootNode, keyEvent, _focusRing);
                            break;
                        
                        // Mouse events: update cursor position and handle clicks
                        case Hex1bMouseEvent mouseEvent:
                            _mouseX = mouseEvent.X;
                            _mouseY = mouseEvent.Y;
                            
                            // Handle click events (button down)
                            if (mouseEvent.Action == MouseAction.Down && mouseEvent.Button != MouseButton.None)
                            {
                                HandleMouseClick(mouseEvent);
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

        // Step 1: Call the root component to get the widget tree
        var widgetTree = await _rootComponent(_rootContext, cancellationToken);

        // Step 2: Reconcile - update the node tree to match the widget tree
        _rootNode = Reconcile(_rootNode, widgetTree);

        // Step 3: Layout - measure and arrange the node tree
        if (_rootNode != null)
        {
            var terminalSize = new Size(_context.Width, _context.Height);
            var constraints = Constraints.Tight(terminalSize);
            _rootNode.Measure(constraints);
            _rootNode.Arrange(Rect.FromSize(terminalSize));
        }

        // Step 4: Rebuild focus ring from the current node tree
        _focusRing.Rebuild(_rootNode);
        _focusRing.EnsureFocus();

        // Step 5: Render the node tree to the terminal
        _context.Clear();
        _rootNode?.Render(_context);
        
        // Step 6: Render mouse cursor overlay if enabled
        RenderMouseCursor();
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
    /// </summary>
    private void HandleMouseClick(Hex1bMouseEvent mouseEvent)
    {
        // Find the focusable node at the click position
        var hitNode = _focusRing.HitTest(mouseEvent.X, mouseEvent.Y);
        
        if (hitNode == null) return;
        
        // Focus the clicked node (if not already focused)
        if (!hitNode.IsFocused)
        {
            _focusRing.Focus(hitNode);
        }
        
        // Check if the node has a mouse binding for this event
        var builder = hitNode.BuildBindings();
        foreach (var mouseBinding in builder.MouseBindings)
        {
            if (mouseBinding.Matches(mouseEvent))
            {
                mouseBinding.Execute();
                return; // First match wins
            }
        }
        
        // No binding matched - call the node's HandleMouseClick with local coordinates
        var localX = mouseEvent.X - hitNode.Bounds.X;
        var localY = mouseEvent.Y - hitNode.Bounds.Y;
        hitNode.HandleMouseClick(localX, localY, mouseEvent);
    }

    /// <summary>
    /// Reconciles a node with a widget, creating/updating/replacing as needed.
    /// This is the core of the "diffing" algorithm.
    /// </summary>
    private static Hex1bNode? Reconcile(Hex1bNode? existingNode, Hex1bWidget? widget)
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
        node.BindingsConfigurator = widget.BindingsConfigurator;
        node.WidthHint = widget.WidthHint;
        node.HeightHint = widget.HeightHint;

        return node;
    }

    public void Dispose()
    {
        // Unsubscribe from INotifyPropertyChanged if we subscribed
        if (_propertyChangedHandler != null && State is INotifyPropertyChanged notifyPropertyChanged)
        {
            notifyPropertyChanged.PropertyChanged -= _propertyChangedHandler;
        }
        
        // Complete the invalidate channel
        _invalidateChannel.Writer.TryComplete();
        
        if (_ownsTerminal && _terminal is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
