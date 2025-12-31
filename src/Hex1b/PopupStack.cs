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
    internal PopupEntry(PopupStack stack, Func<Hex1bWidget> contentBuilder, Hex1bNode? anchorNode = null, AnchorPosition position = AnchorPosition.Below)
    {
        Stack = stack;
        ContentBuilder = contentBuilder;
        AnchorNode = anchorNode;
        Position = position;
    }

    internal PopupStack Stack { get; }
    internal Func<Hex1bWidget> ContentBuilder { get; }
    internal Hex1bNode? AnchorNode { get; }
    internal AnchorPosition Position { get; }
    
    /// <summary>
    /// Gets whether this popup is a barrier that stops cascade dismissal.
    /// </summary>
    public bool IsBarrier { get; internal set; }
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
    /// </summary>
    /// <param name="anchorNode">The node to anchor the popup to.</param>
    /// <param name="position">Where to position the popup relative to the anchor.</param>
    /// <param name="contentBuilder">A function that builds the widget content for the popup.</param>
    /// <returns>The popup entry for optional fluent configuration (e.g., <c>.AsBarrier()</c>).</returns>
    public PopupEntry PushAnchored(Hex1bNode anchorNode, AnchorPosition position, Func<Hex1bWidget> contentBuilder)
    {
        var entry = new PopupEntry(this, contentBuilder, anchorNode, position);
        _entries.Add(entry);
        return entry;
    }
    
    /// <summary>
    /// Pushes an anchored popup positioned relative to a specific node.
    /// </summary>
    /// <param name="anchorNode">The node to anchor the popup to.</param>
    /// <param name="position">Where to position the popup relative to the anchor.</param>
    /// <param name="content">The widget content for the popup.</param>
    /// <returns>The popup entry for optional fluent configuration (e.g., <c>.AsBarrier()</c>).</returns>
    public PopupEntry PushAnchored(Hex1bNode anchorNode, AnchorPosition position, Hex1bWidget content)
    {
        return PushAnchored(anchorNode, position, () => content);
    }

    /// <summary>
    /// Removes the topmost popup from the stack.
    /// </summary>
    /// <returns>True if a popup was removed, false if stack was empty.</returns>
    public bool Pop()
    {
        if (_entries.Count == 0) return false;
        _entries.RemoveAt(_entries.Count - 1);
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
    /// If the topmost popup is a barrier, only that popup is popped (using regular <see cref="Pop"/>).
    /// </para>
    /// </remarks>
    /// <returns>True if any popups were removed, false if stack was empty.</returns>
    public bool PopToBarrier()
    {
        if (_entries.Count == 0) return false;
        
        // If the top entry is a barrier, just pop it (explicit click-away on barrier)
        if (_entries[^1].IsBarrier)
        {
            return Pop();
        }
        
        // Pop until we hit a barrier or empty the stack
        bool popped = false;
        while (_entries.Count > 0 && !_entries[^1].IsBarrier)
        {
            _entries.RemoveAt(_entries.Count - 1);
            popped = true;
        }
        
        return popped;
    }

    /// <summary>
    /// Clears all popups from the stack.
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
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
    /// Each popup is wrapped in a transparent Backdrop that uses PopToBarrier when clicked away.
    /// Anchored popups are positioned relative to their anchor node.
    /// </summary>
    /// <returns>An enumerable of backdrop-wrapped popup widgets.</returns>
    internal IEnumerable<Hex1bWidget> BuildPopupWidgets()
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
            yield return new BackdropWidget(content)
            {
                Style = BackdropStyle.Transparent,
                ClickAwayHandler = () => { PopToBarrier(); return Task.CompletedTask; }
            };
        }
    }
}
