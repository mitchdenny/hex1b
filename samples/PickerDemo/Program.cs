using Hex1b;
using Hex1b.Theming;

// Track the currently selected fruit for display
var selectedFruit = "Apple";
var selectedColor = "Blue";
var lastAction = "";

await Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
        ctx.VStack(v => [
            v.Text("Picker Demo - Select your favorite fruit:"),
            v.Text(""),
            v.HStack(h => [
                h.Text("Fruit: "),
                h.Picker(["Apple", "Banana", "Cherry", "Date", "Elderberry", "Fig", "Grape"])
                    .OnSelectionChanged(e =>
                    {
                        selectedFruit = e.SelectedText;
                        lastAction = $"Selected fruit: {e.SelectedText}";
                    })
            ]),
            v.Text(""),
            v.Text("--- Themed Picker (inside ThemePanel) ---"),
            v.ThemePanel(
                theme => theme
                    .Set(ButtonTheme.ForegroundColor, Hex1bColor.Yellow)
                    .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black)
                    .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Yellow)
                    .Set(ListTheme.SelectedForegroundColor, Hex1bColor.Black)
                    .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.Cyan)
                    .Set(BorderTheme.BorderColor, Hex1bColor.Magenta),
                t => [
                    t.HStack(h => [
                        h.Text("Color: "),
                        h.Picker(["Red", "Green", "Blue", "Yellow", "Cyan", "Magenta", "White"])
                            .OnSelectionChanged(e =>
                            {
                                selectedColor = e.SelectedText;
                                lastAction = $"Selected color: {e.SelectedText}";
                            })
                    ])
                ]
            ),
            v.Text(""),
            v.Text($"Currently selected fruit: {selectedFruit}"),
            v.Text($"Currently selected color: {selectedColor}"),
            v.Text($"Last action: {lastAction}"),
            v.Text(""),
            v.Text("Press Enter or click to open picker, then select an item"),
            v.Text("Press Ctrl+C to exit")
        ]))
    .WithMouse()
    .WithRenderOptimization()
    .RunAsync();
