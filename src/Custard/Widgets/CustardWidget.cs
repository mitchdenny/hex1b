using Custard.Input;
using Custard.Layout;

namespace Custard.Widgets;

public abstract record CustardWidget
{
    /// <summary>
    /// Keyboard shortcuts for this widget. Processed hierarchically from focused widget to root.
    /// </summary>
    public IReadOnlyList<Shortcut>? Shortcuts { get; init; }
}

public sealed record TextBlockWidget(string Text) : CustardWidget;

public sealed record TextBoxWidget(TextBoxState State) : CustardWidget;

public sealed record VStackWidget(IReadOnlyList<CustardWidget> Children, IReadOnlyList<SizeHint>? ChildHeightHints = null) : CustardWidget;

public sealed record HStackWidget(IReadOnlyList<CustardWidget> Children, IReadOnlyList<SizeHint>? ChildWidthHints = null) : CustardWidget;
