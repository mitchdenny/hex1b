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
    var outerPresentation = new ConsolePresentationAdapter(enableMouse: true);
    
    // Create the outer app's workload adapter
    var outerWorkload = new Hex1bAppWorkloadAdapter(outerPresentation.Capabilities);
    
    // Create the outer terminal that bridges presentation ↔ workload
    using var outerTerminal = new Hex1bTerminal(outerPresentation, outerWorkload);

    // ===== Create a nested terminal =====
    
    // Create the workload adapter for the nested app
    var nestedWorkload = new Hex1bAppWorkloadAdapter();
    
    // Create a dummy workload for the screen buffer (nothing writes to this)
    // The screen buffer only receives content via the presentation adapter's WriteOutputAsync
    var screenBufferWorkload = new Hex1bAppWorkloadAdapter();
    
    // Create a headless terminal to capture the nested app's screen buffer
    // This terminal will receive output via WriteOutputAsync and store it in a screen buffer
    var nestedScreenBuffer = new Hex1bTerminal(screenBufferWorkload, width: 60, height: 20);
    
    // Create the presentation adapter that wraps the screen buffer terminal
    // The TerminalWidget will use this to display content and get notified when it changes
    var nestedPresentation = new Hex1bAppPresentationAdapter(nestedScreenBuffer);
    
    // Create the bridge terminal that connects the workload to the presentation adapter
    // This terminal reads from nestedWorkload and writes to nestedPresentation
    var nestedTerminal = new Hex1bTerminal(nestedPresentation, nestedWorkload);
    
    // Shared counter for the nested app to show ongoing activity
    var nestedCounter = 0;
    var spinnerFrames = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
    var startTime = DateTime.Now;
    
    // Create the nested app that will run inside the nested terminal
    var nestedApp = new Hex1bApp(
        ctx => 
        {
            var elapsed = DateTime.Now - startTime;
            var spinnerFrame = spinnerFrames[nestedCounter % spinnerFrames.Length];
            
            return ctx.VStack(v => [
                v.Border(
                    v.VStack(inner => [
                        inner.Text("╔═══════════════════════════════════════════════╗"),
                        inner.Text("║   NESTED TERMINAL - Live Updates!             ║"),
                        inner.Text("╚═══════════════════════════════════════════════╝"),
                        inner.Text(""),
                        inner.Text("This nested app has its own:"),
                        inner.Text("  • Render loop"),
                        inner.Text("  • Virtual terminal emulator"),
                        inner.Text("  • Screen buffer"),
                        inner.Text(""),
                        inner.Text($"  {spinnerFrame} Activity counter: {nestedCounter}"),
                        inner.Text($"  ⏱ Uptime: {elapsed:mm\\:ss\\.ff}"),
                        inner.Text(""),
                        inner.Text("The outer app displays this nested terminal"),
                        inner.Text("using a TerminalWidget on the right side!"),
                    ]),
                    title: "Nested App (Live)"
                ).Fill(),
            ]);
        },
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
    
    // Start a background task to increment the nested counter for ongoing activity
    var activityCts = new CancellationTokenSource();
    var activityTask = Task.Run(async () =>
    {
        while (!activityCts.Token.IsCancellationRequested)
        {
            nestedCounter++;
            nestedApp.Invalidate();
            await Task.Delay(100, activityCts.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    });

    // Give the nested app a moment to render
    await Task.Delay(500);

    // ===== Create the outer app that displays the nested terminal =====
    
    var outerCounter = 0;
    
    await using var outerApp = new Hex1bApp(
        ctx => ctx.VStack(root => [
            root.Splitter(
                // Left side: Outer app controls
                root.Border(
                    root.VStack(outer => [
                        outer.Text("╔════════════════════════════════════╗"),
                        outer.Text("║   OUTER APP                        ║"),
                        outer.Text("║   Hosting a nested terminal        ║"),
                        outer.Text("╚════════════════════════════════════╝"),
                        outer.Text(""),
                        outer.Text("This is the outer Hex1bApp."),
                        outer.Text(""),
                        outer.Text("The panel on the RIGHT contains"),
                        outer.Text("a fully independent Hex1bApp"),
                        outer.Text("running with its own render loop."),
                        outer.Text(""),
                        outer.Text("Notice the spinner and counter"),
                        outer.Text("updating in the nested terminal!"),
                        outer.Text(""),
                        outer.Text("─────────────────────────────────────"),
                        outer.Text(""),
                        outer.HStack(h => [
                            h.Button("Increment").OnClick(_ => outerCounter++),
                            h.Text($"  Outer counter: {outerCounter}"),
                        ]),
                        outer.Text(""),
                        outer.Text("Use Tab to navigate, Enter to click."),
                    ]),
                    title: "Outer App Controls"
                ).Fill(),
                
                // Right side: Nested terminal
                root.Border(
                    root.Terminal(nestedPresentation),
                    title: "Embedded Terminal →"
                ).Fill()
            ).Fill(),
            
            root.InfoBar([
                "Drag Splitter", "Resize",
                "Tab", "Navigate", 
                "Enter", "Activate Button", 
                "Ctrl+C", "Exit"
            ]),
        ]),
        new Hex1bAppOptions
        {
            WorkloadAdapter = outerWorkload,
            EnableMouse = true
        }
    );
    
    // Subscribe to nested terminal output to invalidate the outer app
    // This wakes up the outer app's render loop when the nested terminal has new content
    nestedPresentation.OutputReceived += () => outerApp.Invalidate();

    await outerApp.RunAsync();
    
    // Clean up
    activityCts.Cancel();
    await activityTask;
    await nestedApp.DisposeAsync();
    nestedTerminal.Dispose();
    await nestedPresentation.DisposeAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey(true);
}

Console.WriteLine("Done!");
