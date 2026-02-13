using Hex1b;
using Hex1b.Animation;
using Hex1b.Input;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

// Demo: StatePanel + EffectPanel — identity-anchored reconciliation,
// animations, and visual post-processing effects.
//
// Shows a list of items that can be shuffled, added, and removed.
// Each item is wrapped in a StatePanel so its node identity (and animations)
// survive position changes. EffectPanel applies a shimmer highlight that
// flows along the border of the focused row.

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
                        v.Text(" ◆ StatePanel + EffectPanel Demo")),
                    v.Text($" {statusMessage}"),
                    v.Text(" Focused row has a shimmer highlight flowing along its border"),
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
        });

        // Animate a progress bar width
        var slide = sp.Animations.Get<NumericAnimator<int>>("slide", a =>
        {
            a.From = 0;
            a.To = 20;
            a.Duration = TimeSpan.FromMilliseconds(800);
            a.EasingFunction = Easing.EaseOutQuad;
        });

        // Build a visual bar based on animation progress
        var barWidth = slide.Value;
        var bar = new string('█', barWidth) + new string('░', 20 - barWidth);

        // Opacity affects the status color intensity
        var statusColor = Hex1bColor.FromRgb(
            (byte)(item.Color.R * fade.Value),
            (byte)(item.Color.G * fade.Value),
            (byte)(item.Color.B * fade.Value));

        // Shimmer animation — 10 second cycle, shimmer travels in first ~4s then pauses
        var shimmer = sp.Animations.Get<NumericAnimator<double>>("shimmer", a =>
        {
            a.From = 0.0;
            a.To = 1.0;
            a.Duration = TimeSpan.FromMilliseconds(10000);
            a.EasingFunction = Easing.Linear;
            a.Repeat = true;
        });

        return sp.Interactable(ic =>
        {
            // Focus/hover fade animation — retargets smoothly on state change
            var focusFade = sp.Animations.Get<NumericAnimator<double>>("focus", a =>
            {
                a.Duration = TimeSpan.FromMilliseconds(1000);
                a.EasingFunction = Easing.EaseOutQuad;
            });
            focusFade.AnimateTo(ic.IsFocused ? 1.0 : 0.0);

            var hoverFade = sp.Animations.Get<NumericAnimator<double>>("hover", a =>
            {
                a.Duration = TimeSpan.FromMilliseconds(1000);
                a.EasingFunction = Easing.EaseOutQuad;
            });
            hoverFade.AnimateTo(ic.IsHovered ? 1.0 : 0.0);

            // Blend colors based on animated focus/hover intensity
            var highlight = Math.Max(focusFade.Value, hoverFade.Value * 0.6);
            var nameColor = Helpers.Lerp(Hex1bColor.Gray, Hex1bColor.White, highlight);
            var borderColor = Helpers.Lerp(Hex1bColor.DarkGray, Hex1bColor.Cyan, focusFade.Value);
            borderColor = Helpers.Lerp(borderColor, Hex1bColor.Blue, hoverFade.Value * (1.0 - focusFade.Value));

            // Background fades in on focus
            var bgR = (byte)(20 * focusFade.Value + 15 * hoverFade.Value * (1.0 - focusFade.Value));
            var bgG = (byte)(40 * focusFade.Value + 25 * hoverFade.Value * (1.0 - focusFade.Value));
            var bgB = (byte)(60 * focusFade.Value + 40 * hoverFade.Value * (1.0 - focusFade.Value));
            var bgColor = Hex1bColor.FromRgb(bgR, bgG, bgB);

            var content = ic.ThemePanel(
                t =>
                {
                    var themed = t.Set(BorderTheme.BorderColor, borderColor);
                    if (focusFade.Value > 0.01 || hoverFade.Value > 0.01)
                        themed = themed.Set(GlobalTheme.BackgroundColor, bgColor);
                    return themed;
                },
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

            // Apply shimmer effect to focused row via EffectPanel
            var shimmerIntensity = focusFade.Value;
            if (shimmerIntensity > 0.01)
            {
                // Map first 30% of the 10s cycle to shimmer travel (0→1.5)
                // The overshoot past 1.0 lets the shimmer tail exit cleanly
                // Remaining 70% is a pause with no shimmer visible
                var shimmerRaw = shimmer.Value;
                var shimmerProgress = shimmerRaw < 0.3 ? (shimmerRaw / 0.3) * 1.5 : 2.0;

                if (shimmerProgress < 2.0)
                {
                    var capturedProgress = shimmerProgress;
                    var capturedIntensity = shimmerIntensity;
                    return ic.EffectPanel(content, effect: surface =>
                        Helpers.ApplyBorderShimmer(surface, capturedProgress, capturedIntensity));
                }
            }
            return content;
        })
        .OnClick(args =>
        {
            statusMessage = $"Clicked '{item.Name}' — identity: {item.GetHashCode():X8}";
        });
    });
}

record ItemModel(string Name, string Status, Hex1bColor Color);

static partial class Helpers
{
    public static Hex1bColor Lerp(Hex1bColor a, Hex1bColor b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Hex1bColor.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    /// <summary>
    /// Applies a split shimmer highlight along border cells.
    /// Two bright spots start at top-left, one clockwise, one counter-clockwise,
    /// meeting at bottom-right. Each has a bright head and fading tail.
    /// </summary>
    public static void ApplyBorderShimmer(Surface surface, double progress, double intensity, int shimmerWidth = 6)
    {
        var w = surface.Width;
        var h = surface.Height;
        if (w < 2 || h < 2) return;

        var perimeter = 2 * (w - 1) + 2 * (h - 1);
        var halfPerimeter = perimeter / 2.0;
        var headPos = progress * halfPerimeter;

        for (int i = 0; i < perimeter; i++)
        {
            var (x, y) = PerimeterIndexToXY(i, w, h);

            var cwDist = (double)i;
            var ccwDist = i == 0 ? 0.0 : (double)(perimeter - i);

            var behindA = headPos - cwDist;
            var behindB = headPos - ccwDist;

            var tA = (behindA >= 0 && behindA <= shimmerWidth)
                ? (1.0 + Math.Cos(behindA / shimmerWidth * Math.PI)) / 2.0
                : 0.0;
            var tB = (behindB >= 0 && behindB <= shimmerWidth)
                ? (1.0 + Math.Cos(behindB / shimmerWidth * Math.PI)) / 2.0
                : 0.0;

            var t = Math.Max(tA, tB) * intensity;
            if (t < 0.01) continue;

            var cell = surface[x, y];
            var brightened = BrightenColor(cell.Foreground, t);
            surface[x, y] = cell with { Foreground = brightened };
        }
    }

    public static (int x, int y) PerimeterIndexToXY(int index, int width, int height)
    {
        if (index < width)
            return (index, 0);
        index -= width;
        if (index < height - 1)
            return (width - 1, 1 + index);
        index -= (height - 1);
        if (index < width - 1)
            return (width - 2 - index, height - 1);
        index -= (width - 1);
        return (0, height - 2 - index);
    }

    private static Hex1bColor? BrightenColor(Hex1bColor? color, double t)
    {
        if (color is null || color.Value.IsDefault)
            return Hex1bColor.FromRgb((byte)(255 * t), (byte)(255 * t), (byte)(255 * t));
        var c = color.Value;
        return Hex1bColor.FromRgb(
            (byte)(c.R + (255 - c.R) * t),
            (byte)(c.G + (255 - c.G) * t),
            (byte)(c.B + (255 - c.B) * t));
    }
}
