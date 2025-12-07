using Hex1b.Input;
using Hex1b.Layout;

namespace Hex1b.Widgets;

public abstract record Hex1bWidget
{
    /// <summary>
    /// Keyboard shortcuts for this widget. Processed hierarchically from focused widget to root.
    /// </summary>
    public IReadOnlyList<Shortcut>? Shortcuts { get; init; }
}

public sealed record TextBlockWidget(string Text) : Hex1bWidget;

public sealed record TextBoxWidget(TextBoxState State) : Hex1bWidget;

public sealed record VStackWidget(IReadOnlyList<Hex1bWidget> Children, IReadOnlyList<SizeHint>? ChildHeightHints = null) : Hex1bWidget;

public sealed record HStackWidget(IReadOnlyList<Hex1bWidget> Children, IReadOnlyList<SizeHint>? ChildWidthHints = null) : Hex1bWidget;

/// <summary>
/// A widget that draws a box border around its child content.
/// </summary>
public sealed record BorderWidget(Hex1bWidget Child, string? Title = null) : Hex1bWidget;

/// <summary>
/// A widget that provides a styled background for its child content.
/// </summary>
public sealed record PanelWidget(Hex1bWidget Child) : Hex1bWidget;
