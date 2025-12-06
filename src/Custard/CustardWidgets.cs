using Custard.Widgets;

namespace Custard;

public static class CustardWidgets
{
    public static Task<CustardWidget> TextBlockAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult<CustardWidget>(new TextBlockWidget(text));

    public static Task<CustardWidget> TextBoxAsync(TextBoxState state, CancellationToken cancellationToken = default) => Task.FromResult<CustardWidget>(new TextBoxWidget(state));

    public static Task<CustardWidget> VStackAsync(IReadOnlyList<CustardWidget> children, CancellationToken cancellationToken = default) => Task.FromResult<CustardWidget>(new VStackWidget(children));

    public static Task<CustardWidget> VStackAsync(CancellationToken cancellationToken = default, params CustardWidget[] children) => Task.FromResult<CustardWidget>(new VStackWidget(children));

    public static Task<CustardWidget> HStackAsync(IReadOnlyList<CustardWidget> children, CancellationToken cancellationToken = default) => Task.FromResult<CustardWidget>(new HStackWidget(children));

    public static Task<CustardWidget> HStackAsync(CancellationToken cancellationToken = default, params CustardWidget[] children) => Task.FromResult<CustardWidget>(new HStackWidget(children));
}
