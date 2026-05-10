using Hex1b;
using Hex1b.Composition;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

// Demo: composing widgets via Hex1bWidget.Build — composite widgets without a custom node.
//
// Three widgets cooperate without any of the usual workarounds (no node
// subclasses, no FindAncestor walks, no closure-over-mutable-node tricks):
//
//   AppShellWidget
//     ├─ provides a CounterStore via ctx.Provide(...)
//     └─ contains:
//         CounterDisplayWidget
//             └─ pulls the store via ctx.Use<CounterStore>() and renders count
//         CounterStatusWidget
//             └─ pulls the store via ctx.Use<CounterStore>() and renders status
//
// Every widget ships a fluent extension method (see CompositionDemoExtensions
// at the bottom of this file), so call sites stay on the standard `ctx.X(...)`
// surface and never write `new ...Widget(...)` inside a Build method.

Hex1bApp? app = null;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((a, options) =>
    {
        app = a;
        return ctx => ctx.AppShell(InvalidateApp, RequestStopApp);
    })
    .Build();

await terminal.RunAsync();

void InvalidateApp() => app?.Invalidate();
void RequestStopApp() => app?.RequestStop();

// --- Composite widgets ---

internal sealed record AppShellWidget(Action Invalidate, Action RequestStop) : Hex1bWidget
{
    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        // Per-instance store, allocated once and reused across frames.
        var store = ctx.UseState(() => new CounterStore { Invalidate = Invalidate });

        // Publish to descendants via the typed ambient API.
        ctx.Provide(store);

        return ctx.VStack(v =>
        [
            v.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                v.Text(" ◆ Composition Demo")),
            v.Text(" Up/Down to change counter, R to reset, Q to quit"),
            v.Separator(),

            v.CounterDisplay(),
            v.CounterStatus(),

            v.Separator(),
            v.Text(" Both inner widgets share state via ctx.Use<CounterStore>()"),
        ])
        .InputBindings(b =>
        {
            b.Key(Hex1bKey.UpArrow).Global().Action(_ => store.Increment(), "Increment");
            b.Key(Hex1bKey.DownArrow).Global().Action(_ => store.Decrement(), "Decrement");
            b.Key(Hex1bKey.R).Global().Action(_ => store.Reset(), "Reset");
            b.Key(Hex1bKey.Q).Global().Action(_ => RequestStop(), "Quit");
        });
    }
}

internal sealed record CounterDisplayWidget : Hex1bWidget
{
    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var store = ctx.Require<CounterStore>();
        return ctx.Text($"  Count: {store.Count}");
    }
}

internal sealed record CounterStatusWidget : Hex1bWidget
{
    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var store = ctx.Require<CounterStore>();
        var status = store.Count switch
        {
            0 => "  (idle)",
            > 0 => $"  ↑ positive ({store.Count})",
            _ => $"  ↓ negative ({store.Count})",
        };
        return ctx.Text(status);
    }
}

// --- Shared state object ---

internal sealed class CounterStore
{
    public int Count { get; private set; }
    public required Action Invalidate { get; init; }

    public void Increment() { Count++; Invalidate(); }
    public void Decrement() { Count--; Invalidate(); }
    public void Reset() { Count = 0; Invalidate(); }
}

// --- Fluent extensions: one per composite ---

internal static class CompositionDemoExtensions
{
    public static AppShellWidget AppShell<T>(
        this WidgetContext<T> context,
        Action invalidate,
        Action requestStop) where T : Hex1bWidget
        => new(invalidate, requestStop);

    public static CounterDisplayWidget CounterDisplay<T>(this WidgetContext<T> context)
        where T : Hex1bWidget
        => new();

    public static CounterStatusWidget CounterStatus<T>(this WidgetContext<T> context)
        where T : Hex1bWidget
        => new();
}
