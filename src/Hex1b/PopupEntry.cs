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
