using Hex1b;
using Hex1b.Animation;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

// Demo: StatePanel — identity-anchored reconciliation + animations
//
// Shows a list of items that can be shuffled, added, and removed.
// Each item is wrapped in a StatePanel so its node identity (and animations)
// survive position changes. Without StatePanel, shuffling would cause
// focus and animation state to be lost.

var items = new List<ItemModel>
{
    new("apiservice", "Running", Hex1bColor.Green),
    new("frontend", "Starting", Hex1bColor.Yellow),
    new("postgres", "Running", Hex1bColor.Green),
    new("redis", "Stopped", Hex1bColor.Red),
    new("worker", "Running", Hex1bColor.Green),
};

Hex1bApp? app = null;
string statusMessage = "S=shuffle  A=add  D=delete last  Q=quit";
int nextId = 1;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((a, options) =>
    {
        app = a;
        return ctx =>
            ctx.StatePanel(app, root =>
                root.VStack(v => [
                    // Header
                    v.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                        v.Text(" ◆ StatePanel Demo — Identity-Anchored Reconciliation")),
                    v.Text($" {statusMessage}"),
                    v.Separator(),

                    // Item list — each wrapped in StatePanel for identity preservation
                    v.VStack(list =>
                        items.Select(item => BuildItemRow(list, item)).ToArray()
                    ).Fill(),

                    v.Separator(),
                    v.Text($" {items.Count} items"),
                ])
            ).WithInputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.S).Global().Action(_ =>
                {
                    var rng = new Random();
                    for (int i = items.Count - 1; i > 0; i--)
                    {
                        int j = rng.Next(i + 1);
                        (items[i], items[j]) = (items[j], items[i]);
                    }
                    statusMessage = "Shuffled! Node identities preserved across reorder.";
                }, "Shuffle");

                bindings.Key(Hex1bKey.A).Global().Action(_ =>
                {
                    var names = new[] { "cache", "queue", "gateway", "scheduler", "monitor", "proxy" };
                    var name = $"{names[nextId % names.Length]}-{nextId++}";
                    items.Add(new ItemModel(name, "New", Hex1bColor.Magenta));
                    statusMessage = $"Added '{name}' — watch it animate in!";
                }, "Add item");

                bindings.Key(Hex1bKey.D).Global().Action(_ =>
                {
                    if (items.Count > 0)
                    {
                        var removed = items[^1];
                        items.RemoveAt(items.Count - 1);
                        statusMessage = $"Removed '{removed.Name}' — node swept, animations disposed.";
                    }
                }, "Delete last");

                bindings.Key(Hex1bKey.Q).Global().Action(_ => app?.RequestStop(), "Quit");
            });
    })
    .WithMouse()
    .Build();

await terminal.RunAsync();

StatePanelWidget BuildItemRow(
    WidgetContext<VStackWidget> parent,
    ItemModel item)
{
    return parent.StatePanel(item, sp =>
    {
        // Get or create a fade-in animation — persists across shuffles!
        var fade = sp.Animations.Get<OpacityAnimator>("fade-in", a =>
        {
            a.Duration = TimeSpan.FromMilliseconds(600);
            a.EasingFunction = Easing.EaseOutCubic;
            a.Start();
        });

        // Animate a progress bar width
        var slide = sp.Animations.Get<NumericAnimator<int>>("slide", a =>
        {
            a.From = 0;
            a.To = 20;
            a.Duration = TimeSpan.FromMilliseconds(800);
            a.EasingFunction = Easing.EaseOutQuad;
            a.Start();
        });

        // Build a visual bar based on animation progress
        var barWidth = slide.Value;
        var bar = new string('█', barWidth) + new string('░', 20 - barWidth);

        // Opacity affects the status color intensity
        var statusColor = Hex1bColor.FromRgb(
            (byte)(item.Color.R * fade.Value),
            (byte)(item.Color.G * fade.Value),
            (byte)(item.Color.B * fade.Value));

        return sp.Interactable(ic =>
        {
            var nameColor = ic.IsFocused ? Hex1bColor.White
                : ic.IsHovered ? Hex1bColor.Cyan
                : Hex1bColor.Gray;
            var borderColor = ic.IsFocused ? Hex1bColor.Cyan
                : ic.IsHovered ? Hex1bColor.Blue
                : Hex1bColor.DarkGray;

            return ic.ThemePanel(
                t => t.Set(BorderTheme.BorderColor, borderColor),
                ic.Border(
                    ic.HStack(h => [
                        h.ThemePanel(
                            t => t.Set(GlobalTheme.ForegroundColor, statusColor),
                            h.Text($" ● ")).FixedWidth(4),
                        h.ThemePanel(
                            t => t.Set(GlobalTheme.ForegroundColor, nameColor),
                            h.Text(item.Name)).FixedWidth(20),
                        h.ThemePanel(
                            t => t.Set(GlobalTheme.ForegroundColor, statusColor),
                            h.Text(item.Status)).FixedWidth(12),
                        h.ThemePanel(
                            t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(60, 120, 180)),
                            h.Text(bar)),
                    ])
                )
            );
        })
        .OnClick(args =>
        {
            statusMessage = $"Clicked '{item.Name}' — identity: {item.GetHashCode():X8}";
        });
    });
}

record ItemModel(string Name, string Status, Hex1bColor Color);
