namespace Custard.Widgets;

public abstract record CustardWidget;

public sealed record TextBlockWidget(string Text) : CustardWidget;

public sealed record TextBoxWidget(TextBoxState State) : CustardWidget;

public sealed record VStackWidget(IReadOnlyList<CustardWidget> Children) : CustardWidget;

public sealed record HStackWidget(IReadOnlyList<CustardWidget> Children) : CustardWidget;
