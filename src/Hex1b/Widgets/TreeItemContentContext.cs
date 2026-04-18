using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// Context that can be provided when building custom content for a tree item row.
/// </summary>
/// <remarks>
/// <para>
/// Provides structural information about the tree item's position so that builders
/// can make layout decisions (e.g., adjusting label width to account for indentation).
/// </para>
/// <para>
/// Runtime state (focus, selection, hover) is available on the <see cref="TreeItemWidget"/>
/// via its properties. The tree also applies appropriate foreground/background colors
/// around the content row automatically.
/// </para>
/// </remarks>
public readonly struct TreeItemContentContext
{
    /// <summary>
    /// The depth of this item in the tree (0 = root level).
    /// </summary>
    public int Depth { get; init; }

    /// <summary>
    /// The total left margin offset in characters consumed by tree chrome
    /// (guides, expand indicator, checkbox, icon) before this content starts.
    /// </summary>
    /// <remarks>
    /// Use this to adjust width calculations for alignment. For example, a label
    /// column that should be visually aligned across all depths can subtract
    /// <c>LeftMarginOffset</c> from its fixed width to compensate for indentation.
    /// </remarks>
    public int LeftMarginOffset { get; init; }

    /// <summary>
    /// Whether this item is currently focused in the tree.
    /// </summary>
    public bool IsFocused { get; init; }

    /// <summary>
    /// Whether this item is currently selected (in multi-select mode).
    /// </summary>
    public bool IsSelected { get; init; }

    /// <summary>
    /// The foreground color that should be applied to match the tree's
    /// current state (focused, hovered, selected, or default).
    /// </summary>
    public Hex1bColor ItemForegroundColor { get; init; }

    /// <summary>
    /// The background color that should be applied to match the tree's
    /// current state (focused, hovered, selected, or default).
    /// </summary>
    public Hex1bColor ItemBackgroundColor { get; init; }

    /// <summary>
    /// Whether this item is currently expanded.
    /// </summary>
    public bool IsExpanded { get; init; }

    /// <summary>
    /// Whether this item has children (actual or hinted for lazy loading).
    /// </summary>
    public bool HasChildren { get; init; }
}
