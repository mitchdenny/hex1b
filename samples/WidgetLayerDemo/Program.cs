// WidgetLayerDemo - Demonstrates WidgetSurfaceLayer with animated transition effects
//
// Shows a bordered table with data. Press "Animate" to replace the content with a
// SurfaceWidget containing a WidgetLayer (same table) plus computed effect layers
// that animate over time. Pick different transitions to explore the concept.

using System.Diagnostics;
using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

// ---------- State ----------

var effects = new[]
{
    "Circular Reveal",
    "Fog of War",
    "Dim Fade",
    "Tint Wash",
    "Scanlines",
    "Vignette",
    "Invert",
};
int selectedEffect = 0;
bool animating = false;
var animationStart = Stopwatch.GetTimestamp();

// ---------- Table data ----------

var products = new List<Product>
{
    new("Laptop Pro 16\"", "Electronics", 2499.99m, 15),
    new("Mechanical Keyboard", "Electronics", 149.99m, 50),
    new("Wireless Mouse", "Electronics", 49.99m, 120),
    new("Standing Desk", "Furniture", 549.99m, 8),
    new("Ergonomic Chair", "Furniture", 399.99m, 22),
    new("Monitor 27\" 4K", "Electronics", 449.99m, 35),
    new("USB-C Hub 10-port", "Accessories", 79.99m, 75),
    new("Webcam 4K HDR", "Electronics", 129.99m, 40),
    new("LED Desk Lamp", "Furniture", 45.99m, 60),
    new("Cable Kit Deluxe", "Accessories", 24.99m, 200),
    new("Noise-Cancel Headset", "Electronics", 299.99m, 30),
    new("Monitor Arm Dual", "Accessories", 89.99m, 45),
    new("Thunderbolt Dock", "Electronics", 249.99m, 18),
    new("Footrest Adjustable", "Furniture", 59.99m, 90),
    new("Desk Organizer Set", "Accessories", 34.99m, 110),
};

object? focusedKey = products[0].Name;

// ---------- Helpers ----------

double GetProgress()
{
    if (!animating) return 0;
    var elapsed = Stopwatch.GetElapsedTime(animationStart).TotalSeconds;
    return Math.Clamp(elapsed / 2.0, 0, 1); // 2-second animation
}

Hex1bWidget BuildTableContent<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return ctx.Border(
        v => [
            v.Table((IReadOnlyList<Product>)products)
                .RowKey(p => p.Name)
                .Header(h => [
                    h.Cell("Product").Width(SizeHint.Fill),
                    h.Cell("Category").Width(SizeHint.Content),
                    h.Cell("Price").Width(SizeHint.Fixed(12)).Align(Alignment.Right),
                    h.Cell("Stock").Width(SizeHint.Fixed(8)).Align(Alignment.Right)
                ])
                .Row((r, product, state) => [
                    r.Cell(product.Name),
                    r.Cell(product.Category),
                    r.Cell($"${product.Price:F2}"),
                    r.Cell(product.Stock.ToString())
                ])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .FillHeight()
        ],
        title: "Product Inventory"
    );
}

CellCompute BuildEffect(int effectIndex, double progress, int width, int height)
{
    return effectIndex switch
    {
        0 => // Circular Reveal — expand from center
            SurfaceEffects.CircularReveal(
                width / 2, height / 2,
                (int)(Math.Max(width, height) * progress)),

        1 => // Fog of War — spotlight sweeping left to right
            SurfaceEffects.FogOfWar(
                (int)(width * progress), height / 2,
                radius: 8, fadeWidth: 4),

        2 => // Dim Fade — progressive dimming
            SurfaceEffects.Dim(progress),

        3 => // Tint Wash — cyan tint increasing in opacity
            SurfaceEffects.Tint(Hex1bColor.Cyan, progress * 0.8),

        4 => // Scanlines — appear with increasing intensity
            SurfaceEffects.Scanlines(progress * 0.6),

        5 => // Vignette — edges darken
            SurfaceEffects.Vignette(width, height, progress),

        6 => // Invert
            CellEffects.Invert(),

        _ => SurfaceEffects.Passthrough()
    };
}

// ---------- App ----------

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx =>
    {
        var progress = GetProgress();

        return ctx.VStack(v =>
        {
            var contentWidget = animating
                ? (Hex1bWidget)v.Surface(s =>
                    [
                        s.WidgetLayer(BuildTableContent(v)),
                        s.Layer(BuildEffect(selectedEffect, progress, s.Width, s.Height))
                    ])
                    .RedrawAfter(16) // ~60fps during animation
                    .FillHeight()
                : BuildTableContent(v).FillHeight();

            return [
                contentWidget,
                v.HStack(h => [
                    h.Text(" Effect: "),
                    h.Picker(effects, selectedEffect)
                        .OnSelectionChanged(e => selectedEffect = e.SelectedIndex),
                    h.Text("  "),
                    h.Button(animating ? "⏹ Reset" : "▶ Animate")
                        .OnClick(_ =>
                        {
                            animating = !animating;
                            if (animating)
                                animationStart = Stopwatch.GetTimestamp();
                        }),
                    h.Text($"  Progress: {progress:P0}")
                ]).Height(SizeHint.Content),
                v.Text(" Press Ctrl+C to exit").Height(SizeHint.Content)
            ];
        });
    })
    .Build();

await terminal.RunAsync();

// ---------- Data model ----------

record Product(string Name, string Category, decimal Price, int Stock);
