#pragma warning disable HEX1B001 // Navigator API is experimental - internal usage is allowed

using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Context passed to widget reconciliation methods, providing access to
/// child reconciliation and focus management utilities.
/// </summary>
public sealed class ReconcileContext
{
    /// <summary>
    /// The parent node in the tree (used for focus management decisions).
    /// </summary>
    public Hex1bNode? Parent { get; }
    
    /// <summary>
    /// The full chain of ancestor nodes, from immediate parent to root.
    /// This is needed because during reconciliation, node.Parent links may not
    /// be set yet on intermediate nodes.
    /// </summary>
    private readonly IReadOnlyList<Hex1bNode> _ancestors;

    internal FocusRing FocusRing { get; }
    
    /// <summary>
    /// Callback to invalidate the app and trigger a re-render.
    /// Nodes that receive asynchronous updates (like TerminalNode) use this to
    /// trigger re-renders when new content arrives.
    /// </summary>
    internal Action? InvalidateCallback { get; }
    
    /// <summary>
    /// Callback to capture all input to a node.
    /// Nodes call this when they need to receive all keyboard and mouse input,
    /// bypassing normal binding processing.
    /// </summary>
    internal Action<Hex1bNode>? CaptureInputCallback { get; }
    
    /// <summary>
    /// Callback to release input capture.
    /// Nodes call this when they no longer need to capture all input.
    /// </summary>
    internal Action? ReleaseCaptureCallback { get; }

    /// <summary>
    /// Whether this is a new node being created (vs updating an existing one).
    /// </summary>
    public bool IsNew { get; internal set; }
    
    /// <summary>
    /// When true, per-node diagnostic timing is collected during reconciliation.
    /// </summary>
    internal bool DiagnosticTimingEnabled { get; set; }

    /// <summary>
    /// Metrics instance to propagate to nodes for per-node metric recording.
    /// Only set (non-null) when per-node metrics are enabled.
    /// </summary>
    internal Diagnostics.Hex1bMetrics? Metrics { get; set; }
    
    /// <summary>
    /// The layout axis of the parent container (if any).
    /// Used by SeparatorWidget to determine orientation.
    /// </summary>
    public LayoutAxis? LayoutAxis { get; private set; }
    
    /// <summary>
    /// The index of this child within the parent container (if known).
    /// Used by DrawerWidget for direction auto-detection.
    /// </summary>
    public int? ChildIndex { get; private set; }
    
    /// <summary>
    /// The total number of children in the parent container (if known).
    /// Used by DrawerWidget for direction auto-detection.
    /// </summary>
    public int? ChildCount { get; private set; }
    
    /// <summary>
    /// The cancellation token for the current render frame.
    /// Composite widgets that perform async work should observe this token.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Callback to schedule an animation timer for a widget.
    /// The action will be called after the specified delay, marking the node dirty
    /// and triggering a re-render.
    /// </summary>
    internal Action<TimeSpan, Action>? ScheduleTimerCallback { get; }

    private ReconcileContext(
        Hex1bNode? parent, 
        FocusRing focusRing, 
        CancellationToken cancellationToken, 
        IReadOnlyList<Hex1bNode>? ancestors = null, 
        LayoutAxis? layoutAxis = null, 
        Action? invalidateCallback = null,
        Action<Hex1bNode>? captureInputCallback = null,
        Action? releaseCaptureCallback = null,
        Action<TimeSpan, Action>? scheduleTimerCallback = null,
        WindowManagerRegistry? windowManagerRegistry = null)
    {
        Parent = parent;
        _ancestors = ancestors ?? Array.Empty<Hex1bNode>();
        LayoutAxis = layoutAxis;
        FocusRing = focusRing;
        CancellationToken = cancellationToken;
        InvalidateCallback = invalidateCallback;
        CaptureInputCallback = captureInputCallback;
        ReleaseCaptureCallback = releaseCaptureCallback;
        ScheduleTimerCallback = scheduleTimerCallback;
        WindowManagerRegistry = windowManagerRegistry;
    }

