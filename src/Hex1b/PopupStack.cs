using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Represents a popup entry with optional anchor information and barrier flag.
/// </summary>
/// <remarks>
/// <para>
/// A barrier popup stops the cascade when clicking away. Non-barrier popups (like submenus)
/// propagate click-away dismissal to the nearest barrier or the bottom of the stack.
/// </para>
/// <para>
/// Use <see cref="PopupEntryExtensions.AsBarrier"/> to mark a popup as a barrier:
/// <code>
/// e.Popups.Push(() => MyDialog()).AsBarrier();
/// </code>
/// </para>
/// </remarks>
public sealed class PopupEntry
{
    internal PopupEntry(PopupStack stack, Func<Hex1bWidget> contentBuilder, Hex1bNode? anchorNode = null, AnchorPosition position = AnchorPosition.Below, Func<Hex1bTheme, Hex1bTheme>? themeMutator = null, Hex1bNode? focusRestoreNode = null, Action? onDismiss = null)
    {
        Stack = stack;
        ContentBuilder = contentBuilder;
        AnchorNode = anchorNode;
        Position = position;
        ThemeMutator = themeMutator;
        FocusRestoreNode = focusRestoreNode;
        OnDismiss = onDismiss;
    }

    internal PopupStack Stack { get; }
    internal Func<Hex1bWidget> ContentBuilder { get; }
    internal Hex1bNode? AnchorNode { get; }
    internal AnchorPosition Position { get; }
    
    /// <summary>
    /// The captured theme mutator chain from ancestor ThemePanelNodes.
    /// When set, popup content is wrapped in a ThemePanelWidget with this mutator.
    /// </summary>
    internal Func<Hex1bTheme, Hex1bTheme>? ThemeMutator { get; }
    
    /// <summary>
    /// Callback invoked when this popup is dismissed (popped from the stack).
    /// Used to clean up owner state (e.g., clear IsOpen/IsSelected on MenuNodes).
    /// </summary>
    internal Action? OnDismiss { get; }
    
    /// <summary>
    /// The node that should receive focus when this popup is dismissed.
    /// Typically the node that was focused when the popup was opened.
    /// </summary>
    internal Hex1bNode? FocusRestoreNode { get; }
    
    /// <summary>
    /// Gets whether this popup is a barrier that stops cascade dismissal.
    /// </summary>
    public bool IsBarrier { get; internal set; }
    
    /// <summary>
    /// The reconciled content node for this popup entry.
    /// Used to check if click coordinates fall within content bounds.
    /// Set by the reconciler after building popup widgets.
    /// </summary>
    internal Hex1bNode? ContentNode { get; set; }
}

/// <summary>
/// Extension methods for <see cref="PopupEntry"/> fluent configuration.
/// </summary>
public static class PopupEntryExtensions
{
    /// <summary>
    /// Marks this popup as a barrier that stops cascade dismissal.
    /// </summary>
    /// <remarks>
    /// When clicking away on a non-barrier popup, the stack unwinds to the nearest barrier.
    /// When clicking away on a barrier popup, only that single layer is dismissed.
    /// </remarks>
    /// <param name="entry">The popup entry to mark as a barrier.</param>
    /// <returns>The popup stack for method chaining.</returns>
    public static PopupStack AsBarrier(this PopupEntry entry)
    {
        entry.IsBarrier = true;
        return entry.Stack;
    }
}

/// <summary>
/// Manages a stack of popups for menu-like overlay behavior.
/// Each popup has a transparent backdrop - clicking the backdrop pops that layer.
/// </summary>
/// <remarks>
/// <para>
/// PopupStack is designed for cascading menus, dropdowns, and similar UX patterns where:
/// - Multiple popups can be stacked (e.g., File → Recent Items → filename)
/// - Each popup layer has its own transparent backdrop
/// - Clicking a backdrop dismisses that layer (and propagates to layers below)
/// </para>
/// <para>
/// By default, clicking away from any popup clears the entire stack. To create a "barrier"
/// that stops propagation (e.g., for modal dialogs), use <see cref="PopupEntryExtensions.AsBarrier"/>:
/// <code>
/// e.Popups.Push(() => MyDialog()).AsBarrier();
/// </code>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Simple popup (full-screen backdrop)
/// e.Popups.Push(() => BuildDialog());
/// 
/// // Anchored popup (positioned relative to triggering element)
/// e.Popups.PushAnchored(AnchorPosition.Below, () => BuildMenu());
/// 
/// // Barrier popup (stops cascade dismissal)
/// e.Popups.Push(() => BuildModal()).AsBarrier();
/// </code>
/// </example>
public sealed class PopupStack
{
    private readonly List<PopupEntry> _entries = [];
    
