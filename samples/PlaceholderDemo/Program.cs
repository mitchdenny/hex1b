using Hex1b;
using Hex1b.Widgets;

// Demonstrates WithPlaceholderWorkload: the terminal runs a placeholder
// Hex1bApp ("PTY not connected — Q to quit") until an HMP1 producer appears
// at the configured UDS path, then swaps to the live producer. When the
// producer goes away the placeholder comes back.
//
// Usage:
//   1. In one terminal, start a producer:
//        dotnet run --project samples/MuxerDemo -- --server /tmp/hex1b-placeholder.sock
//   2. In another terminal, start this client (any order works — the client
//      will wait for the socket to appear):
//        dotnet run --project samples/PlaceholderDemo

var socketPath = args.Length > 0
    ? args[0]
    : Path.Combine(Path.GetTempPath(), "hex1b-placeholder.sock");

using var lifetime = new CancellationTokenSource();
var status = "Waiting for producer at " + socketPath;
Hex1bApp? placeholderApp = null;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHmp1Client(
        Hmp1Transports.RetryingUnixSocket(socketPath, new RetryPolicy
        {
            InitialDelay = TimeSpan.FromMilliseconds(200),
            MaxDelay = TimeSpan.FromSeconds(2),
            Multiplier = 1.5,
            OnAttemptFailed = e =>
            {
                status = $"Attempt {e.AttemptNumber} failed: {e.Error.GetType().Name}. Retrying in {e.NextDelay.TotalMilliseconds:0} ms…";
                placeholderApp?.Invalidate();
            },
        }),
        opts =>
        {
            opts.DisplayName = "placeholder-demo";
            opts.DefaultRole = Hmp1Role.Secondary;
        })
    .WithPlaceholderHex1bApp((app, _) =>
    {
        placeholderApp = app;
        return ctx =>
            ctx.Center(
                ctx.VStack(v =>
                [
                    v.Text("PTY not connected"),
                    v.Text(""),
                    v.Text(status),
                    v.Text(""),
                    v.Text("Press Q to quit"),
                ]))
            .Fill()
            .InputBindings(b => b.Key(Hex1b.Input.Hex1bKey.Q).Action(_ =>
            {
                app.RequestStop();
                lifetime.Cancel();
            }, "Quit"));
    })
    .Build();

await terminal.RunAsync(lifetime.Token);