    /// <summary>
    /// The window manager registry for registering WindowPanels.
    /// </summary>
    internal WindowManagerRegistry? WindowManagerRegistry { get; }

    /// <summary>
    /// Creates a root reconcile context (no parent).
    /// </summary>
    internal static ReconcileContext CreateRoot(
        FocusRing? focusRing = null, 
        CancellationToken cancellationToken = default, 
        Action? invalidateCallback = null,
        Action<Hex1bNode>? captureInputCallback = null,
        Action? releaseCaptureCallback = null,
        Action<TimeSpan, Action>? scheduleTimerCallback = null,
        WindowManagerRegistry? windowManagerRegistry = null) 
        => new(null, focusRing ?? new FocusRing(), cancellationToken, invalidateCallback: invalidateCallback, 
            captureInputCallback: captureInputCallback, releaseCaptureCallback: releaseCaptureCallback,
            scheduleTimerCallback: scheduleTimerCallback, windowManagerRegistry: windowManagerRegistry);

    /// <summary>
    /// Creates a child context with the specified parent.
    /// The new context includes the full ancestor chain.
    /// </summary>
    internal ReconcileContext WithParent(Hex1bNode parent)
    {
        // Build the new ancestor list: [parent, ...current ancestors]
        var newAncestors = new List<Hex1bNode>(_ancestors.Count + 1) { parent };
        newAncestors.AddRange(_ancestors);
        return new ReconcileContext(parent, FocusRing, CancellationToken, newAncestors, LayoutAxis, InvalidateCallback, 
            CaptureInputCallback, ReleaseCaptureCallback, ScheduleTimerCallback, WindowManagerRegistry)
        {
            DiagnosticTimingEnabled = DiagnosticTimingEnabled,
            Metrics = Metrics
        };
    }
    
    /// <summary>
    /// Creates a new context with the specified layout axis.
    /// Used by VStack and HStack to inform children of the layout direction.
    /// </summary>
    public ReconcileContext WithLayoutAxis(LayoutAxis axis)
    {
        return new ReconcileContext(Parent, FocusRing, CancellationToken, _ancestors.ToList(), axis, InvalidateCallback,
            CaptureInputCallback, ReleaseCaptureCallback, ScheduleTimerCallback, WindowManagerRegistry) { IsNew = IsNew, DiagnosticTimingEnabled = DiagnosticTimingEnabled, Metrics = Metrics };
    }
    
    /// <summary>
    /// Creates a new context with child position info for direction auto-detection.
    /// Used by HStack and VStack when reconciling children.
    /// </summary>
    public ReconcileContext WithChildPosition(int index, int count)
    {
        return new ReconcileContext(Parent, FocusRing, CancellationToken, _ancestors.ToList(), LayoutAxis, InvalidateCallback,
            CaptureInputCallback, ReleaseCaptureCallback, ScheduleTimerCallback, WindowManagerRegistry) { IsNew = IsNew, ChildIndex = index, ChildCount = count, DiagnosticTimingEnabled = DiagnosticTimingEnabled, Metrics = Metrics };
    }

