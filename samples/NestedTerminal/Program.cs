using Hex1b;
using Hex1b.Terminal;
using Hex1b.Widgets;

// Demonstration of nested terminal support (tmux-style)
// Shows how a TerminalWidget can host another Hex1bApp

Console.WriteLine("Nested Terminal Demo");
Console.WriteLine("====================");
Console.WriteLine("This demonstrates embedding terminals within terminals.");
Console.WriteLine("Press any key to start...");
Console.ReadKey(true);

try
{
    // Create the main presentation adapter for the outer console
    var outerPresentation = new ConsolePresentationAdapter(enableMouse: false);
    
    // Create the outer app's workload adapter
    var outerWorkload = new Hex1bAppWorkloadAdapter(outerPresentation.Capabilities);
    
    // Create the outer terminal that bridges presentation ↔ workload
    using var outerTerminal = new Hex1bTerminal(outerPresentation, outerWorkload);

    // ===== Create a nested terminal =====
    
    // Create the workload adapter for the nested app
    var nestedWorkload = new Hex1bAppWorkloadAdapter();
    
    // Create a headless terminal (no presentation adapter - we'll render it ourselves)
    var nestedTerminal = new Hex1bTerminal(nestedWorkload, width: 60, height: 15);
    
    // Create the nested app that will run inside the nested terminal
    var nestedApp = new Hex1bApp(
        ctx => ctx.VStack(v => [
            v.Border(
                v.VStack(inner => [
                    inner.Text("╔═══════════════════════════════════════════════╗"),
                    inner.Text("║   NESTED TERMINAL - This is a Hex1bApp        ║"),
                    inner.Text("║   running inside another Hex1bApp!            ║"),
                    inner.Text("╚═══════════════════════════════════════════════╝"),
                    inner.Text(""),
                    inner.Text("This nested app has its own:"),
                    inner.Text("  • Render loop"),
                    inner.Text("  • Virtual terminal emulator"),
                    inner.Text("  • Screen buffer"),
                    inner.Text(""),
                    inner.Text("The outer app displays this nested terminal"),
                    inner.Text("using a TerminalWidget!"),
                ]),
                title: "Nested App"
            ).Fill(),
        ]),
        new Hex1bAppOptions
        {
            WorkloadAdapter = nestedWorkload,
            EnableMouse = false
        }
    );

    // Start the nested app in the background (it renders to the nested terminal)
    var nestedAppTask = Task.Run(async () => 
    {
        try
        {
            await nestedApp.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Nested app error: {ex.Message}");
        }
    });

    // Give the nested app a moment to render
    await Task.Delay(500);

    // ===== Create the outer app that displays the nested terminal =====
    
    var counter = 0;
    
    await using var outerApp = new Hex1bApp(
        ctx => ctx.VStack(root => [
            root.Border(
                root.VStack(outer => [
                    outer.Text("╔══════════════════════════════════════════════════════════════════╗"),
                    outer.Text("║   OUTER APP - Hosting a nested terminal                          ║"),
                    outer.Text("╚══════════════════════════════════════════════════════════════════╝"),
                    outer.Text(""),
                    outer.Text("This outer app uses TerminalWidget to display another Hex1bApp:"),
                    outer.Text(""),
                    
                    // Display the nested terminal using TerminalWidget
                    outer.Border(
                        outer.Terminal(nestedTerminal),
                        title: "Embedded Terminal"
                    ),
                    
                    outer.Text(""),
                    outer.Text("The embedded terminal above is a real Hex1bTerminal with its"),
                    outer.Text("own screen buffer, ANSI parsing, and render loop!"),
                    outer.Text(""),
                    outer.HStack(h => [
                        h.Button("Increment Counter").OnClick(_ => counter++),
                        h.Text($"  Counter: {counter}"),
                    ]),
                ]),
                title: "Outer App"
            ).Fill(),
            
            root.InfoBar([
                "Tab", "Navigate", 
                "Enter", "Activate Button", 
                "Ctrl+C", "Exit"
            ]),
        ]),
        new Hex1bAppOptions
        {
            WorkloadAdapter = outerWorkload,
            EnableMouse = false
        }
    );

    await outerApp.RunAsync();
    
    // Clean up nested app
    await nestedApp.DisposeAsync();
    nestedTerminal.Dispose();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey(true);
}

Console.WriteLine("Done!");
