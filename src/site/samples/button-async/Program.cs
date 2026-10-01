using Hex1b;

var state = new LoaderState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) =>
    {
        state.App = app;
        return ctx => ctx.Border(b => [
            b.VStack(v => [
                v.Text("Async Background Work Demo"),
                v.Text(""),
                v.Text($"Status: {state.Status}"),
                v.Progress(state.Progress),
                v.Text(""),
                state.Result != null 
                    ? v.Text($"Result: {state.Result}") 
                    : v.Text(""),
                v.Text(""),
                v.Button(state.IsLoading ? "Loading..." : "Load Data")
                    .OnClick(_ => state.StartLoading())
            ])
        ]).Title("Background Work");
    })
    .Build();

await terminal.RunAsync();

class LoaderState
{
    public Hex1bApp? App { get; set; }
    public string Status { get; private set; } = "Ready";
    public int Progress { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Result { get; private set; }

    public void StartLoading()
    {
        if (IsLoading || App is null) return;
        
        IsLoading = true;
        Status = "Starting...";
        Progress = 0;
        Result = null;
        
        // Trigger background work - not awaited!
        _ = DoBackgroundWorkAsync();
    }

    private async Task DoBackgroundWorkAsync()
    {
        var steps = new[] { "Connecting...", "Fetching data...", "Processing...", "Finalizing..." };

        for (int i = 0; i < steps.Length; i++)
        {
            Status = steps[i];
            Progress = (i + 1) * 25;
            App?.Invalidate(); // Tell app to re-render
            
            await Task.Delay(600); // Simulate work
        }

        Status = "Complete!";
        Progress = 100;
        Result = "Successfully loaded 42 items";
        IsLoading = false;
        App?.Invalidate();
    }
}
