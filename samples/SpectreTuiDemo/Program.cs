using Hex1b;
using Hex1b.Integrations.Spectre.SpectreTui;
using Spectre.Console;
using Spectre.Tui;
using Spectre.Tui.App;

// SpectreTuiDemo - Demonstrates running a Spectre.Tui application inside a
// Hex1b terminal. The Spectre.Tui Application owns the render loop, screen
// stack, and input pump; Hex1b owns the underlying ANSI plumbing so the
// session can be recorded to an asciinema cast.

var castPath = Path.Combine(AppContext.BaseDirectory, "spectre-tui-demo.cast");

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithAsciinemaRecording(castPath)
    .WithSpectreTuiApp(new MainScreen())
    .Build();

return await terminal.RunAsync();

internal sealed class MainScreen : Screen
{
    private int _frames;

    public override void OnMessage(ApplicationContext context, ApplicationMessage message)
    {
        if (message is KeyMessage key)
        {
            if (key.Info.Key == ConsoleKey.Q || key.Info.Key == ConsoleKey.Escape)
            {
                context.Quit();
            }
            else if (key.Info.Key == ConsoleKey.Spacebar)
            {
                context.Push(new PopUpScreen());
            }
        }
    }

    public override void Render(RenderContext context)
    {
        _frames++;
        context.SetString(2, 1, "Hex1b ⨯ Spectre.Tui", new Style(Color.Aqua, decoration: Decoration.Bold));
        context.SetString(2, 3, $"Frame: {_frames}", new Style(Color.Grey));
        context.SetString(2, 5, "Press SPACE for a popup", new Style(Color.Yellow));
        context.SetString(2, 6, "Press Q or Esc to quit", new Style(Color.Yellow));
    }
}

internal sealed class PopUpScreen : Screen
{
    public override bool IsTransparent => true;

    public override void OnMessage(ApplicationContext context, ApplicationMessage message)
    {
        if (message is KeyMessage key && key.Info.Key == ConsoleKey.Escape)
        {
            context.Pop();
        }
    }

    public override void Render(RenderContext context)
    {
        var width = Math.Min(40, context.Viewport.Width - 4);
        var height = 5;
        var x = (context.Viewport.Width - width) / 2;
        var y = (context.Viewport.Height - height) / 2;

        for (var dy = 0; dy < height; dy++)
        {
            for (var dx = 0; dx < width; dx++)
            {
                var ch = (dy == 0 || dy == height - 1)
                    ? '─'
                    : (dx == 0 || dx == width - 1) ? '│' : ' ';
                context.SetSymbol(x + dx, y + dy, ch);
            }
        }

        // Corners
        context.SetSymbol(x, y, '╭');
        context.SetSymbol(x + width - 1, y, '╮');
        context.SetSymbol(x, y + height - 1, '╰');
        context.SetSymbol(x + width - 1, y + height - 1, '╯');

        context.SetString(x + 2, y + 2, "Press ESC to close", new Style(Color.Yellow));
    }
}
