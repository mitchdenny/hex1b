using Hex1b;
using Hex1b.Automation;

namespace Hex1bInsideSpectreTui;

/// <summary>
/// Drives the embed demo end-to-end via <see cref="Hex1bTerminalAutomator"/>.
/// Sends arrow keys and +/- through Spectre.Tui's input loop into
/// <see cref="EmbedScreen.OnMessage"/>, which mutates the persisted
/// <see cref="Globe"/> state so the embedded Hex1b SurfaceWidget renders
/// new globe frames every Spectre.Tui redraw.
/// </summary>
internal static class EmbedAutomator
{
    public static async Task<int> RunAsync(Hex1bTerminal terminal)
    {
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(20));

        // Wait for the Spectre.Tui chrome and the embedded globe panel.
        await auto.WaitUntilTextAsync("embedded GlobeDemo");
        await auto.WaitUntilTextAsync("GlobeDemo embed");

        // Let the globe spin on its own first so the recording captures the
        // auto-rotation and cloud drift.
        await auto.WaitAsync(1500);

        // Spin right.
        for (var i = 0; i < 6; i++)
        {
            await auto.RightAsync();
            await auto.WaitAsync(140);
        }

        // Spin down (pitch).
        for (var i = 0; i < 3; i++)
        {
            await auto.DownAsync();
            await auto.WaitAsync(140);
        }

        // Zoom in (Spectre.Tui surfaces both Add and OemPlus depending on layout).
        for (var i = 0; i < 3; i++)
        {
            await auto.TypeAsync("+");
            await auto.WaitAsync(180);
        }
        await auto.WaitAsync(800);

        // Zoom back out.
        for (var i = 0; i < 4; i++)
        {
            await auto.TypeAsync("-");
            await auto.WaitAsync(180);
        }

        // Spin left to demonstrate rotation in the opposite direction.
        for (var i = 0; i < 6; i++)
        {
            await auto.LeftAsync();
            await auto.WaitAsync(140);
        }

        // Linger so the auto-rotate + cloud drift are visible.
        await auto.WaitAsync(1500);

        // Quit via the screen-level Q binding.
        await auto.TypeAsync("q");

        return 0;
    }
}

