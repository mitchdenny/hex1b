using System.CommandLine;
using Hex1b;
using Hex1b.Integrations.Spectre.SpectreTui;
using SpectreTuiDemo;

// SpectreTuiDemo - A widget showcase for Spectre.Tui running inside a Hex1b
// terminal. Renders a TabsWidget across the top with five tabs that exercise
// the major Spectre.Tui widgets (Paragraph, BoxWidget, ListWidget,
// TableWidget, ScrollViewWidget, SparklineWidget, ProgressBarWidget,
// SpinnerWidget, PopupWidget, Layout) all wired through the Hex1b workload
// pipeline so the session can be recorded, embedded, or muxed.
//
//   --auto       Drive the demo end-to-end via Hex1bTerminalAutomator
//                instead of waiting for keyboard input. Visible by default.
//   --headless   Render to an off-screen buffer and write an asciinema cast
//                to the binary directory. Combine with --auto in CI to
//                produce a reproducible recording without any UI.

var autoOption = new Option<bool>("--auto")
{
    Description = "Drive the demo end-to-end via Hex1bTerminalAutomator.",
};

var headlessOption = new Option<bool>("--headless")
{
    Description = "Render to an off-screen 120x40 buffer and write an asciinema cast.",
};

var root = new RootCommand("Hex1b x Spectre.Tui widget showcase.");
root.Options.Add(autoOption);
root.Options.Add(headlessOption);

root.SetAction((parseResult, cancellationToken) => RunDemoAsync(
    auto: parseResult.GetValue(autoOption),
    headless: parseResult.GetValue(headlessOption),
    ct: cancellationToken));

return await root.Parse(args).InvokeAsync();

static async Task<int> RunDemoAsync(bool auto, bool headless, CancellationToken ct)
{
    var castPath = Path.Combine(AppContext.BaseDirectory, "spectre-tui-demo.cast");

    var builder = Hex1bTerminal.CreateBuilder()
        .WithSpectreTuiApp(new MainScreen());

    if (headless)
    {
        // Headless + fixed dimensions + asciinema so the recorded cast is
        // deterministic and reproducible across machines.
        builder = builder
            .WithHeadless()
            .WithDimensions(120, 40)
            .WithAsciinemaRecording(castPath);
    }

    await using var terminal = builder.Build();

    Task<int>? autoTask = null;
    if (auto)
    {
        autoTask = Task.Run(() => DemoAutomator.RunAsync(terminal), ct);
    }

    var exit = await terminal.RunAsync();

    if (autoTask is not null)
    {
        try
        {
            await autoTask;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Automator failed: {ex}");
            if (exit == 0)
            {
                exit = 1;
            }
        }
    }

    if (headless)
    {
        Console.WriteLine();
        Console.WriteLine($"Recording saved to {castPath}");
    }

    return exit;
}