    /// <summary>
    /// Gets whether any popups are currently open.
    /// </summary>
    public bool HasPopups => _entries.Count > 0;
    
    /// <summary>
    /// Gets the number of popups currently open.
    /// </summary>
    public int Count => _entries.Count;
    
    /// <summary>
    /// Gets the popup entries for internal use by the reconciler.
    /// </summary>
    internal IReadOnlyList<PopupEntry> Entries => _entries;

    /// <summary>
    /// Pushes a new popup onto the stack (full-screen backdrop, not anchored).
    /// </summary>
    /// <param name="contentBuilder">A function that builds the widget content for the popup.</param>
    /// <returns>The popup entry for optional fluent configuration (e.g., <c>.AsBarrier()</c>).</returns>
    public PopupEntry Push(Func<Hex1bWidget> contentBuilder)
    {
        var entry = new PopupEntry(this, contentBuilder);
        _entries.Add(entry);
        return entry;
    }
    
    /// <summary>
    /// Pushes a new popup onto the stack with static content (full-screen backdrop, not anchored).
    /// </summary>
    /// <param name="content">The widget content for the popup.</param>
    /// <returns>The popup entry for optional fluent configuration (e.g., <c>.AsBarrier()</c>).</returns>
    public PopupEntry Push(Hex1bWidget content)
    {
        return Push(() => content);
    }
    
    /// <summary>
    /// Pushes an anchored popup positioned relative to a specific node.
    /// Automatically captures theme context from ancestor ThemePanelNodes.
    /// </summary>
    /// <param name="anchorNode">The node to anchor the popup to.</param>
    /// <param name="position">Where to position the popup relative to the anchor.</param>
    /// <param name="contentBuilder">A function that builds the widget content for the popup.</param>
    /// <param name="focusRestoreNode">Optional node to restore focus to when this popup is dismissed.</param>
    /// <param name="onDismiss">Optional callback invoked when this popup is dismissed to clean up owner state.</param>
    /// <returns>The popup entry for optional fluent configuration (e.g., <c>.AsBarrier()</c>).</returns>
    public PopupEntry PushAnchored(Hex1bNode anchorNode, AnchorPosition position, Func<Hex1bWidget> contentBuilder, Hex1bNode? focusRestoreNode = null, Action? onDismiss = null)
    {
        // Capture theme context from ancestor ThemePanelNodes
        var themeMutator = CaptureThemeMutator(anchorNode);
        var entry = new PopupEntry(this, contentBuilder, anchorNode, position, themeMutator, focusRestoreNode, onDismiss);
        _entries.Add(entry);
        return entry;
    }
    
    /// <summary>
    /// Captures a combined theme mutator from ancestor ThemePanelNodes.
    /// </summary>
    private static Func<Hex1bTheme, Hex1bTheme>? CaptureThemeMutator(Hex1bNode? node)
    {
        // Collect theme mutators from ancestors (innermost first)
        var mutators = new List<Func<Hex1bTheme, Hex1bTheme>>();
        var current = node;
        while (current != null)
        {
            if (current is ThemePanelNode themePanel && themePanel.ThemeMutator != null)
            {
                mutators.Add(themePanel.ThemeMutator);
            }
            current = current.Parent;
        }
        
        if (mutators.Count == 0)
        {
            return null;
        }
        
        // Reverse so we apply from outermost to innermost (like during render)
        mutators.Reverse();
        
        // Combine into a single mutator
        return theme =>
        {
            var result = theme;
            foreach (var mutator in mutators)
            {
                result = mutator(result);
            }
            return result;
        };
    }
    
