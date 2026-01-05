#pragma warning disable HEX1B_SIXEL // Sixel API is experimental - internal usage is allowed

using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal;
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
public class Hex1bApp : IDisposable, IAsyncDisposable
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
    
    // Default CTRL-C binding option
    private readonly bool _enableDefaultCtrlCExit;
    
    // Input coalescing options
    private readonly bool _enableInputCoalescing;
    private readonly int _inputCoalescingInitialDelayMs;
    private readonly int _inputCoalescingMaxDelayMs;

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
            _ownedTerminal = new Hex1bTerminal(presentation, workload);
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
        _context.EnterAlternateScreen();
        try
        {
            // Initial render
            await RenderFrameAsync(cancellationToken);

            // React to input events and invalidation signals
            while (!cancellationToken.IsCancellationRequested && !_stopRequested)
            {
                // Wait for either an input event or an invalidation signal
                var inputTask = _adapter.InputEvents.ReadAsync(cancellationToken).AsTask();
                var invalidateTask = _invalidateChannel.Reader.ReadAsync(cancellationToken).AsTask();
                
                var completedTask = await Task.WhenAny(inputTask, invalidateTask);
                
                if (completedTask == inputTask)
                {
                    // Process the first input event
                    var inputEvent = await inputTask;
                    await ProcessInputEventAsync(inputEvent, cancellationToken);
                    
                    // Input coalescing: batch rapid inputs together before rendering
                    // This prevents back pressure from rapid input (key repeats, mouse moves)
                    if (_enableInputCoalescing)
                    {
                        // Adaptive delay: scales based on output queue depth
                        var outputBacklog = _adapter.OutputQueueDepth;
                        var coalescingDelayMs = Math.Min(
                            _inputCoalescingInitialDelayMs + (outputBacklog * 10), 
                            _inputCoalescingMaxDelayMs);
                        await Task.Delay(coalescingDelayMs, cancellationToken);
                    
                        // Drain any pending input that arrived during the delay
                        while (_adapter.InputEvents.TryRead(out var pendingEvent))
                        {
                            await ProcessInputEventAsync(pendingEvent, cancellationToken);
                            
                            // Check for stop request between events
                            if (_stopRequested || cancellationToken.IsCancellationRequested)
                                break;
                        }
                    }
                }
                // If invalidateTask completed, we just need to re-render (no input to handle)

                // Re-render after handling ALL input or invalidation (state may have changed)
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

    /// <summary>
    /// Processes a single input event (key, mouse, resize, etc.).
    /// </summary>
    private async Task ProcessInputEventAsync(Hex1bEvent inputEvent, CancellationToken cancellationToken)
    {
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
                await InputRouter.RouteInputAsync(_rootNode, keyEvent, _focusRing, _inputRouterState, RequestStop, cancellationToken, CopyToClipboard);
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

    private async Task RenderFrameAsync(CancellationToken cancellationToken)
    {
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
        
        try
        {
            widgetTree = await _rootComponent(_rootContext);
        }
        catch (Exception ex) when (_rescueEnabled)
        {
            // Build phase failed - capture exception for RescueWidget to handle
            buildException = ex;
        }

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
        _rootNode = await ReconcileAsync(_rootNode, widgetTree, cancellationToken);

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

        // Step 6.5: Hide cursor during rendering to prevent flicker
        if (_mouseEnabled)
        {
            _context.Write("\x1b[?25l"); // Hide cursor
        }

        // Step 6.6: Begin frame buffering - all changes from here until EndFrame
        // will be accumulated in the Hex1bAppRenderOptimizationFilter and emitted as net changes
        _context.BeginFrame();

        // Step 7: Clear dirty regions (instead of global clear to reduce flicker)
        // On first frame or when root is new, do a full clear
        if (_isFirstFrame)
        {
            _context.Clear();
            _isFirstFrame = false;
        }
        else if (_rootNode != null)
        {
            ClearDirtyRegions(_rootNode);
        }
        
        // Step 8: Render the node tree to the terminal (only dirty nodes)
        if (_rootNode != null)
        {
            RenderTree(_rootNode);
        }
        
        // Step 9: End frame buffering - Hex1bAppRenderOptimizationFilter will now emit only
        // the net changes (e.g., clear + re-render same content = no output)
        _context.EndFrame();
        
        // Step 9.5: Ensure cursor is hidden after rendering to prevent it showing at last write position
        _context.Write("\x1b[?25l");
        
        // Step 9.6: Render mouse cursor overlay if enabled (after hiding default cursor)
        // This positions and shows the cursor at the mouse location
        RenderMouseCursor();
        
        // Step 10: Clear dirty flags on all nodes (they've been rendered)
        if (_rootNode != null)
        {
            ClearDirtyFlags(_rootNode);
        }
    }
    
    /// <summary>
    /// Renders the node tree, skipping clean subtrees that don't need re-rendering.
    /// </summary>
    /// <remarks>
    /// This method performs a smart traversal:
    /// - If a node is dirty, render it (which includes its children)
    /// - If a node is clean but has dirty descendants, traverse children
    /// - If a subtree is entirely clean, skip it
    /// Theme mutations from ThemePanelNodes are tracked and applied during traversal.
    /// </remarks>
    private void RenderTree(Hex1bNode node, Theming.Hex1bTheme? currentTheme = null)
    {
        // If this subtree has no dirty nodes, skip it entirely
        if (!node.NeedsRender())
        {
            return;
        }
        
        // Track theme mutations from ThemePanelNode
        var effectiveTheme = currentTheme ?? _context.Theme;
        if (node is Nodes.ThemePanelNode themePanelNode && themePanelNode.ThemeMutator != null)
        {
            // Clone the theme before passing to mutator to prevent mutation of parent themes
            effectiveTheme = themePanelNode.ThemeMutator(effectiveTheme.Clone());
        }
        
        // If this specific node is dirty, render it (and its children)
        if (node.IsDirty)
        {
            // Apply the effective theme before rendering
            var originalTheme = _context.Theme;
            _context.Theme = effectiveTheme;
            
            _context.SetCursorPosition(node.Bounds.X, node.Bounds.Y);
            node.Render(_context);
            
            // Restore original theme
            _context.Theme = originalTheme;
            return;
        }
        
        // Node is clean but has dirty descendants - traverse children with the current theme
        // Check if this node provides custom layout/clipping for its children
        var childLayoutProvider = node as Nodes.IChildLayoutProvider;
        
        foreach (var child in node.GetChildren())
        {
            if (!child.NeedsRender()) continue;
            
            // Get layout provider for this child (if any)
            var layoutForChild = childLayoutProvider?.GetChildLayoutProvider(child);
            
            if (layoutForChild != null)
            {
                // Set up clipping context for this child
                var previousLayout = _context.CurrentLayoutProvider;
                layoutForChild.ParentLayoutProvider = previousLayout;
                _context.CurrentLayoutProvider = layoutForChild;
                
                RenderTree(child, effectiveTheme);
                
                _context.CurrentLayoutProvider = previousLayout;
            }
            else
            {
                RenderTree(child, effectiveTheme);
            }
        }
    }
    
    /// <summary>
    /// Recursively clears dirty regions in the node tree.
    /// For each dirty node, clears the union of its previous and current bounds,
    /// intersected with any active clip rect from ancestor layout providers.
    /// Tracks theme from ThemePanelNodes to ensure proper clearing with the correct background.
    /// </summary>
    private void ClearDirtyRegions(Hex1bNode node, Rect? clipRect = null, Rect? expandedClipRect = null)
    {
        // Calculate this node's effective clip rect first
        var effectiveClipRect = clipRect;
        var effectiveExpandedClipRect = expandedClipRect;
        
        if (node is Nodes.ILayoutProvider layoutProvider && layoutProvider.ClipMode == Widgets.ClipMode.Clip)
        {
            // For normal rendering, use current bounds as clip
            effectiveClipRect = effectiveClipRect.HasValue 
                ? Intersect(effectiveClipRect.Value, layoutProvider.ClipRect)
                : layoutProvider.ClipRect;
            
            // For orphan clearing, use union of current and previous bounds
            // This allows clearing areas that were visible before the node shrunk
            var expandedNodeClip = Union(node.Bounds, node.PreviousBounds);
            effectiveExpandedClipRect = effectiveExpandedClipRect.HasValue 
                ? Intersect(effectiveExpandedClipRect.Value, expandedNodeClip)
                : expandedNodeClip;
        }
        
        // Track theme from ThemePanelNode
        var previousTheme = _context.Theme;
        if (node is Nodes.ThemePanelNode themePanelNode && themePanelNode.ThemeMutator != null)
        {
            // Clone the theme before passing to mutator to prevent mutation of parent themes
            _context.Theme = themePanelNode.ThemeMutator(previousTheme.Clone());
        }
        
        if (node.IsDirty)
        {
            // Clear the previous bounds (where the node was)
            // Use expanded clip rect if content shrunk (PreviousBounds larger than Bounds)
            // This ensures areas outside current bounds but inside previous bounds get cleared
            if (node.PreviousBounds.Width > 0 && node.PreviousBounds.Height > 0)
            {
                // Check if content shrunk in either dimension
                var shrunk = node.PreviousBounds.Width > node.Bounds.Width ||
                             node.PreviousBounds.Height > node.Bounds.Height ||
                             node.PreviousBounds.X < node.Bounds.X ||
                             node.PreviousBounds.Y < node.Bounds.Y ||
                             node.PreviousBounds.X + node.PreviousBounds.Width > node.Bounds.X + node.Bounds.Width ||
                             node.PreviousBounds.Y + node.PreviousBounds.Height > node.Bounds.Y + node.Bounds.Height;
                
                var clipToUse = shrunk && effectiveExpandedClipRect.HasValue 
                    ? effectiveExpandedClipRect.Value 
                    : effectiveClipRect;
                    
                var regionToClear = clipToUse.HasValue 
                    ? Intersect(node.PreviousBounds, clipToUse.Value)
                    : node.PreviousBounds;
                if (regionToClear.Width > 0 && regionToClear.Height > 0)
                {
                    _context.ClearRegion(regionToClear);
                }
            }
            
            // Clear the current bounds (where the node will be), clipped to effective clip rect
            // This handles the case where content shrinks or moves
            if (node.Bounds != node.PreviousBounds)
            {
                var regionToClear = effectiveClipRect.HasValue 
                    ? Intersect(node.Bounds, effectiveClipRect.Value)
                    : node.Bounds;
                if (regionToClear.Width > 0 && regionToClear.Height > 0)
                {
                    _context.ClearRegion(regionToClear);
                }
            }
            
            // Clear orphaned child bounds (children that were removed during reconciliation)
            // Use the EXPANDED clip rect which includes both current and previous bounds
            // of all ancestor nodes - this allows clearing areas that were visible before
            // any ancestor container shrunk
            if (node.OrphanedChildBounds != null)
            {
                foreach (var orphanedBounds in node.OrphanedChildBounds)
                {
                    var regionToClear = effectiveExpandedClipRect.HasValue 
                        ? Intersect(orphanedBounds, effectiveExpandedClipRect.Value)
                        : orphanedBounds;
                    if (regionToClear.Width > 0 && regionToClear.Height > 0)
                    {
                        _context.ClearRegion(regionToClear);
                    }
                }
                node.ClearOrphanedChildBounds();
            }
        }
        
        // Recurse into children with the effective clip rects
        foreach (var child in node.GetChildren())
        {
            ClearDirtyRegions(child, effectiveClipRect, effectiveExpandedClipRect);
        }
        
        // Restore previous theme after processing children
        _context.Theme = previousTheme;
    }
    
    /// <summary>
    /// Computes the intersection of two rectangles.
    /// Returns a zero-sized rect if they don't intersect.
    /// </summary>
    private static Rect Intersect(Rect a, Rect b)
    {
        var x = Math.Max(a.X, b.X);
        var y = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        
        var width = Math.Max(0, right - x);
        var height = Math.Max(0, bottom - y);
        
        return new Rect(x, y, width, height);
    }
    
    /// <summary>
    /// Computes the union (bounding box) of two rectangles.
    /// If either rect is empty, returns the other.
    /// </summary>
    private static Rect Union(Rect a, Rect b)
    {
        // Handle empty rects
        if (a.Width <= 0 || a.Height <= 0) return b;
        if (b.Width <= 0 || b.Height <= 0) return a;
        
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Max(a.Y + a.Height, b.Y + b.Height);
        
        return new Rect(x, y, right - x, bottom - y);
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
    /// Renders the mouse cursor overlay at the current mouse position.
    /// </summary>
    private void RenderMouseCursor()
    {
        if (!_mouseEnabled || _mouseX < 0 || _mouseY < 0) return;
        if (_mouseX >= _context.Width || _mouseY >= _context.Height) return;
        
        var showCursor = _context.Theme.Get(MouseTheme.ShowCursor);
        if (!showCursor) return;
        
        // Use the terminal's native cursor - just position it and show it
        // This avoids overwriting cell content and eliminates flicker
        _context.SetCursorPosition(_mouseX, _mouseY);
        _context.Write("\x1b[?25h"); // Show cursor
        
        // Set cursor shape based on the node's preferred cursor
        var nodeAtCursor = FindNodeAt(_rootNode, _mouseX, _mouseY);
        var shape = nodeAtCursor?.PreferredCursorShape ?? CursorShape.Default;
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
        
        // Focus the clicked node (if not already focused)
        if (!hitNode.IsFocused)
        {
            _focusRing.Focus(hitNode);
        }
        
        // Calculate local coordinates for this node
        var localX = mouseEvent.X - hitNode.Bounds.X;
        var localY = mouseEvent.Y - hitNode.Bounds.Y;
        
        // Create action context for mouse bindings (includes mouse coordinates)
        var actionContext = new InputBindingActionContext(_focusRing, RequestStop, cancellationToken, mouseEvent.X, mouseEvent.Y, CopyToClipboard);
        
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
    /// Reconciles a node with a widget asynchronously, creating/updating/replacing as needed.
    /// This is the core of the "diffing" algorithm.
    /// </summary>
    private async Task<Hex1bNode?> ReconcileAsync(Hex1bNode? existingNode, Hex1bWidget? widget, CancellationToken cancellationToken)
    {
        if (widget is null)
        {
            return null;
        }

        // Create the root reconcile context
        var context = ReconcileContext.CreateRoot(_focusRing, cancellationToken);
        context.IsNew = existingNode is null || existingNode.GetType() != widget.GetExpectedNodeType();
        
        // Delegate to the widget's own ReconcileAsync method
        var node = await widget.ReconcileAsync(existingNode, context);

        // Set common properties on the reconciled node
        node.Parent = null; // Root has no parent
        
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

        return node;
    }

    public void Dispose()
    {
        // Complete the invalidate channel
        _invalidateChannel.Writer.TryComplete();
        
        // Dispose the owned terminal if we created it
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
        if (_ownedTerminal != null)
        {
            await _ownedTerminal.DisposeAsync();
        }
        
        // Dispose the adapter asynchronously
        await _adapter.DisposeAsync();
    }
}
