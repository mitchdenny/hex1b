using Hex1b;
using Hex1b.Automation;

namespace SpectreConsoleDemo;

/// <summary>
/// Drives <see cref="InteractiveDemo"/> end-to-end via
/// <see cref="Hex1bTerminalAutomator"/>. Walks every menu entry in
/// <see cref="InteractiveDemo.MenuItems"/>, types into the prompts demo, then
/// selects Quit and waits for the goodbye line.
/// </summary>
internal static class DemoAutomator
{
    public static async Task<int> RunAsync(Hex1bTerminal terminal)
    {
        // Use a comfortable timeout so the long-running spinners / live-display
        // demos have room to finish before the automator times out waiting for
        // their completion text.
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(20));

        // Wait for the menu to appear before doing anything else.
        await auto.WaitUntilTextAsync("Pick a demo");

        // Walk every menu entry except the trailing "Quit".
        for (var i = 0; i < InteractiveDemo.MenuItems.Count - 1; i++)
        {
            var item = InteractiveDemo.MenuItems[i];

            // SelectionPrompt always re-renders with the first item highlighted,
            // so press Down i times to land on the i-th entry.
            for (var d = 0; d < i; d++)
            {
                await auto.DownAsync();
                await auto.WaitAsync(20);
            }
            await auto.EnterAsync();

            // Each demo prints a "<choice>" rule line before its body. Wait
            // for that as a stable sync point.
            await auto.WaitUntilTextAsync(item);

            // Demo-specific extra interactions.
            switch (item)
            {
                case "Prompts":
                    await DrivePromptsAsync(auto);
                    break;
            }

            // Every demo ends with the "Press Enter to return" prompt. Wait
            // for it, then dismiss it to bounce back to the main menu.
            await auto.WaitUntilTextAsync("Press");
            await auto.WaitAsync(150);
            await auto.EnterAsync();

            await auto.WaitUntilTextAsync("Pick a demo");
        }

        // Finally select "Quit" — last item in the menu, so press Down
        // (count - 1) times then Enter.
        for (var d = 0; d < InteractiveDemo.MenuItems.Count - 1; d++)
        {
            await auto.DownAsync();
            await auto.WaitAsync(20);
        }
        await auto.EnterAsync();

        await auto.WaitUntilTextAsync("Goodbye");

        return 0;
    }

    private static async Task DrivePromptsAsync(Hex1bTerminalAutomator auto)
    {
        // Ask<string>("What's your name?")
        await auto.WaitUntilTextAsync("name");
        await auto.WaitAsync(150);
        await auto.TypeAsync("Hex1b");
        await auto.EnterAsync();

        // ConfirmAsync("Ready to keep going?")
        await auto.WaitUntilTextAsync("keep going");
        await auto.WaitAsync(150);
        await auto.TypeAsync("y");
        await auto.EnterAsync();

        // Ask<int>("Pick a number between 1 and 10")
        await auto.WaitUntilTextAsync("Pick a number");
        await auto.WaitAsync(150);
        await auto.TypeAsync("7");
        await auto.EnterAsync();

        // MultiSelectionPrompt<string>("Pick your favourite features")
        await auto.WaitUntilTextAsync("favourite");
        await auto.WaitAsync(200);
        await auto.SpaceAsync();   // toggle first
        await auto.WaitAsync(50);
        await auto.DownAsync();
        await auto.WaitAsync(50);
        await auto.SpaceAsync();   // toggle second
        await auto.WaitAsync(50);
        await auto.EnterAsync();   // confirm

        // Wait for the summary line that the demo prints after the prompts.
        await auto.WaitUntilTextAsync("All prompts complete");
    }
}