    /// <summary>
    /// Pushes an anchored popup positioned relative to a specific node.
    /// </summary>
    /// <param name="anchorNode">The node to anchor the popup to.</param>
    /// <param name="position">Where to position the popup relative to the anchor.</param>
    /// <param name="content">The widget content for the popup.</param>
    /// <param name="focusRestoreNode">Optional node to restore focus to when this popup is dismissed.</param>
    /// <param name="onDismiss">Optional callback invoked when this popup is dismissed to clean up owner state.</param>
    /// <returns>The popup entry for optional fluent configuration (e.g., <c>.AsBarrier()</c>).</returns>
    public PopupEntry PushAnchored(Hex1bNode anchorNode, AnchorPosition position, Hex1bWidget content, Hex1bNode? focusRestoreNode = null, Action? onDismiss = null)
    {
        return PushAnchored(anchorNode, position, () => content, focusRestoreNode, onDismiss);
    }

    /// <summary>
    /// Removes the topmost popup from the stack.
    /// </summary>
    /// <returns>True if a popup was removed, false if stack was empty.</returns>
    public bool Pop()
    {
        return Pop(out _);
    }
    
    /// <summary>
    /// Removes the topmost popup from the stack and returns the node that should receive focus.
    /// </summary>
    /// <param name="focusRestoreNode">The node that was designated to receive focus when this popup was pushed, or null.</param>
    /// <returns>True if a popup was removed, false if stack was empty.</returns>
    public bool Pop(out Hex1bNode? focusRestoreNode)
    {
        if (_entries.Count == 0)
        {
            focusRestoreNode = null;
            return false;
        }
        
        var entry = _entries[^1];
        focusRestoreNode = entry.FocusRestoreNode;
        _entries.RemoveAt(_entries.Count - 1);
        
        // Invoke dismiss callback to clean up owner state (e.g., clear IsOpen on MenuNode)
        entry.OnDismiss?.Invoke();
        
        return true;
    }
    
    /// <summary>
    /// Removes popups from the stack until a barrier is reached or the stack is empty.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method implements cascade dismissal for menus:
    /// - Non-barrier popups (submenus) are popped automatically
    /// - Stops when it encounters a barrier popup or empties the stack
    /// - The barrier popup itself is NOT popped
    /// </para>
    /// <para>
    /// If the topmost popup is a barrier, only that popup is popped (using regular <see cref="Pop()"/>).
    /// </para>
    /// </remarks>
    /// <returns>True if any popups were removed, false if stack was empty.</returns>
    public bool PopToBarrier()
    {
        return PopAtPosition(-1, -1);
    }
    
    /// <summary>
    /// Removes popups from the stack, stopping if the click position falls within
    /// content bounds of a lower layer (not just its backdrop).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method extends cascade dismissal with geometry awareness:
    /// - Pops the topmost popup (where the click was registered on the backdrop)
    /// - For each subsequent layer, checks if the click position hits content
    /// - Stops unwinding if the click would land on a lower layer's content
    /// - Also stops at barriers as with <see cref="PopToBarrier"/>
    /// </para>
    /// <para>
    /// This enables nested menu UX where clicking outside a submenu but inside
    /// the parent menu dismisses only the submenu, not the entire menu stack.
    /// </para>
    /// </remarks>
    /// <param name="x">The X coordinate of the click (screen position), or -1 to skip position check.</param>
    /// <param name="y">The Y coordinate of the click (screen position), or -1 to skip position check.</param>
    /// <returns>True if any popups were removed, false if stack was empty.</returns>
    public bool PopAtPosition(int x, int y)
    {
        if (_entries.Count == 0) return false;
        
        // If the top entry is a barrier, just pop it (explicit click-away on barrier)
        if (_entries[^1].IsBarrier)
        {
            return Pop();
        }
        
        // Track the focus restore node from the first entry we pop
        // This is typically the menu trigger that should receive focus after dismissal
        Hex1bNode? focusRestoreNode = null;
        
        // Pop until we hit a barrier, content bounds, or empty the stack
        bool popped = false;
        while (_entries.Count > 0 && !_entries[^1].IsBarrier)
        {
            var entry = _entries[^1];
            _entries.RemoveAt(_entries.Count - 1);
            
            // Capture the focus restore node from the first popped entry
            focusRestoreNode ??= entry.FocusRestoreNode;
            
            // Invoke dismiss callback to clean up owner state (e.g., clear IsOpen on MenuNode)
            entry.OnDismiss?.Invoke();
            
            popped = true;
            
            // Check if remaining top entry's content contains the click position
            if (_entries.Count > 0 && x >= 0 && y >= 0)
            {
                var topEntry = _entries[^1];
                if (topEntry.ContentNode != null && topEntry.ContentNode.ContentBounds.Contains(x, y))
                {
                    // Click falls within this layer's content - stop unwinding
                    break;
                }
            }
        }
        
        // Restore focus to the designated node if we popped anything
        if (popped && focusRestoreNode != null)
        {
            focusRestoreNode.IsFocused = true;
        }
        
        return popped;
    }

