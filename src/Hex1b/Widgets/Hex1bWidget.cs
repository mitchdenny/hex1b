using Hex1b.Input;
using Hex1b.Layout;

namespace Hex1b.Widgets;

public abstract record Hex1bWidget
{
    /// <summary>
    /// Keyboard shortcuts for this widget. Processed hierarchically from focused widget to root.
    /// </summary>
    public IReadOnlyList<Shortcut>? Shortcuts { get; init; }

    /// <summary>
    /// Hint for how this widget should be sized horizontally within its parent.
    /// Used by HStack to distribute width among children.
    /// </summary>
    public SizeHint? WidthHint { get; init; }

    /// <summary>
    /// Hint for how this widget should be sized vertically within its parent.
    /// Used by VStack to distribute height among children.
    /// </summary>
    public SizeHint? HeightHint { get; init; }
}

/// <summary>
/// How text should handle horizontal overflow.
/// </summary>
public enum TextOverflow
{
    /// <summary>
    /// Text extends beyond bounds (default, for backward compatibility).
    /// Clipping is handled by parent LayoutNode if present.
    /// </summary>
    Overflow,
    
    /// <summary>
    /// Text wraps to next line when it exceeds available width.
    /// This affects the measured height of the node.
    /// </summary>
    Wrap,
    
    /// <summary>
    /// Text is truncated with ellipsis when it exceeds available width.
    /// </summary>
    Ellipsis,
}

public sealed record TextBlockWidget(string Text, TextOverflow Overflow = TextOverflow.Overflow) : Hex1bWidget;

public sealed record TextBoxWidget(TextBoxState State) : Hex1bWidget;

public sealed record VStackWidget(IReadOnlyList<Hex1bWidget> Children) : Hex1bWidget;

public sealed record HStackWidget(IReadOnlyList<Hex1bWidget> Children) : Hex1bWidget;

/// <summary>
/// A widget that draws a box border around its child content.
/// </summary>
public sealed record BorderWidget(Hex1bWidget Child, string? Title = null) : Hex1bWidget;

/// <summary>
/// A widget that provides a styled background for its child content.
/// </summary>
public sealed record PanelWidget(Hex1bWidget Child) : Hex1bWidget;
