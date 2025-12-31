using Hex1b;
using Hex1b.Terminal;
using Hex1b.Widgets;

// State for the demo
var shouldThrowOnBuild = false;
var throwCount = 0;
var lastAction = "Ready";

var presentation = new ConsolePresentationAdapter(enableMouse: true);
var workload = new Hex1bAppWorkloadAdapter(presentation.Capabilities);

var terminalOptions = new Hex1bTerminalOptions
{
    PresentationAdapter = presentation,
    WorkloadAdapter = workload
};
terminalOptions.AddHex1bAppRenderOptimization();

using var terminal = new Hex1bTerminal(terminalOptions);

using var app = new Hex1bApp((RootContext ctx) =>
    ctx.VStack(v => [
        v.Text("RescueWidget Demo"),
        v.Text("================"),
        v.Text(""),
        v.Text("This demo shows how RescueWidget catches errors and displays a fallback UI."),
        v.Text(""),
        
        // Rescue widget wrapping potentially throwing content
        v.Rescue(rescue => 
        {
            // This code runs during the Build phase
            if (shouldThrowOnBuild)
            {
                shouldThrowOnBuild = false; // Reset so we can retry
                throwCount++;
                throw new InvalidOperationException(
                    $"Simulated Build phase error #{throwCount}!\n\n" +
                    "This error was thrown during widget tree construction.\n" +
                    "The RescueWidget caught it and is displaying this fallback UI.\n\n" +
                    "Click 'Retry' to recover, or 'Copy Details' to copy the error to clipboard.");
            }
            
            // Normal content when no error
            return [
                rescue.Border(b => [
                    b.VStack(content => [
                        content.Text("Protected Content Area"),
                        content.Text(""),
                        content.Text("This content is protected by a RescueWidget."),
                        content.Text("Click the button below to trigger an error."),
                        content.Text(""),
                        content.HStack(h => [
                            h.Button("Trigger Error").OnClick(_ =>
                            {
                                shouldThrowOnBuild = true;
                                lastAction = "Error will be thrown on next build...";
                            })
                        ]),
                        content.Text(""),
                        content.Text($"Status: All good! (Errors caught so far: {throwCount})")
                    ])
                ], title: "Protected Zone")
            ];
        })
        .OnRescue(e => 
        {
            lastAction = $"Error caught in {e.Phase}: {e.Exception.GetType().Name}";
        })
        .OnReset(_ => 
        {
            shouldThrowOnBuild = false;
            lastAction = "Reset - ready to trigger new errors";
        }),
        
        v.Text(""),
        v.Text($"Last action: {lastAction}"),
        v.Text(""),
        v.Text("Press Ctrl+C to exit")
    ]),
    new Hex1bAppOptions
    {
        WorkloadAdapter = workload,
        EnableMouse = true
    }
);

await app.RunAsync();