    /// <summary>
    /// Clears all popups from the stack, invoking OnDismiss callbacks for each.
    /// Focus is restored to the first (bottommost) popup's FocusRestoreNode if set.
    /// </summary>
    public void Clear()
    {
        if (_entries.Count == 0) return;
        
        // Get the focus restore node from the first (bottommost) entry
        // This is the node that was focused before any menus were opened
        var focusRestoreNode = _entries[0].FocusRestoreNode;
        
        // Invoke OnDismiss for each entry to clean up owner state
        foreach (var entry in _entries)
        {
            entry.OnDismiss?.Invoke();
        }
        _entries.Clear();
        
        // Restore focus to the original node
        if (focusRestoreNode != null)
        {
            focusRestoreNode.IsFocused = true;
        }
    }
    
    /// <summary>
    /// Removes any popup entries whose anchor nodes are stale (have zero bounds).
    /// This happens when an anchor node is replaced during reconciliation but the popup
    /// still holds a reference to the old node.
    /// Call this after layout (Arrange) to clean up stale popups.
    /// </summary>
    /// <returns>True if any stale popups were removed.</returns>
    public bool RemoveStaleAnchoredPopups()
    {
        if (_entries.Count == 0) return false;
        
        var removed = false;
        
        // Iterate backwards to safely remove entries
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            
            // Check if this is an anchored popup with a stale anchor
            if (entry.ContentNode is AnchoredNode anchoredNode && anchoredNode.IsAnchorStale)
            {
                // Remove this stale popup entry
                _entries.RemoveAt(i);
                
                // Invoke dismiss callback to clean up owner state
                entry.OnDismiss?.Invoke();
                
                removed = true;
            }
        }
        
        return removed;
    }

    /// <summary>
    /// Builds the ZStack widgets for all popups in the stack.
    /// Each popup is wrapped in a transparent Backdrop that calls PopToBarrier() when clicked.
    /// Anchored popups are positioned relative to their anchor node.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context (typically from a ZStack).</param>
    /// <returns>An enumerable of backdrop widgets for the popup stack.</returns>
    public IEnumerable<Hex1bWidget> BuildWidgets<TParent>(WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
    {
        foreach (var entry in _entries)
        {
            var content = entry.ContentBuilder();
            
            // If anchored, wrap content in AnchoredWidget for positioning
            if (entry.AnchorNode != null)
            {
                content = new AnchoredWidget(content, entry.AnchorNode, entry.Position);
            }
            
            // Click-away uses PopToBarrier for cascade dismissal
            yield return ctx.Backdrop(content)
                .Transparent()
                .OnClickAway(() => PopToBarrier());
        }
    }
    
    /// <summary>
    /// Builds popup widgets wrapped in backdrops for internal use by the reconciler.
    /// Each popup is wrapped in a transparent Backdrop that uses PopAtPosition when clicked away.
    /// Anchored popups are positioned relative to their anchor node.
    /// Theme context is preserved from the original push location.
    /// </summary>
    /// <returns>An enumerable of backdrop-wrapped popup widgets.</returns>
    internal IEnumerable<Hex1bWidget> BuildPopupWidgets()
    {
        foreach (var entry in _entries)
        {
            var content = entry.ContentBuilder();
            
            // If we have a captured theme mutator, wrap content in ThemePanelWidget
            if (entry.ThemeMutator != null)
            {
                content = new ThemePanelWidget(entry.ThemeMutator, content);
            }
            
            // If anchored, wrap content in AnchoredWidget for positioning
            if (entry.AnchorNode != null)
            {
                content = new AnchoredWidget(content, entry.AnchorNode, entry.Position);
            }
            
            // Click-away uses PopAtPosition for coordinate-aware cascade dismissal
            yield return new BackdropWidget(content)
            {
                Style = BackdropStyle.Transparent,
                ClickAwayEventHandler = args => { PopAtPosition(args.X, args.Y); return Task.CompletedTask; }
            };
        }
    }
}
