using Hex1b;
using Hex1b.Automation;

namespace Hex1bInsideSpectreTui;

/// <summary>
/// Drives the embed demo end-to-end via <see cref="Hex1bTerminalAutomator"/>.
/// Walks the embedded Hex1b list with arrow keys and PgUp/PgDn to prove that
/// (a) keys flow through the Spectre.Tui input loop into the embedded host's
/// <see cref="Hex1b.Input.InputRouter"/>, and (b) selection state survives
/// every Spectre.Tui frame's reconcile pass.
/// </summary>
internal static class EmbedAutomator
{
    public static async Task<int> RunAsync(Hex1bTerminal terminal)
    {
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(20));

        // Wait until both the Spectre.Tui chrome and the embedded Hex1b
        // ListWidget have rendered their first frame.
        await auto.WaitUntilTextAsync("embed showcase");
        await auto.WaitUntilTextAsync(EmbedScreen.ListItems[0]);
        await auto.WaitAsync(800);

        // Walk the list with Down. Each step is forwarded through Spectre.Tui's
        // OnMessage to the embedded Hex1b widget's input router, which moves
        // the ListNode's selection.
        for (var i = 0; i < 4; i++)
        {
            await auto.DownAsync();
            await auto.WaitAsync(180);
        }

        // Page down once to demonstrate larger jumps still flow through.
        await auto.PageDownAsync();
        await auto.WaitAsync(500);

        // Walk back up.
        for (var i = 0; i < 3; i++)
        {
            await auto.UpAsync();
            await auto.WaitAsync(160);
        }

        // Page back up to the top.
        await auto.PageUpAsync();
        await auto.WaitAsync(500);

        // Linger so the sparkline animation in the Spectre.Tui chrome
        // accumulates visibly in the recording (proves Spectre.Tui's render
        // loop is still alive while the embedded Hex1b panel is hosted).
        await auto.WaitAsync(1500);

        // Quit via the screen-level Q binding.
        await auto.TypeAsync("q");

        return 0;
    }
}
