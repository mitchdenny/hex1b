// SurfaceDemo - Demonstrates the SurfaceWidget with layered compositing

using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;
using SurfaceDemo;

// Available demos
var demos = new[] { "Fireflies", "Gradient", "Noise", "Slime Mold", "Snow", "Shadows" };
var selectedDemo = 0;

// Initialize firefly state
var random = new Random();
var fireflies = FirefliesDemo.CreateFireflies();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithRenderingModeFromEnvironment()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx =>
    {
        // Update based on selected demo
        if (selectedDemo == 0)
            FirefliesDemo.Update(fireflies, random);
        
        return ctx.Center(c => 
            c.VStack(v => [
                v.Border(
                    v.Surface(s => selectedDemo switch
                    {
                        0 => FirefliesDemo.BuildLayers(s, fireflies),
                        1 => GradientDemo.BuildLayers(s),
                        2 => NoiseDemo.BuildLayers(s, random),
                        3 => SlimeMoldDemo.BuildLayers(s, random),
                        4 => SnowDemo.BuildLayers(s, random),
                        5 => ShadowDemo.BuildLayers(s, random),
                        _ => FirefliesDemo.BuildLayers(s, fireflies)
                    })
                    .Width(SizeHint.Fixed(FirefliesDemo.WidthCells))
                    .Height(SizeHint.Fixed(FirefliesDemo.HeightCells))
                    .RedrawAfter(50),
                    title: demos[selectedDemo]
                )
                .FixedWidth(FirefliesDemo.RequiredWidth)
                .FixedHeight(FirefliesDemo.RequiredHeight),
                v.Center(vc => 
                    vc.HStack(h => [
                        h.Text("Demo: "),
                        h.Picker(demos, selectedDemo)
                            .OnSelectionChanged(e => selectedDemo = e.SelectedIndex)
                    ]).Height(SizeHint.Content)
                ).Height(SizeHint.Content)
            ])
        );
    })
    .Build();

await terminal.RunAsync();