    /// <summary>
    /// Reconciles a child widget asynchronously, returning the updated or new node.
    /// </summary>
    public async Task<Hex1bNode?> ReconcileChildAsync(Hex1bNode? existingNode, Hex1bWidget? widget, Hex1bNode parent)
    {
        if (widget is null)
        {
            return null;
        }

        var childContext = WithParent(parent);
        var isReplacement = existingNode is not null && existingNode.GetType() != widget.GetExpectedNodeType();
        childContext.IsNew = existingNode is null || isReplacement;
        
        long reconcileStart = 0;
        var recordReconcileMetric = Metrics?.NodeReconcileDuration != null;
        if (DiagnosticTimingEnabled || recordReconcileMetric) reconcileStart = System.Diagnostics.Stopwatch.GetTimestamp();
        
        var node = await widget.ReconcileAsync(existingNode, childContext);
        
        long reconcileElapsed = 0;
        if (DiagnosticTimingEnabled || recordReconcileMetric)
            reconcileElapsed = System.Diagnostics.Stopwatch.GetTimestamp() - reconcileStart;
        if (DiagnosticTimingEnabled) node.DiagReconcileTicks = reconcileElapsed;

        // If this is a replacement (different node type), inherit bounds from the old node
        // so ClearDirtyRegions knows to clear the region previously occupied by the old content
        if (isReplacement)
        {
            node.InheritBoundsFromReplacedNode(existingNode!);
        }

        // Set common properties on the reconciled node
        node.Parent = parent;
        node.BindingsConfigurator = widget.BindingsConfigurator;
        node.WidthHint = widget.WidthHint;
        node.HeightHint = widget.HeightHint;
        
        // Set metric name from widget (for per-node metrics)
        var newMetricName = widget.MetricName;
        if (node.MetricName != newMetricName)
        {
            node.MetricName = newMetricName;
            node.InvalidateMetricPath();
        }
        node.MetricChildIndex = childContext.ChildIndex ?? 0;
        
        // Set metrics reference for per-node recording
        node.Metrics = Metrics;
        
        // Record per-node reconcile duration (after MetricName/parent are set so path is correct)
        if (recordReconcileMetric)
        {
            var elapsedMs = (double)reconcileElapsed / System.Diagnostics.Stopwatch.Frequency * 1000.0;
            Metrics!.NodeReconcileDuration!.Record(elapsedMs, new KeyValuePair<string, object?>("node", node.GetMetricPath()));
        }
        
        // Schedule animation timer if widget has RedrawDelay
        var effectiveDelay = widget.GetEffectiveRedrawDelay();
        if (effectiveDelay.HasValue && ScheduleTimerCallback is not null)
        {
            var capturedNode = node;
            var capturedParent = parent;
            var capturedInvalidate = InvalidateCallback;
            ScheduleTimerCallback(effectiveDelay.Value, () =>
            {
                capturedNode.MarkDirty();
                // Also mark ancestors dirty - important for composed widgets like TreeItemNode
                // where the grandparent (TreeNode) renders the child's state (e.g., spinner frame)
                capturedParent?.MarkDirty();
                capturedParent?.Parent?.MarkDirty();
                capturedInvalidate?.Invoke();
            });
        }
        
        // Mark new nodes as dirty (they need to be rendered for the first time)
        // Note: Existing nodes are marked dirty by individual widgets when their
        // properties change, or by Arrange() when bounds change.
        if (childContext.IsNew)
        {
            node.MarkDirty();
        }

        return node;
    }

    /// <summary>
    /// Finds the nearest ancestor node of the specified type by walking the ancestor chain.
    /// Returns null if no ancestor of that type exists.
    /// </summary>
    internal T? FindAncestor<T>() where T : Hex1bNode
    {
        foreach (var ancestor in _ancestors)
        {
            if (ancestor is T match)
                return match;
        }
        return null;
    }

    /// <summary>
    /// Returns true if the parent node (or any ancestor) manages focus for its children.
    /// When a parent manages focus, child containers should NOT set initial focus.
    /// </summary>
    public bool ParentManagesFocus()
    {
        // Use the ancestor chain stored in the context, not node.Parent links,
        // because node.Parent links may not be set yet during reconciliation.
        foreach (var ancestor in _ancestors)
        {
            // Check the virtual property - any container can declare it manages child focus
            if (ancestor.ManagesChildFocus)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Sets focus on a node. Uses the virtual IsFocused property on Hex1bNode.
    /// </summary>
    public static void SetNodeFocus(Hex1bNode node, bool focused)
    {
        node.IsFocused = focused;
    }

    /// <summary>
    /// Checks if a node currently has focus. Uses the virtual IsFocused property on Hex1bNode.
    /// </summary>
    public static bool IsNodeFocused(Hex1bNode node)
    {
        return node.IsFocused;
    }

    /// <summary>
    /// Recursively syncs focus indices on container nodes after focus has been set.
    /// Uses the virtual SyncFocusIndex method on Hex1bNode.
    /// </summary>
    public static void SyncContainerFocusIndices(Hex1bNode node)
    {
        // Call the virtual method - containers override this to sync their internal state
        node.SyncFocusIndex();
    }
}
