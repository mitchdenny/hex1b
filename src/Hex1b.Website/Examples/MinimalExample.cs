using Hex1b;
using Hex1b.Terminal;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// The minimal "Hello World" example for Hex1b.
/// Shows a complete, minimal app with Hex1bApp instantiation.
/// This is the first example on the homepage.
/// </summary>
public class MinimalExample(ILogger<MinimalExample> logger) : ReactiveExample
{
    private readonly ILogger<MinimalExample> _logger = logger;

    public override string Id => "minimal";
    public override string Title => "Hello World";
    public override string Description => "The most basic Hex1b application - a complete, runnable example.";

    public override async Task RunAsync(IHex1bAppTerminalWorkloadAdapter workloadAdapter, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting minimal example");

        // --- BEGIN: Code shown on homepage ---
        var clickCount = 0;

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Hello, Hex1b!"),
                v.Text($"Clicks: {clickCount}"),
                v.Button("Click me").OnClick(_ => clickCount++)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workloadAdapter }
        );

        await app.RunAsync(cancellationToken);
        // --- END: Code shown on homepage ---
    }
}
