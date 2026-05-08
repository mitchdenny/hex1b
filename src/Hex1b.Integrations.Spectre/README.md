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

## What's not bridged

- **Mouse input.** Neither Spectre.Console nor Spectre.Tui have a mouse
  model; mouse events surfaced by Hex1b are dropped silently.
- **Bracketed paste / focus events.** They flow through the ANSI parser
  for recording and presentation, but Spectre's input reader doesn't see
  them — same behaviour you'd get running Spectre directly.
