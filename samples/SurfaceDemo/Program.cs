// SurfaceDemo - Demonstrates the SurfaceWidget with layered compositing
// Run with "web" argument to launch web-based terminal (for sixel testing)

using System.Net.WebSockets;
using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;
using SurfaceDemo;

// Check for web mode
if (args.Length > 0 && args[0].Equals("web", StringComparison.OrdinalIgnoreCase))
{
    await RunWebMode(args);
    return;
}

// Console mode (default)
await RunConsoleMode();

async Task RunConsoleMode()
{
    var demos = new[] { "Fireflies", "Gradient", "Noise", "Slime Mold", "Snow", "Shadows", "Gravity", "Radar", "Smart Matter", "Sixel (Experimental)" };
    var selectedDemo = 0;
    var random = new Random();
    var fireflies = FirefliesDemo.CreateFireflies();

    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithMouse()
        .WithHex1bApp((app, options) => ctx =>
        {
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
                            6 => GravityDemo.BuildLayers(s, random),
                            7 => RadarDemo.BuildLayers(s, random),
                            8 => SmartMatterDemo.BuildLayers(s, random),
                            9 => SixelDemo.BuildLayers(s, random),
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
}

async Task RunWebMode(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    var app = builder.Build();

    app.UseWebSockets();
    
    // Serve static files from wwwroot
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // WebSocket endpoint for terminal
    app.Map("/ws/terminal", async context =>
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await using var presentation = new WebSocketPresentationAdapter(webSocket, 120, 40, enableMouse: true);
        
        var demos = new[] { "Fireflies", "Gradient", "Noise", "Slime Mold", "Snow", "Shadows", "Gravity", "Radar", "Smart Matter", "Sixel (Experimental)" };
        var selectedDemo = 0;
        var random = new Random();
        var fireflies = FirefliesDemo.CreateFireflies();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(presentation)
            .WithHex1bApp((app, options) => ctx =>
            {
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
                                6 => GravityDemo.BuildLayers(s, random),
                                7 => RadarDemo.BuildLayers(s, random),
                                8 => SmartMatterDemo.BuildLayers(s, random),
                                9 => SixelDemo.BuildLayers(s, random),
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

        await terminal.RunAsync(context.RequestAborted);
    });

    Console.WriteLine("SurfaceDemo running in web mode");
    Console.WriteLine("Open http://localhost:5000 in your browser");
    
    await app.RunAsync();
}

