#pragma warning disable HEX1B_SIXEL // Sixel API is experimental - internal usage is allowed

using System.Diagnostics;
using System.Threading.Channels;
using Hex1b.Animation;
using Hex1b.Diagnostics;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
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
public class Hex1bApp : IDisposable, IAsyncDisposable, IDiagnosticTreeProvider
{
    private readonly Func<RootContext, Task<Hex1bWidget>> _rootComponent;
    private readonly Func<Hex1bTheme>? _themeProvider;
    private readonly IHex1bAppTerminalWorkloadAdapter _adapter;
    private readonly Hex1bTerminal? _ownedTerminal; // Terminal we created and should dispose
    private readonly Hex1bRenderContext _context;
    private readonly RootContext _rootContext = new();
    private readonly FocusRing _focusRing = new();
    private readonly InputRouterState _inputRouterState = new();
    private Hex1bNode? _rootNode;
    
    // Theme tracking for dirty detection when theme changes
    private Hex1bTheme? _previousTheme;
    
    // Mouse tracking
    private int _mouseX = -1;
    private int _mouseY = -1;
    private bool _mouseEnabled;
    
    // Hover tracking
    private Hex1bNode? _hoveredNode;
    
    // Cursor state tracking for mouse cursor rendering - only update when changed
    private int _lastRenderedCursorX = -1;
    private int _lastRenderedCursorY = -1;
    private CursorShape _lastRenderedCursorShape = CursorShape.Default;
    private bool _lastRenderedCursorVisible = false;
    private Hex1bNode? _lastRenderedCursorNode;
    
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
    private const int DoubleClickDistance = 0;
    
    // Drag state - when active, all mouse events route to the drag handler
    private DragHandler? _activeDragHandler;
    private Hex1bNode? _activeDragNode;
    private int _dragStartX;
    private int _dragStartY;
    
    // Render optimization - track if this is the first frame (needs full clear)
    private bool _isFirstFrame = true;
    
