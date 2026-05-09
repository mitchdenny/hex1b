using System.CommandLine;
using Hex1b;
using Hex1b.Integrations.Spectre.SpectreTui;
using Hex1bInsideSpectreTui;

// Hex1bInsideSpectreTui — the inverse of SpectreTuiDemo. Where SpectreTuiDemo
// shows a Spectre.Tui app running *as* a Hex1b workload, this demo embeds a
// Hex1b widget tree *inside* a Spectre.Tui screen via Hex1bSpectreTuiWidget.
// The Spectre.Tui screen renders chrome (title, side panel, status bar) using
// its native widgets, and the centre region is a Hex1b widget tree that
// preserves its own focus/cursor/list-selection state across every Spectre.Tui
// frame.
//
//   --auto       Drive the demo via Hex1bTerminalAutomator. Visible by default.
//   --headless   Render to an off-screen 120x40 buffer and write an asciinema
//                cast to the binary directory. Combine with --auto for a
//                reproducible recording.

var autoOption = new Option<bool>("--auto")
{
    Description = "Drive the demo end-to-end via Hex1bTerminalAutomator.",
};

var headlessOption = new Option<bool>("--headless")
{
    Description = "Render to an off-screen 120x40 buffer and write an asciinema cast.",
};

var root = new RootCommand("Hex1b widget embedded inside a Spectre.Tui screen.");
root.Options.Add(autoOption);
root.Options.Add(headlessOption);

root.SetAction((parseResult, cancellationToken) => RunDemoAsync(
    auto: parseResult.GetValue(autoOption),
    headless: parseResult.GetValue(headlessOption),
    ct: cancellationToken));

return await root.Parse(args).InvokeAsync();

static async Task<int> RunDemoAsync(bool auto, bool headless, CancellationToken ct)
{
    var castPath = Path.Combine(AppContext.BaseDirectory, "hex1b-inside-spectre-tui.cast");

    var screen = new EmbedScreen();

    var builder = Hex1bTerminal.CreateBuilder()
        .WithSpectreTuiApp(screen);

    if (headless)
    {
        builder = builder
            .WithHeadless()
            .WithDimensions(120, 40)
            .WithAsciinemaRecording(castPath);
    }

    await using var terminal = builder.Build();

    Task<int>? autoTask = null;
    if (auto)
    {
        autoTask = Task.Run(() => EmbedAutomator.RunAsync(terminal), ct);
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
        Console.Error.WriteLine($"Recorded asciinema cast: {castPath}");
    }

    return exit;
}
