# EffectPanelWidget

Apply visual post-processing effects to a child widget's rendered output.

EffectPanelWidget captures a child's rendered output to a temporary surface, passes it to an effect callback for in-place modification, then composites the result back. The child remains fully interactive — focus, input, and hit testing all work normally through the effect.

## Basic Usage

Wrap any widget with an EffectPanel and provide an effect callback:

```csharp
using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;

var app = new Hex1bApp(ctx =>
    ctx.EffectPanel(
        ctx.Text("This text is dimmed"),
        surface =>
        {
            for (int y = 0; y < surface.Height; y++)
            for (int x = 0; x < surface.Width; x++)
            {
                var cell = surface[x, y];
                if (cell.Foreground is { } fg)
                    surface[x, y] = cell with
                    {
                        Foreground = Hex1bColor.FromRgb(
                            (byte)(fg.R / 2), (byte)(fg.G / 2), (byte)(fg.B / 2))
                    };
            }
        }
    )
);

await app.RunAsync();
```

You can also create the widget first and chain `.Effect()`:

```csharp
ctx.EffectPanel(ctx.Text("Hello"))
    .Effect(surface => { /* modify surface */ });
```

## How It Works

1. **Measure/Arrange**: The child is measured and arranged normally
2. **Render**: The child renders to a temporary `Surface` (same size as the EffectPanel bounds)
3. **Effect**: Your callback receives the `Surface` for in-place cell modification
4. **Composite**: The modified surface is written to the parent render context, clipped to the panel's bounds

The effect sees the child's **fully rendered output** — all text, colors, and attributes are already resolved.

## Working with Surface Cells

The `Surface` passed to your effect callback provides direct cell access:

```csharp
surface =>
{
    for (int y = 0; y < surface.Height; y++)
    for (int x = 0; x < surface.Width; x++)
    {
        var cell = surface[x, y];

        // Available properties:
        // cell.Character  - the rendered character
        // cell.Foreground - foreground Hex1bColor (nullable)
        // cell.Background - background Hex1bColor (nullable)
        // cell.IsBold, cell.IsItalic, etc.

        // Modify with `with` expression and write back:
        surface[x, y] = cell with { Foreground = Hex1bColor.White };
    }
}
```

## Combining with StatePanel

EffectPanel pairs naturally with StatePanel for **animated effects**. The StatePanel provides identity-anchored animation state, and EffectPanel applies the visual result:

```csharp
ctx.StatePanel(item, sp =>
{
    var shimmer = sp.GetAnimations().Get<NumericAnimator<double>>("shimmer", a =>
    {
        a.From = 0.0;
        a.To = 1.0;
        a.Duration = TimeSpan.FromMilliseconds(2000);
        a.EasingFunction = Easing.Linear;
        a.Repeat = true;
    });

    return sp.EffectPanel(
        sp.Text(item.Name),
        surface => ApplyShimmer(surface, shimmer.Value)
    );
});
```

::: tip No Special Relationship
StatePanelWidget and EffectPanelWidget are fully independent — neither knows about the other. They compose naturally because StatePanel provides state and EffectPanel consumes it, but you can use either one alone.
:::

## Layout Behavior

- **Measuring**: Passes constraints directly to the child
- **Arranging**: Passes bounds directly to the child
- **Focus**: Passes through to focusable children
- **No size overhead**: Adds zero pixels to the layout

## Related Widgets

- [StatePanel](/guide/widgets/statepanel) — Identity-anchored state and animations
- [Surface](/guide/widgets/surface) — Low-level surface rendering
- [ThemePanel](/guide/widgets/themepanel) — Scoped theme mutations (non-visual, like EffectPanel)
