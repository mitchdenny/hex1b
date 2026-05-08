# Hex1b.Integrations.Spectre

Run [Spectre.Console](https://spectreconsole.net/) (and, on `net10.0`,
[Spectre.Tui](https://github.com/patriksvensson/spectre.tui)) apps as
workloads inside a Hex1b virtual terminal.

Once attached, the Spectre app gets — for free — every workload-tier
capability Hex1b ships:

- asciinema-cast recording
- muxing and remote/web presentation
- embedding inside `TerminalWidget` for picture-in-picture style UIs
- the headless capture / automation harness used by Hex1b's own tests

You don't have to rewrite your UI on top of Hex1b widgets to get any of
this. The Spectre app keeps writing ANSI exactly as it does today; Hex1b
just becomes the terminal that interprets those bytes.

## Spectre.Console quickstart

```csharp
using Hex1b;
using Hex1b.Integrations.Spectre.SpectreConsole;
using Spectre.Console;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithAsciinemaRecording("./session.cast")
    .WithSpectreConsole(async (console, ct) =>
    {
        console.Write(new FigletText("Hex1b").Color(Color.Aqua));
        console.MarkupLine("Press [yellow]any key[/] to quit.");
        await console.Input.ReadKeyAsync(intercept: true, ct);
    })
    .Build();

await terminal.RunAsync();
```

## Spectre.Tui quickstart (net10.0 only)

```csharp
using Hex1b;
using Hex1b.Integrations.Spectre.SpectreTui;
using Spectre.Tui;
using Spectre.Tui.App;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithAsciinemaRecording("./session.cast")
    .WithSpectreTuiApp(new MainScreen())
    .Build();

return await terminal.RunAsync();

public sealed class MainScreen : Screen
{
    public override void OnMessage(ApplicationContext context, ApplicationMessage message)
    {
        if (message is KeyMessage { Info.Key: ConsoleKey.Q })
        {
            context.Quit();
        }
    }

    public override void Render(RenderContext context)
    {
        context.SetString(2, 1, "Hex1b ⨯ Spectre.Tui");
        context.SetString(2, 3, "Press Q to quit");
    }
}
```

For users not building on top of `Spectre.Tui.App`, drop down to the
`ITerminal` overload:

```csharp
.WithSpectreTuiTerminal(async (terminal, ct) =>
{
    var renderer = new Renderer(terminal);
    while (!ct.IsCancellationRequested)
    {
        renderer.Draw((ctx, _) => ctx.Render(myWidget));
        // your own input handling...
    }
})
```

Both `WithSpectreTuiApp` and `WithSpectreTuiTerminal` accept an optional
`ITerminalMode` argument. Pass `new InlineMode(height)` when you want
Spectre.Tui's output to land on the main scroll-back instead of the
alternate screen — handy for embedding inside scripted automation or
recording mixed inline + TUI sessions.

## Driving Spectre with Hex1b's automator

Because the bridge feeds Spectre's input reader from Hex1b's input
channel, anything that pushes events into that channel — including
`Hex1bTerminalAutomator` — can drive a Spectre app the same way it
drives a Hex1bApp. That makes the integration a viable target for the
existing automation/test harness without any extra plumbing.

The included `samples/SpectreConsoleDemo` ships in two modes:

```bash
# Interactive: a looping menu that walks through ten Spectre.Console
# controls — markup, tables, trees, calendar, charts, status spinner,
# live display, progress, prompts. Use the arrow keys + Enter to
# navigate.
dotnet run --project samples/SpectreConsoleDemo

# Self-driving: the same demo, but a background Hex1bTerminalAutomator
# walks every menu entry, types answers into the prompts, and selects
# Quit at the end. Output is captured to spectre-console-demo.cast in
# the build output directory. Headless terminal, deterministic timing.
dotnet run --project samples/SpectreConsoleDemo -- --auto
```

In `--auto` mode the program builds the terminal with `WithHeadless()`
and then spins up the automator on a background task in parallel with
`terminal.RunAsync()`:

```csharp
var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(20));
await auto.WaitUntilTextAsync("Pick a demo");

// Walk the SelectionPrompt — Down N times then Enter selects the Nth item.
for (var d = 0; d < itemIndex; d++) await auto.DownAsync();
await auto.EnterAsync();

// For the Prompts demo, type into Ask/Confirm/MultiSelectionPrompt directly.
await auto.WaitUntilTextAsync("name");
await auto.TypeAsync("Hex1b");
await auto.EnterAsync();
```

That's the same pattern Hex1b's own widget tests use — proof that the
bridge is a first-class workload as far as the rest of Hex1b is
concerned.

## What's not bridged

- **Mouse input.** Neither Spectre.Console nor Spectre.Tui have a mouse
  model; mouse events surfaced by Hex1b are dropped silently.
- **Bracketed paste / focus events.** They flow through the ANSI parser
  for recording and presentation, but Spectre's input reader doesn't see
  them — same behaviour you'd get running Spectre directly.
