using Hex1b;
using Hex1b.Input;
using Hex1b.Terminal;
using Hex1b.Widgets;

// Terminal Heatmap Demo - Shows which parts of the UI are being redrawn
// Press 'H' to toggle heatmap on/off

Console.WriteLine("Terminal Heatmap Demo");
Console.WriteLine("=====================");
Console.WriteLine("This demo shows a heatmap of terminal cell updates.");
Console.WriteLine("Cells that update more frequently appear 'hotter' (red/orange).");
Console.WriteLine("Press 'H' to toggle heatmap on/off.");
Console.WriteLine();
Console.WriteLine("Press any key to start...");
Console.ReadKey(true);

// State for the demo
var counter = 0;
var time = DateTimeOffset.UtcNow;
var showClock = true;
var showCounter = true;
var showSpinner = true;
var spinnerIndex = 0;
var spinnerChars = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
var lastMessage = "Press 'H' to toggle heatmap";

try
{
    // Create the presentation adapter for console I/O
    var console = new ConsolePresentationAdapter();
    
    // Wrap it with the heatmap filter
    var heatmap = new TerminalHeatmapFilter(console);
    
    // Create the workload adapter that Hex1bApp will use
    var workload = new Hex1bAppWorkloadAdapter(heatmap.Capabilities);
    
    // Create the terminal that bridges presentation ↔ workload
    using var terminal = new Hex1bTerminal(heatmap, workload);
    
    // Attach the terminal to the heatmap filter so it can access the screen buffer
    heatmap.AttachTerminal(terminal);

    await using var app = new Hex1bApp(
        ctx => ctx.VStack(root => [
            // Main content in a border
            root.Border(
                root.VStack(main => [
                    main.Text("Terminal Heatmap Visualization Demo"),
                    main.Text(""),
                    
                    // Clock section (updates frequently)
                    showClock ? main.VStack(clock => [
                        clock.Text("── Real-time Clock (updates every frame) ──"),
                        clock.Text($"Time: {time:HH:mm:ss.fff}"),
                        clock.Text(""),
                    ]) : main.Text(""),
                    
                    // Counter section (updates on button click)
                    showCounter ? main.VStack(cnt => [
                        cnt.Text("── Counter (updates on click) ──"),
                        cnt.HStack(h => [
                            h.Button("  -  ").OnClick(_ => counter--),
                            h.Text($"  {counter,5}  "),
                            h.Button("  +  ").OnClick(_ => counter++),
                        ]),
                        cnt.Text(""),
                    ]) : main.Text(""),
                    
                    // Spinner section (animates)
                    showSpinner ? main.VStack(spin => [
                        spin.Text("── Spinner (animates) ──"),
                        spin.Text($"Loading... {spinnerChars[spinnerIndex]}"),
                        spin.Text(""),
                    ]) : main.Text(""),
                    
                    // Toggles
                    main.Text("── Display Options ──"),
                    main.HStack(h => [
                        h.Button(showClock ? "[✓] Clock" : "[ ] Clock")
                            .OnClick(_ => showClock = !showClock),
                        h.Text("  "),
                        h.Button(showCounter ? "[✓] Counter" : "[ ] Counter")
                            .OnClick(_ => showCounter = !showCounter),
                        h.Text("  "),
                        h.Button(showSpinner ? "[✓] Spinner" : "[ ] Spinner")
                            .OnClick(_ => showSpinner = !showSpinner),
                    ]),
                    main.Text(""),
                    
                    // Instructions
                    main.Text("Press 'H' to toggle heatmap visualization"),
                    main.Text(lastMessage),
                ]),
                title: "Heatmap Demo"
            ).Fill(),
            
            // Info bar at the bottom
            root.InfoBar([
                "H", "Toggle Heatmap",
                "Tab/Arrows", "Navigate",
                "Enter", "Activate",
                "Ctrl+C", "Exit"
            ]),
        ]).WithInputBindings(bindings =>
        {
            // Add global keyboard binding for 'H' to toggle heatmap
            bindings.Key(Hex1bKey.H).Action(() =>
            {
                if (heatmap.IsEnabled)
                {
                    heatmap.Disable();
                    lastMessage = "Heatmap disabled - showing normal view";
                }
                else
                {
                    heatmap.Enable();
                    lastMessage = "Heatmap enabled - showing update frequency";
                }
            });
        }),
        new Hex1bAppOptions
        {
            WorkloadAdapter = workload
        }
    );

    // Start a background task to update the clock and spinner
    using var cts = new CancellationTokenSource();
    var updateTask = Task.Run(async () =>
    {
        while (!cts.Token.IsCancellationRequested)
        {
            time = DateTimeOffset.UtcNow;
            spinnerIndex = (spinnerIndex + 1) % spinnerChars.Length;
            await Task.Delay(50, cts.Token); // Update 20 times per second
        }
    });

    try
    {
        await app.RunAsync();
    }
    finally
    {
        await cts.CancelAsync();
        await updateTask;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey(true);
}

Console.WriteLine("Done!");
