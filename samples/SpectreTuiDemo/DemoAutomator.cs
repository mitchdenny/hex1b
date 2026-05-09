using Hex1b;
using Hex1b.Automation;

namespace SpectreTuiDemo;

/// <summary>
/// Drives <see cref="MainScreen"/> end-to-end via
/// <see cref="Hex1bTerminalAutomator"/>. Walks every tab in
/// <see cref="MainScreen.TabLabels"/>, exercises a per-tab interaction
/// (selection moves, scrolling, etc.), opens and closes the help popup,
/// then quits.
/// </summary>
internal static class DemoAutomator
{
    public static async Task<int> RunAsync(Hex1bTerminal terminal)
    {
        // Spectre.Tui renders at 60 fps, so input latency is essentially a
        // single frame. We give each Wait a generous timeout so flaky CI
        // doesn't trip the script.
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(20));

        // Wait for the welcome tab content to appear.
        await auto.WaitUntilTextAsync("widget showcase");

        // Tab 1 (Welcome) - already visible. Linger briefly so it's
        // recognisable in the recording.
        await auto.WaitAsync(600);

        // Tab 2 (List) - press Tab, then exercise the selection.
        await auto.TabAsync();
        await auto.WaitUntilTextAsync("To-do");
        await auto.WaitAsync(300);
        for (var i = 0; i < 4; i++)
        {
            await auto.DownAsync();
            await auto.WaitAsync(120);
        }
        await auto.SpaceAsync(); // toggle one item
        await auto.WaitAsync(300);

        // Tab 3 (Table) - press Tab, then move down a few rows.
        await auto.TabAsync();
        await auto.WaitUntilTextAsync("Largest cities");
        await auto.WaitAsync(300);
        for (var i = 0; i < 5; i++)
        {
            await auto.DownAsync();
            await auto.WaitAsync(100);
        }
        await auto.WaitAsync(300);

        // Tab 4 (Scroll) - press Tab, page down twice, then home.
        await auto.TabAsync();
        await auto.WaitUntilTextAsync("Scrollable text");
        await auto.WaitAsync(300);
        await auto.PageDownAsync();
        await auto.WaitAsync(400);
        await auto.PageDownAsync();
        await auto.WaitAsync(400);
        await auto.HomeAsync();
        await auto.WaitAsync(300);

        // Tab 5 (Sparkline) - press Tab, then linger so the streaming sparkline
        // accumulates visible data.
        await auto.TabAsync();
        await auto.WaitUntilTextAsync("Streaming samples");
        await auto.WaitAsync(1500);

        // Open the help popup with '?' and dismiss it with Escape.
        await auto.TypeAsync("?");
        await auto.WaitUntilTextAsync("this popup");
        await auto.WaitAsync(1000);
        await auto.EscapeAsync();
        await auto.WaitAsync(400);

        // Quit.
        await auto.TypeAsync("q");

        return 0;
    }
}
