using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b;

public static class Hex1bWidgets
{
    public static Task<Hex1bWidget> TextBlockAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult<Hex1bWidget>(new TextBlockWidget(text));

    public static Task<Hex1bWidget> TextBoxAsync(string? initialText = null, CancellationToken cancellationToken = default) => Task.FromResult<Hex1bWidget>(new TextBoxWidget(initialText));

    public static Task<Hex1bWidget> VStackAsync(IReadOnlyList<Hex1bWidget> children, CancellationToken cancellationToken = default) => Task.FromResult<Hex1bWidget>(new VStackWidget(children));

    public static Task<Hex1bWidget> VStackAsync(CancellationToken cancellationToken = default, params Hex1bWidget[] children) => Task.FromResult<Hex1bWidget>(new VStackWidget(children));

    public static Task<Hex1bWidget> HStackAsync(IReadOnlyList<Hex1bWidget> children, CancellationToken cancellationToken = default) => Task.FromResult<Hex1bWidget>(new HStackWidget(children));

    public static Task<Hex1bWidget> HStackAsync(CancellationToken cancellationToken = default, params Hex1bWidget[] children) => Task.FromResult<Hex1bWidget>(new HStackWidget(children));

    public static Task<Hex1bWidget> ButtonAsync(string label, Action<ActionContext> onClick, CancellationToken cancellationToken = default) => Task.FromResult<Hex1bWidget>(new ButtonWidget(label) { OnClick = ctx => { onClick(ctx); return Task.CompletedTask; } });
    
    public static Task<Hex1bWidget> ButtonAsync(string label, Func<ActionContext, Task> onClick, CancellationToken cancellationToken = default) => Task.FromResult<Hex1bWidget>(new ButtonWidget(label) { OnClick = onClick });
}
