using Hex1b;
using Hex1b.Terminal;
using Hex1b.Theming;
using Hex1b.Widgets;

// Fog of War Demo - Shows a colorful grid that fades to black based on distance from mouse cursor
// Run with: dotnet run --project samples/FogOfWar

Console.WriteLine("Fog of War Demo");
Console.WriteLine("===============");
Console.WriteLine("Move your mouse around to see the fog effect!");
Console.WriteLine("Press any key to start...");
Console.ReadKey(true);

try
{
    // Create the base console presentation adapter with mouse support
    var basePresentation = new ConsolePresentationAdapter(enableMouse: true);
    
    // Wrap it with the fog of war adapter
    var fogPresentation = new FogOfWarPresentationAdapter(basePresentation, maxDistance: 15.0);
    
    // Create the workload adapter that Hex1bApp will use
    var workload = new Hex1bAppWorkloadAdapter(fogPresentation.Capabilities);
    
    // Create the terminal that bridges presentation ↔ workload
    using var terminal = new Hex1bTerminal(fogPresentation, workload);

    await using var app = new Hex1bApp(
        ctx => ctx.VStack(root => [
            // Title
            root.Border(
                root.VStack(content => [
                    root.Text("🌫️  FOG OF WAR DEMO  🌫️"),
                    root.Text(""),
                    root.Text("Move your mouse to reveal the colorful grid below!"),
                    root.Text(""),
                ]),
                title: "Fog of War Effect"
            ),
            
            // Colorful grid content
            root.VStack(grid =>
            {
                var widgets = new List<Hex1bWidget>();
                
                // Create a colorful grid
                for (var y = 0; y < 15; y++)
                {
                    var rowWidgets = new List<Hex1bWidget>();
                    
                    for (var x = 0; x < 60; x++)
                    {
                        // Create rainbow pattern
                        var hue = (x * 6 + y * 12) % 360;
                        var (r, g, b) = HsvToRgb(hue, 0.8, 0.9);
                        
                        // Checkerboard pattern with different characters
                        var character = (x + y) % 2 == 0 ? "█" : "▓";
                        
                        rowWidgets.Add(grid.Text(character));
                    }
                    
                    widgets.Add(grid.HStack(h => rowWidgets.ToArray()));
                }
                
                return widgets.ToArray();
            }),
            
            // Instructions
            root.InfoBar([
                "Move Mouse", "Reveal Grid",
                "Ctrl+C", "Exit"
            ]),
        ]),
        new Hex1bAppOptions
        {
            WorkloadAdapter = workload,
            EnableMouse = true
        }
    );

    await app.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey(true);
}

Console.WriteLine("Done!");

// Helper function to convert HSV to RGB
static (byte r, byte g, byte b) HsvToRgb(double h, double s, double v)
{
    var c = v * s;
    var x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
    var m = v - c;

    double r1, g1, b1;
    if (h < 60)
    {
        (r1, g1, b1) = (c, x, 0);
    }
    else if (h < 120)
    {
        (r1, g1, b1) = (x, c, 0);
    }
    else if (h < 180)
    {
        (r1, g1, b1) = (0, c, x);
    }
    else if (h < 240)
    {
        (r1, g1, b1) = (0, x, c);
    }
    else if (h < 300)
    {
        (r1, g1, b1) = (x, 0, c);
    }
    else
    {
        (r1, g1, b1) = (c, 0, x);
    }

    return (
        (byte)Math.Round((r1 + m) * 255),
        (byte)Math.Round((g1 + m) * 255),
        (byte)Math.Round((b1 + m) * 255)
    );
}