    // Channel for signaling that a re-render is needed (from Invalidate() calls)
    private readonly Channel<bool> _invalidateChannel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) 
        { 
            FullMode = BoundedChannelFullMode.DropOldest 
        });
    
    // Rescue (error boundary) support
    private readonly bool _rescueEnabled;
    private readonly Func<RescueContext, Hex1bWidget>? _rescueFallbackBuilder;
    private readonly Action<Events.RescueEventArgs>? _onRescue;
    private readonly Action<Events.RescueResetEventArgs>? _onRescueReset;
    
    // Stop request flag - set by InputBindingActionContext.RequestStop()
    private volatile bool _stopRequested;
    
    // Pending focus request - will be processed after next render
    private Func<Hex1bNode, bool>? _pendingFocusPredicate;
    
    // Default CTRL-C binding option
    private readonly bool _enableDefaultCtrlCExit;
    
    // Input coalescing options
    private readonly bool _enableInputCoalescing;
    private readonly int _inputCoalescingInitialDelayMs;
    private readonly int _inputCoalescingMaxDelayMs;
    
    // Surface rendering double-buffer
    private Surface? _currentSurface;
    private Surface? _previousSurface;

    // Optional pool for temporary surfaces (SurfaceWidget layers, effect panels, etc.)
    private readonly SurfacePool? _surfacePool;
    
    // Animation timer for RedrawAfter() support
    private readonly AnimationTimer _animationTimer;
    
    // Window manager registry for accessing WindowManagers from anywhere
    private readonly WindowManagerRegistry _windowManagerRegistry = new();
    
    // Diagnostic timing (opt-in, zero-alloc when disabled)
    private bool _diagnosticTimingEnabled;
    private long _diagBuildTicks;
    private long _diagReconcileTicks;
    private long _diagRenderTicks;
    
    // Metrics instrumentation
    private readonly Diagnostics.Hex1bMetrics _metrics;

    /// <summary>
    /// Gets the registry of WindowManagers for this application.
    /// </summary>
    /// <remarks>
    /// WindowPanels automatically register their managers here.
    /// Use this to access window managers from anywhere in the app.
    /// </remarks>
    internal WindowManagerRegistry WindowManagers => _windowManagerRegistry;

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
        _metrics = options.Metrics ?? Diagnostics.Hex1bMetrics.Default;
        
        // Create animation timer with configured frame rate limit
        var frameRateLimitMs = Math.Max(1, options.FrameRateLimitMs);
        _animationTimer = new AnimationTimer(TimeSpan.FromMilliseconds(frameRateLimitMs));
        
        // Check if mouse is enabled in options
        _mouseEnabled = options.EnableMouse;
        
        // Create or use provided adapter
        if (options.WorkloadAdapter != null)
        {
            // Use provided adapter directly
            _adapter = options.WorkloadAdapter;
            _ownedTerminal = null;
        }
        else
        {
            // Default: create console terminal using new architecture
            var presentation = new ConsolePresentationAdapter(enableMouse: _mouseEnabled);
            var workload = new Hex1bAppWorkloadAdapter(presentation.Capabilities);
            _ownedTerminal = new Hex1bTerminal(new Hex1bTerminalOptions
            {
                PresentationAdapter = presentation,
                WorkloadAdapter = workload
            });
            _adapter = workload;
        }
        
        var initialTheme = options.ThemeProvider?.Invoke() ?? options.Theme;
        _context = new Hex1bRenderContext(_adapter, initialTheme);
        
        // Rescue (error boundary) options
        _rescueEnabled = options.EnableRescue;
        _rescueFallbackBuilder = options.RescueFallbackBuilder;
        _onRescue = options.OnRescue;
        _onRescueReset = options.OnRescueReset;
        
        // Default CTRL-C binding option
        _enableDefaultCtrlCExit = options.EnableDefaultCtrlCExit;
        
        // Input coalescing options
        _enableInputCoalescing = options.EnableInputCoalescing;
        _inputCoalescingInitialDelayMs = options.InputCoalescingInitialDelayMs;
        _inputCoalescingMaxDelayMs = options.InputCoalescingMaxDelayMs;

        _surfacePool = options.EnableSurfacePooling
            ? new SurfacePool(options.SurfacePoolMaxSurfacesPerBucket, options.SurfacePoolMaxIdleFrames)
            : null;
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
    /// Captures all input to the specified node.
    /// </summary>
    /// <param name="node">The node to capture input to.</param>
    /// <remarks>
    /// <para>
    /// When a node captures input, it receives all keyboard and mouse events directly,
    /// bypassing normal binding lookup. This is used by embedded terminals and other
    /// controls that need raw input.
    /// </para>
    /// <para>
    /// Only bindings marked with <see cref="InputBinding.OverridesCapture"/> will be
    /// checked before the captured node receives input. Global bindings always override capture.
    /// </para>
    /// </remarks>
    public void CaptureInput(Hex1bNode node)
    {
        _focusRing.CaptureInput(node);
    }
    
    /// <summary>
    /// Releases input capture, returning to normal input routing.
    /// </summary>
    public void ReleaseCapture()
    {
        _focusRing.ReleaseCapture();
    }
    
    /// <summary>
    /// Gets the node that has captured all input, or null if no capture is active.
    /// </summary>
    public Hex1bNode? CapturedNode => _focusRing.CapturedNode;

    /// <summary>
    /// Gets the currently focused node, or null if no node has focus.
    /// Useful for testing and debugging focus state.
    /// </summary>
    public Hex1bNode? FocusedNode => _focusRing.FocusedNode;

    /// <summary>
    /// Gets all focusable nodes in the current focus ring.
    /// Useful for testing and debugging focus navigation.
    /// </summary>
    public IReadOnlyList<Hex1bNode> Focusables => _focusRing.Focusables;

    /// <summary>
    /// Gets the last focus change debug log from the focus ring.
    /// Useful for testing focus navigation.
    /// </summary>
    public string? LastFocusChange => _focusRing.LastFocusChange;
    
    /// <summary>
    /// Gets the last hit test debug log from the focus ring.
    /// Useful for debugging mouse click focus issues.
    /// </summary>
    public string? LastHitTestDebug => _focusRing.LastHitTestDebug;
    
    /// <summary>
    /// Gets the last Focus() call debug log.
    /// </summary>
    public string? LastFocusDebug => _focusRing.LastFocusDebug;

    /// <summary>
    /// Focuses the first node in the focus ring that matches the given predicate.
    /// </summary>
    /// <param name="predicate">A function that returns true for the node to focus.</param>
    /// <returns>True if a matching node was found and focused, false otherwise.</returns>
    /// <remarks>
    /// This is useful for programmatically setting focus, for example after creating a new
    /// focusable widget. The focus ring is rebuilt after each render, so this should be called
    /// after the app has rendered at least once with the target widget present.
    /// </remarks>
    public bool FocusWhere(Func<Hex1bNode, bool> predicate)
    {
        return _focusRing.FocusWhere(predicate);
    }

    /// <summary>
    /// Requests that focus be set to the first node matching the predicate after the next render.
    /// </summary>
    /// <param name="predicate">A function that returns true for the node to focus.</param>
    /// <remarks>
    /// <para>
    /// This is useful when adding a new focusable widget and wanting to focus it immediately.
    /// Since the node doesn't exist in the focus ring until after the render cycle, calling
    /// <see cref="FocusWhere"/> directly won't work. Instead, use this method to queue the
    /// focus request, then call <see cref="Invalidate"/> to trigger a render.
    /// </para>
    /// <para>
    /// After the render completes and the focus ring is rebuilt, the pending focus request
    /// will be processed before <c>EnsureFocus()</c> is called.
    /// </para>
    /// </remarks>
    public void RequestFocus(Func<Hex1bNode, bool> predicate)
    {
        _pendingFocusPredicate = predicate;
    }

    /// <summary>
    /// Gets the last path debug info from input routing.
    /// </summary>
    public string? LastPathDebug => Input.InputRouter.LastPathDebug;

    /// <summary>
    /// Copies the specified text to the system clipboard using the OSC 52 escape sequence.
    /// </summary>
    /// <param name="text">The text to copy to the clipboard.</param>
    /// <remarks>
    /// OSC 52 is the standard escape sequence for clipboard access:
    /// ESC ] 52 ; c ; &lt;base64-data&gt; ST
    /// 
    /// The text is base64-encoded and sent via the OSC 52 sequence. Not all terminals
    /// support this feature, but most modern terminals do.
    /// </remarks>
    public void CopyToClipboard(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        
        // Base64 encode the text
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));
        
        // Send OSC 52 sequence: ESC ] 52 ; c ; <base64> ST
        // Using BEL (\a or \x07) as string terminator for better compatibility
        var osc52 = $"\x1b]52;c;{base64}\x07";
        _adapter.Write(osc52);
    }

    /// <summary>
    /// Runs the application until cancellation is requested.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Register this app as the diagnostic tree provider if the adapter supports it
        if (_adapter is Hex1bAppWorkloadAdapter workloadAdapter)
        {
            workloadAdapter.DiagnosticTreeProvider = this;
            _diagnosticTimingEnabled = workloadAdapter.DiagnosticTimingEnabled;
        }
        
        _context.EnterAlternateScreen();
        try
        {
            // Initial render
            await RenderFrameAsync(cancellationToken);

            // Use an explicit shutdown signal task so normal wakeups do not rely on cancellation exceptions.
            var shutdownSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationRegistration = cancellationToken.Register(static state =>
            {
                var tcs = (TaskCompletionSource)state!;
                tcs.TrySetResult();
            }, shutdownSignal);
            var shutdownTask = shutdownSignal.Task;

            Task<bool>? inputWaitTask = null;
            Task<bool>? invalidateWaitTask = null;

            // React to input events, invalidation signals, and animation timers
            while (!shutdownTask.IsCompleted && !_stopRequested)
            {
                // Fire any due animation timers before waiting
                _animationTimer.FireDue();

                inputWaitTask ??= _adapter.InputEvents.WaitToReadAsync().AsTask();
                invalidateWaitTask ??= _invalidateChannel.Reader.WaitToReadAsync().AsTask();

                var timeUntilTimer = _animationTimer.GetTimeUntilNextDue();
                Task completedTask;

                if (timeUntilTimer.HasValue)
                {
                    // Keep timer waits non-cancelable so steady-state render pacing doesn't throw cancellations.
                    var timerWaitTask = Task.Delay(timeUntilTimer.Value);
                    completedTask = await Task.WhenAny(inputWaitTask, invalidateWaitTask, timerWaitTask, shutdownTask);
                }
                else
                {
                    completedTask = await Task.WhenAny(inputWaitTask, invalidateWaitTask, shutdownTask);
                }

                if (completedTask == shutdownTask || cancellationToken.IsCancellationRequested)
                    break;

                // Fire any timers that are now due (may have triggered the wake-up)
                _animationTimer.FireDue();

                // Consume completion state from non-throwing wait tasks.
                var inputReady = false;
                if (inputWaitTask.IsCompleted)
                {
                    inputReady = await inputWaitTask;
                    inputWaitTask = null;
                }
                if (invalidateWaitTask.IsCompleted)
                {
                    _ = await invalidateWaitTask;
                    invalidateWaitTask = null;
                }

                // Process input - the approach depends on whether input was ready
                if (inputReady)
                {
                    // Input woke us - read and process ONE input event
                    if (_adapter.InputEvents.TryRead(out var inputEvent))
                    {
                        await ProcessInputEventAsync(inputEvent, cancellationToken);
                    }
                    
                    // Input coalescing: batch rapid inputs together before rendering
                    if (_enableInputCoalescing)
                    {
                        var outputBacklog = _adapter.OutputQueueDepth;
                        var coalescingDelayMs = Math.Min(
                            _inputCoalescingInitialDelayMs + (outputBacklog * 10), 
                            _inputCoalescingMaxDelayMs);
                        await Task.Delay(coalescingDelayMs, cancellationToken);
                    
                        while (_adapter.InputEvents.TryRead(out var delayedInput))
                        {
                            await ProcessInputEventAsync(delayedInput, cancellationToken);
                            if (_stopRequested || cancellationToken.IsCancellationRequested)
                                break;
                        }
                    }
                }
                else
                {
                    // Timer or invalidation woke us - drain ALL pending input first
                    // This ensures resize events are never starved by animation timers
                    while (_adapter.InputEvents.TryRead(out var pendingInput))
                    {
                        await ProcessInputEventAsync(pendingInput, cancellationToken);
                        if (_stopRequested || cancellationToken.IsCancellationRequested)
                            break;
                    }
                }

                // Re-render after handling input or invalidation (state may have changed)
                await RenderFrameAsync(cancellationToken);
                
                // IMPORTANT: Handle race condition where output arrived during render.
                // If invalidation was signaled while we were rendering, we need to re-render
                // before blocking on WhenAny, otherwise content may not appear until next input.
                // Limit to 2 extra renders to prevent animation timer cascades from starving input.
                int extraRenders = 0;
                const int maxExtraRenders = 2;
                
                while (_invalidateChannel.Reader.TryRead(out _) && extraRenders < maxExtraRenders)
                {
                    // ALWAYS process pending input before each re-render to prevent starvation
                    while (_adapter.InputEvents.TryRead(out var pendingEvent))
                    {
                        await ProcessInputEventAsync(pendingEvent, cancellationToken);
                        if (_stopRequested || cancellationToken.IsCancellationRequested)
                            break;
                    }
                    
                    if (_stopRequested || cancellationToken.IsCancellationRequested)
                        break;
                    
                    // Fire any timers that became due
                    _animationTimer.FireDue();
                        
                    await RenderFrameAsync(cancellationToken);
                    extraRenders++;
                }
                
                // Drain any remaining invalidations without rendering (will be caught next loop)
                while (_invalidateChannel.Reader.TryRead(out _)) { }
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

    /// <summary>
    /// Processes a single input event (key, mouse, resize, etc.).
    /// </summary>
    private async Task ProcessInputEventAsync(Hex1bEvent inputEvent, CancellationToken cancellationToken)
    {
        var inputStart = Stopwatch.GetTimestamp();
        var eventType = inputEvent switch
        {
            Hex1bKeyEvent => "key",
            Hex1bMouseEvent => "mouse",
            Hex1bResizeEvent => "resize",
            _ => "other"
        };
        
        switch (inputEvent)
        {
            // Terminal capability events are logged but capabilities are now
            // provided via TerminalCapabilities at startup rather than runtime detection
            case Hex1bTerminalEvent:
                // No action needed - capabilities come from workload adapter
                break;
            
            // Resize events trigger a re-layout and re-render
            case Hex1bResizeEvent resizeEvent:
                // Terminal size changed - we need a full re-render
                // Mark root dirty to ensure everything re-renders at new size
                _rootNode?.MarkDirty();
                // Force full clear to handle new screen regions
                _isFirstFrame = true;
                break;
            
            // Key events are routed to the focused node through the tree
            case Hex1bKeyEvent keyEvent when _rootNode != null:
                // Use input routing system - routes to focused node, checks bindings, then calls HandleInput
                await InputRouter.RouteInputAsync(_rootNode, keyEvent, _focusRing, _inputRouterState, RequestStop, cancellationToken, CopyToClipboard, Invalidate, _windowManagerRegistry);
                break;
            
            // Mouse events: update cursor position and handle clicks/drags
            case Hex1bMouseEvent mouseEvent:
                _mouseX = mouseEvent.X;
                _mouseY = mouseEvent.Y;
                
                // Update hover state on any mouse event
                UpdateHoverState(mouseEvent.X, mouseEvent.Y);
                
                // If a drag is active, route all events to the drag handler
                if (_activeDragHandler != null && _activeDragNode != null)
                {
                    // Create context for drag events
                    var dragContext = new InputBindingActionContext(
                        _focusRing, RequestStop, cancellationToken, 
                        mouseEvent.X, mouseEvent.Y, CopyToClipboard, Invalidate, _windowManagerRegistry);
                    
                    // Handle both Move (no button) and Drag (button held) actions
                    if (mouseEvent.Action == MouseAction.Move || mouseEvent.Action == MouseAction.Drag)
                    {
                        var deltaX = mouseEvent.X - _dragStartX;
                        var deltaY = mouseEvent.Y - _dragStartY;
                        _activeDragHandler.OnMove?.Invoke(dragContext, deltaX, deltaY);
                    }
                    else if (mouseEvent.Action == MouseAction.Up)
                    {
                        _activeDragHandler.OnEnd?.Invoke(dragContext);
                        _activeDragHandler = null;
                        _activeDragNode = null;
                    }
                    // While dragging, skip normal event handling
                    break;
                }
                
                // Handle click events (button down) - may start a drag
                if (mouseEvent.Action == MouseAction.Down && mouseEvent.Button != MouseButton.None)
                {
                    await HandleMouseClickAsync(mouseEvent, cancellationToken);
                }
                // Route other mouse events through InputRouter (for nodes that capture all input)
                else if (_rootNode != null)
                {
                    await InputRouter.RouteInputAsync(_rootNode, mouseEvent, _focusRing, _inputRouterState,
                        RequestStop, cancellationToken, CopyToClipboard, Invalidate, _windowManagerRegistry);
                }
                break;
        }
        
        _metrics.InputCount.Add(1, new KeyValuePair<string, object?>("type", eventType));
        _metrics.InputDuration.Record(
            (Stopwatch.GetTimestamp() - inputStart) * 1000.0 / Stopwatch.Frequency);
    }

    private async Task RenderFrameAsync(CancellationToken cancellationToken)
    {
        // NOTE: We intentionally do NOT clear animation timers here.
        // Clearing would reset any pending timers, and if renders happen frequently
        // (e.g., mouse movement), the timers would never become due - they'd keep
        // getting pushed forward. Let timers accumulate and fire naturally.
        // The callback will mark nodes dirty, which is idempotent.
        
        var frameStart = Stopwatch.GetTimestamp();
        
        // Update theme if we have a dynamic theme provider
        if (_themeProvider != null)
        {
            var newTheme = _themeProvider();
            
            // Check if theme instance changed - if so, force full re-render
            if (!ReferenceEquals(newTheme, _previousTheme))
            {
                _previousTheme = newTheme;
                _context.Theme = newTheme;
                
                // Theme changed - mark all nodes dirty so they re-render with new colors
                MarkSubtreeDirty(_rootNode);
            }
        }

        // Update the cancellation token on the root context
        _rootContext.CancellationToken = cancellationToken;

        // Step 1: Call the root component to get the widget tree
        Hex1bWidget? widgetTree = null;
        Exception? buildException = null;
        
        long buildStart = Stopwatch.GetTimestamp();
        
        try
        {
            widgetTree = await _rootComponent(_rootContext);
        }
        catch (Exception ex) when (_rescueEnabled)
        {
            // Build phase failed - capture exception for RescueWidget to handle
            buildException = ex;
        }
        
        var buildTicks = Stopwatch.GetTimestamp() - buildStart;
        if (_diagnosticTimingEnabled) _diagBuildTicks = buildTicks;

        // Step 2: Wrap in rescue widget if enabled (catches Reconcile/Measure/Arrange/Render and Build failures)
        if (_rescueEnabled)
        {
            var rescueWidget = new RescueWidget(widgetTree) { BuildException = buildException };

            if (_rescueFallbackBuilder != null)
            {
                rescueWidget = rescueWidget.WithFallback(_rescueFallbackBuilder);
            }

            if (_onRescue != null)
            {
                rescueWidget = rescueWidget.OnRescue(_onRescue);
            }

            if (_onRescueReset != null)
            {
                rescueWidget = rescueWidget.OnReset(_onRescueReset);
            }

            widgetTree = rescueWidget;
        }
        
        // Step 2.5: Wrap in root ZStack for popup support
        // This ensures ctx.Popups is always available from any event handler
        // Note: widgetTree is always non-null here - either from _rootComponent or RescueWidget wrapping
        widgetTree = new ZStackWidget([widgetTree!]);

        // Step 3: Reconcile - update the node tree to match the widget tree
        long reconcileTicks;
        {
            long reconcileFrameStart = Stopwatch.GetTimestamp();
            
            _rootNode = await ReconcileAsync(_rootNode, widgetTree, cancellationToken);
            
            reconcileTicks = Stopwatch.GetTimestamp() - reconcileFrameStart;
            if (_diagnosticTimingEnabled) _diagReconcileTicks = reconcileTicks;
        }

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
        
        // Step 5.5: Process any pending focus request
        if (_pendingFocusPredicate != null)
        {
            _focusRing.FocusWhere(_pendingFocusPredicate);
            _pendingFocusPredicate = null;
        }
        
        _focusRing.EnsureFocus();

        // Step 6: Update render context with mouse position for hover rendering
        _context.MouseX = _mouseX;
        _context.MouseY = _mouseY;

        // Check if anything needs rendering before doing expensive output operations
        var needsRender = _isFirstFrame || (_rootNode?.NeedsRender() ?? false);
        long renderTicks = 0;
        
        // Step 6.5-9: Only do render output if something actually changed
        if (needsRender)
        {
            // Hide cursor during rendering to prevent flicker
            if (_mouseEnabled)
            {
                _context.Write("\x1b[?25l"); // Hide cursor
            }

            // Render using Surface-based path
            {
                long renderFrameStart = Stopwatch.GetTimestamp();
                
                RenderFrameWithSurface();
                
                renderTicks = Stopwatch.GetTimestamp() - renderFrameStart;
                if (_diagnosticTimingEnabled) _diagRenderTicks = renderTicks;
            }
            
            // Clear dirty flags on all nodes (they've been rendered)
            // Nodes with async content (like TerminalNode) may override ClearDirty()
            // to keep themselves dirty if new content arrived during the render frame.
            if (_rootNode != null)
            {
                ClearDirtyFlags(_rootNode);
            }
        }
        
        // Record metrics for this frame
        var freq = (double)Stopwatch.Frequency;
        _metrics.FrameBuildDuration.Record(buildTicks * 1000.0 / freq);
        _metrics.FrameReconcileDuration.Record(reconcileTicks * 1000.0 / freq);
        if (needsRender)
        {
            _metrics.FrameRenderDuration.Record(renderTicks * 1000.0 / freq);
        }
        _metrics.FrameDuration.Record((Stopwatch.GetTimestamp() - frameStart) * 1000.0 / freq);
        _metrics.FrameCount.Add(1);
        
        // Render hardware cursor - for focused TerminalNode uses child's cursor,
        // otherwise uses mouse position if mouse cursor is enabled
        RenderCursor();
    }
    
    /// <summary>
    /// Renders the frame using Surface-based rendering with efficient diffing.
    /// </summary>
    private void RenderFrameWithSurface()
    {
        var width = _adapter.Width;
        var height = _adapter.Height;

        _surfacePool?.NextFrame();
        
        // Get cell metrics from terminal capabilities
        // Use actual (floating-point) cell width for precise sixel sizing
        var caps = _adapter.Capabilities;
        var cellMetrics = new CellMetrics(caps.EffectiveCellPixelWidth, caps.CellPixelHeight);
        
        // Ensure we have surfaces of the correct size and cell metrics
        var needNewSurfaces = _currentSurface == null 
            || _currentSurface.Width != width 
            || _currentSurface.Height != height
            || _currentSurface.CellMetrics != cellMetrics;
            
        if (needNewSurfaces)
        {
            // Reset attributes and clear screen on resize to remove artifacts from old layout
            _adapter.Write("\x1b[0m\x1b[2J");
            
            _currentSurface = new Surface(width, height, cellMetrics);
            _previousSurface = new Surface(width, height, cellMetrics);
            _isFirstFrame = true;
        }
        
        // Swap buffers (reuse previous surface as current for double-buffering)
        // After the needNewSurfaces block, both surfaces are guaranteed non-null
        (_previousSurface, _currentSurface) = (_currentSurface!, _previousSurface!);
        _currentSurface.Clear();
        
        // Create surface-backed render context and render
        var surfaceContext = new SurfaceRenderContext(_currentSurface, _context.Theme)
        {
            MouseX = _mouseX,
            MouseY = _mouseY,
            CellMetrics = cellMetrics,
            CachingEnabled = false,  // TODO: Re-enable after fixing sixel caching issues
            Metrics = _metrics.NodeRenderDuration != null ? _metrics : null,
            SurfacePool = _surfacePool
        };
        
        if (_rootNode != null)
        {
            RenderTreeToSurface(_rootNode, surfaceContext);
        }
        
        // Diff current vs previous and emit changes
        var diffStart = Stopwatch.GetTimestamp();
        var diff = _isFirstFrame || _previousSurface == null
            ? SurfaceComparer.CompareToEmpty(_currentSurface)
            : SurfaceComparer.Compare(_previousSurface, _currentSurface);
        _metrics.SurfaceDiffDuration.Record(Stopwatch.GetElapsedTime(diffStart).TotalMilliseconds);
        
        _metrics.OutputCellsChanged.Record(diff.Count);
        
        if (!diff.IsEmpty)
        {
            // Generate tokens then serialize — captures both counts for metrics
            var tokensStart = Stopwatch.GetTimestamp();
            var tokens = SurfaceComparer.ToTokens(diff, _currentSurface);
            _metrics.SurfaceTokensDuration.Record(Stopwatch.GetElapsedTime(tokensStart).TotalMilliseconds);
            
            var serializeStart = Stopwatch.GetTimestamp();
            var ansiOutput = Tokens.AnsiTokenSerializer.Serialize(tokens);
            _metrics.SurfaceSerializeDuration.Record(Stopwatch.GetElapsedTime(serializeStart).TotalMilliseconds);
            
            _adapter.Write(ansiOutput);
            
            _metrics.OutputTokens.Record(tokens.Count);
            _metrics.OutputBytes.Record(System.Text.Encoding.UTF8.GetByteCount(ansiOutput));
        }
        
        _isFirstFrame = false;
    }
    
    /// <summary>
    /// Renders the node tree to a Surface via SurfaceRenderContext.
    /// </summary>
    private void RenderTreeToSurface(Hex1bNode node, SurfaceRenderContext context, Theming.Hex1bTheme? currentTheme = null)
    {
        // Track theme mutations from ThemePanelNode
        var effectiveTheme = currentTheme ?? context.Theme;
        if (node is Nodes.ThemePanelNode themePanelNode && themePanelNode.ThemeMutator != null)
        {
            effectiveTheme = themePanelNode.ThemeMutator(effectiveTheme.Clone());
        }
        
        // Apply theme for this node's render
        var originalTheme = context.Theme;
        context.Theme = effectiveTheme;
        
        // Set cursor position and render the node
        // Containers will render their children via context.RenderChild() which handles
        // caching and compositing. We do NOT recursively render children here to avoid
        // double-rendering (containers already handle their own children).
        context.SetCursorPosition(node.Bounds.X, node.Bounds.Y);
        
        if (_diagnosticTimingEnabled)
        {
            var renderStart = Stopwatch.GetTimestamp();
            node.Render(context);
            node.DiagRenderTicks = Stopwatch.GetTimestamp() - renderStart;
            node.DiagLastRenderedTimestamp = Stopwatch.GetTimestamp();
            
            if (_metrics.NodeRenderDuration != null)
            {
                var elapsed = Stopwatch.GetElapsedTime(renderStart);
                _metrics.NodeRenderDuration.Record(elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("node", node.GetMetricPath()));
            }
        }
        else if (_metrics.NodeRenderDuration != null)
        {
            var renderStart = Stopwatch.GetTimestamp();
            node.Render(context);
            var elapsed = Stopwatch.GetElapsedTime(renderStart);
            _metrics.NodeRenderDuration.Record(elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("node", node.GetMetricPath()));
        }
        else
        {
            node.Render(context);
        }
        
        // Restore theme
        context.Theme = originalTheme;
    }

    /// <summary>
    /// Recursively clears the dirty flag on a node and all its children.
    /// Called after rendering to prepare for the next frame.
    /// </summary>
    private static void ClearDirtyFlags(Hex1bNode node)
    {
        node.ClearDirty();
        foreach (var child in node.GetChildren())
        {
            ClearDirtyFlags(child);
        }
    }
    
    /// <summary>
    /// Recursively marks a node and all its descendants as dirty.
    /// Used when the theme changes to force a full re-render.
    /// </summary>
    private static void MarkSubtreeDirty(Hex1bNode? node)
    {
        if (node == null) return;
        
        node.MarkDirty();
        foreach (var child in node.GetChildren())
        {
            MarkSubtreeDirty(child);
        }
    }
    
    /// <summary>
    /// Renders the hardware cursor at the appropriate position.
    /// For focused TerminalNode: uses child terminal's cursor position/shape/visibility.
    /// For other nodes: uses mouse position if mouse cursor is enabled.
    /// Only emits cursor updates when position or shape has changed to reduce flicker.
    /// </summary>
    private void RenderCursor()
    {
        // Check if a TerminalNode is focused - if so, use its cursor
        var focusedNode = _focusRing.FocusedNode;
        if (focusedNode is Nodes.TerminalNode terminalNode && terminalNode.Handle != null)
        {
            var handle = terminalNode.Handle;
            
            // Translate child cursor position to screen coordinates
            var screenCursorX = terminalNode.Bounds.X + handle.CursorX;
            var screenCursorY = terminalNode.Bounds.Y + handle.CursorY;
            var shape = handle.CursorShape;
            var visible = handle.CursorVisible;
            
            // Check if anything changed (including which node is focused)
            if (screenCursorX == _lastRenderedCursorX && 
                screenCursorY == _lastRenderedCursorY && 
                shape == _lastRenderedCursorShape &&
                visible == _lastRenderedCursorVisible &&
                ReferenceEquals(focusedNode, _lastRenderedCursorNode))
            {
                return;
            }
            
            // Update tracking state
            _lastRenderedCursorX = screenCursorX;
            _lastRenderedCursorY = screenCursorY;
            _lastRenderedCursorShape = shape;
            _lastRenderedCursorVisible = visible;
            _lastRenderedCursorNode = focusedNode;
            
            if (visible && 
                screenCursorX >= 0 && screenCursorX < _context.Width &&
                screenCursorY >= 0 && screenCursorY < _context.Height)
            {
                _context.SetCursorPosition(screenCursorX, screenCursorY);
                _context.Write("\x1b[?25h"); // Show cursor
                WriteCursorShape(shape);
            }
            else
            {
                _context.Write("\x1b[?25l"); // Hide cursor
            }
            return;
        }
        
        // Fall back to mouse cursor behavior for non-terminal nodes
        if (!_mouseEnabled || _mouseX < 0 || _mouseY < 0) return;
        if (_mouseX >= _context.Width || _mouseY >= _context.Height) return;
        
        var showCursor = _context.Theme.Get(MouseTheme.ShowCursor);
        if (!showCursor) return;
        
        // Determine the cursor shape based on the node at cursor position
        var nodeAtCursor = FindNodeAt(_rootNode, _mouseX, _mouseY);
        var mouseShape = nodeAtCursor?.PreferredCursorShape ?? CursorShape.Default;
        
        // Check if anything changed - if not, skip the update to reduce flicker
        // Also check that we're not switching from terminal cursor to mouse cursor
        if (_mouseX == _lastRenderedCursorX && 
            _mouseY == _lastRenderedCursorY && 
            mouseShape == _lastRenderedCursorShape &&
            _lastRenderedCursorVisible &&
            _lastRenderedCursorNode == null)
        {
            return;
        }
        
        // Update tracking state
        _lastRenderedCursorX = _mouseX;
        _lastRenderedCursorY = _mouseY;
        _lastRenderedCursorShape = mouseShape;
        _lastRenderedCursorVisible = true;
        _lastRenderedCursorNode = null;
        
        // Use the terminal's native cursor - just position it and show it
        _context.SetCursorPosition(_mouseX, _mouseY);
        _context.Write("\x1b[?25h"); // Show cursor
        WriteCursorShape(mouseShape);
    }
    
    /// <summary>
    /// Writes the escape sequence to set the cursor shape.
    /// </summary>
    private void WriteCursorShape(CursorShape shape)
    {
        var cursorEscape = shape switch
        {
            CursorShape.BlinkingBlock => "\x1b[1 q",
            CursorShape.SteadyBlock => "\x1b[2 q",
            CursorShape.BlinkingUnderline => "\x1b[3 q",
            CursorShape.SteadyUnderline => "\x1b[4 q",
            CursorShape.BlinkingBar => "\x1b[5 q",
            CursorShape.SteadyBar => "\x1b[6 q",
            _ => "\x1b[0 q" // Default
        };
        _context.Write(cursorEscape);
    }
    
    /// <summary>
    /// Finds the deepest (topmost) node at the given position by traversing the tree.
    /// </summary>
    private static Hex1bNode? FindNodeAt(Hex1bNode? node, int x, int y)
    {
        if (node == null) return null;
        
        // Check if point is within this node's bounds
        if (!node.Bounds.Contains(x, y)) return null;
        
        // Check children in reverse order (last = topmost)
        var children = node.GetChildren().ToList();
        for (int i = children.Count - 1; i >= 0; i--)
        {
            var hit = FindNodeAt(children[i], x, y);
            if (hit != null) return hit;
        }
        
        // No child contains the point, return this node
        return node;
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
        
        // Always focus the clicked node through the focus ring
        // This ensures proper state sync (clears old focus, sets new focus)
        _focusRing.Focus(hitNode);
        
        // Calculate local coordinates for this node
        var localX = mouseEvent.X - hitNode.Bounds.X;
        var localY = mouseEvent.Y - hitNode.Bounds.Y;
        
        // Create action context for mouse bindings (includes mouse coordinates)
        var actionContext = new InputBindingActionContext(_focusRing, RequestStop, cancellationToken, mouseEvent.X, mouseEvent.Y, CopyToClipboard, Invalidate, _windowManagerRegistry);
        
        // Check if the node has a drag binding for this event (checked first)
        var builder = hitNode.BuildBindings();
        foreach (var dragBinding in builder.DragBindings)
        {
            if (dragBinding.Matches(eventWithClickCount))
            {
                // Start the drag - capture mouse until release
                var handler = dragBinding.StartDrag(localX, localY);
                
                // If the handler is empty, the drag was rejected (click wasn't in drag area)
                // Continue to check mouse bindings instead
                if (handler.IsEmpty)
                {
                    continue;
                }
                
                _activeDragHandler = handler;
                _activeDragNode = hitNode;
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
    /// Reconciles a node with a widget asynchronously, creating/updating/replacing as needed.
    /// This is the core of the "diffing" algorithm.
    /// </summary>
    private async Task<Hex1bNode?> ReconcileAsync(Hex1bNode? existingNode, Hex1bWidget? widget, CancellationToken cancellationToken)
    {
        if (widget is null)
        {
            return null;
        }

        // Create the root reconcile context with timer scheduling callback
        var context = ReconcileContext.CreateRoot(_focusRing, cancellationToken, Invalidate,
            CaptureInput, ReleaseCapture, ScheduleTimer, _windowManagerRegistry);
        context.IsNew = existingNode is null || existingNode.GetType() != widget.GetExpectedNodeType();
        context.DiagnosticTimingEnabled = _diagnosticTimingEnabled;
        context.Metrics = _metrics.NodeReconcileDuration != null ? _metrics : null;
        
        // Delegate to the widget's own ReconcileAsync method
        long reconcileStart = 0;
        var recordReconcileMetric = _metrics.NodeReconcileDuration != null;
        if (_diagnosticTimingEnabled || recordReconcileMetric) reconcileStart = Stopwatch.GetTimestamp();
        
        var node = await widget.ReconcileAsync(existingNode, context);
        
        long reconcileElapsed = 0;
        if (_diagnosticTimingEnabled || recordReconcileMetric)
            reconcileElapsed = Stopwatch.GetTimestamp() - reconcileStart;
        if (_diagnosticTimingEnabled) node.DiagReconcileTicks = reconcileElapsed;

        // Set common properties on the reconciled node
        node.Parent = null; // Root has no parent
        
        // Set metric name from widget (for per-node metrics)
        var newMetricName = widget.MetricName;
        if (node.MetricName != newMetricName)
        {
            node.MetricName = newMetricName;
            node.InvalidateMetricPath();
        }
        node.Metrics = _metrics.NodeMeasureDuration != null ? _metrics : null;
        
        // Record per-node reconcile duration (after MetricName/parent are set)
        if (recordReconcileMetric)
        {
            var elapsedMs = (double)reconcileElapsed / Stopwatch.Frequency * 1000.0;
            _metrics.NodeReconcileDuration!.Record(elapsedMs, new KeyValuePair<string, object?>("node", node.GetMetricPath()));
        }
        
        // Mark new nodes as dirty (they need to be rendered for the first time)
        if (context.IsNew)
        {
            node.MarkDirty();
        }
        
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
        
        // Schedule animation timer if widget has RedrawDelay (for root widget)
        var effectiveDelay = widget.GetEffectiveRedrawDelay();
        if (effectiveDelay.HasValue)
        {
            var capturedNode = node;
            ScheduleTimer(effectiveDelay.Value, () =>
            {
                capturedNode.MarkDirty();
                Invalidate();
            });
        }

        return node;
    }
    
    private void ScheduleTimer(TimeSpan delay, Action callback)
    {
        _animationTimer.Schedule(delay, callback);
    }

    public void Dispose()
    {
        // Complete the invalidate channel
        _invalidateChannel.Writer.TryComplete();
        
        // Dispose the owned terminal if we created it
        // The terminal handles writing mouse disable sequences directly to the console
        _ownedTerminal?.Dispose();
        
        // Dispose the adapter
        if (_adapter is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        // Complete the invalidate channel
        _invalidateChannel.Writer.TryComplete();
        
        // Dispose the owned terminal if we created it
        // The terminal handles writing mouse disable sequences directly to the console
        if (_ownedTerminal != null)
        {
            await _ownedTerminal.DisposeAsync();
        }
        
        // Dispose the adapter asynchronously
        await _adapter.DisposeAsync();
    }
    
    // ========================================
    // IDiagnosticTreeProvider implementation
    // ========================================
    
    DiagnosticNode? IDiagnosticTreeProvider.GetDiagnosticTree()
    {
        return _rootNode != null ? DiagnosticNode.FromNode(_rootNode) : null;
    }
    
    IReadOnlyList<DiagnosticPopupEntry> IDiagnosticTreeProvider.GetDiagnosticPopups()
    {
        var popups = new List<DiagnosticPopupEntry>();
        
        // Find ZStackNode (the popup host) in the tree
        var zstack = FindNode<ZStackNode>(_rootNode);
        if (zstack == null) return popups;
        
        var entries = zstack.Popups.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var diagEntry = new DiagnosticPopupEntry
            {
                Index = i,
                ContentType = entry.ContentNode?.GetType().Name ?? "null",
                HasBackdrop = true, // All popups have backdrops
                FocusRestoreNodeType = entry.FocusRestoreNode?.GetType().Name,
                IsAnchored = entry.AnchorNode != null,
                IsBarrier = entry.IsBarrier
            };
            
            if (entry.ContentNode != null)
            {
                diagEntry.ContentBounds = DiagnosticRect.FromRect(entry.ContentNode.ContentBounds);
            }
            
            // Check if it's an anchored popup
            if (entry.ContentNode is AnchoredNode anchoredNode)
            {
                diagEntry.AnchorInfo = new DiagnosticAnchorInfo
                {
                    AnchorNodeType = anchoredNode.AnchorNode?.GetType().Name,
                    AnchorBounds = anchoredNode.AnchorNode != null 
                        ? DiagnosticRect.FromRect(anchoredNode.AnchorNode.Bounds) 
                        : null,
                    IsStale = anchoredNode.IsAnchorStale,
                    Position = anchoredNode.Position.ToString()
                };
            }
            
            popups.Add(diagEntry);
        }
        
        return popups;
    }
    
    DiagnosticFocusInfo IDiagnosticTreeProvider.GetDiagnosticFocusInfo()
    {
        var focusables = _focusRing.Focusables;
        var currentIndex = -1;
        var focusedType = (string?)null;
        
        for (int i = 0; i < focusables.Count; i++)
        {
            if (focusables[i].IsFocused)
            {
                currentIndex = i;
                focusedType = focusables[i].GetType().Name;
                break;
            }
        }
        
        return new DiagnosticFocusInfo
        {
            FocusableCount = focusables.Count,
            CurrentFocusIndex = currentIndex,
            FocusedNodeType = focusedType,
            LastHitTestDebug = _focusRing.LastHitTestDebug,
            Focusables = focusables.Select((node, i) => new DiagnosticFocusableEntry
            {
                Index = i,
                Type = node.GetType().Name,
                Bounds = DiagnosticRect.FromRect(node.Bounds),
                HitTestBounds = DiagnosticRect.FromRect(node.HitTestBounds),
                IsFocused = node.IsFocused
            }).ToList()
        };
    }
    
    DiagnosticFrameInfo IDiagnosticTreeProvider.GetDiagnosticFrameInfo()
    {
        var freq = (double)Stopwatch.Frequency;
        return new DiagnosticFrameInfo
        {
            BuildMs = _diagBuildTicks * 1000.0 / freq,
            ReconcileMs = _diagReconcileTicks * 1000.0 / freq,
            RenderMs = _diagRenderTicks * 1000.0 / freq,
            TimingEnabled = _diagnosticTimingEnabled
        };
    }
    
    /// <summary>
    /// Finds the first node of type T in the tree.
    /// </summary>
    private static T? FindNode<T>(Hex1bNode? root) where T : Hex1bNode
    {
        if (root == null) return null;
        if (root is T found) return found;
        
        foreach (var child in root.GetChildren())
        {
            var result = FindNode<T>(child);
            if (result != null) return result;
        }
        
        return null;
    }
}
