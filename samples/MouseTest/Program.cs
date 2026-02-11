using Hex1b;
using Hex1b.Widgets;

// Comprehensive test for mouse and keyboard support with the new ConsolePresentationAdapter
// Run with: dotnet run --project samples/MouseTest

Console.WriteLine("Mouse & Keyboard Test - Raw Console Mode");
Console.WriteLine("=========================================");
Console.WriteLine("Press any key to start...");
Console.ReadKey(true);

// Scenario selection
var scenarios = new List<string>
{
    "Buttons",
    "Counter",
    "TextBox",
    "Toggle",
    "Tabs",
    "List"
};
var selectedScenario = 0;

// State for various controls
var clickCount = 0;
var button1Clicks = 0;
var button2Clicks = 0;
var button3Clicks = 0;
var textValue = "Type here...";
var counter = 0;
var selectedTab = 0;
var toggleOptions = new[] { "Off", "On" };
var toggleSelectedIndex = 0;
var listItems = new List<string> { "Apple", "Banana", "Cherry", "Date", "Elderberry" };
var selectedItem = "Apple";
var lastAction = "None";

// Helper to build the right panel based on selected scenario
Hex1bWidget BuildScenarioPanel(WidgetContext<VStackWidget> v)
{
    return selectedScenario switch
    {
        0 => v.VStack(p => [
            p.Text("── Buttons ──────────────────────────────────────"),
            p.Text(""),
            p.Text("Click the buttons below:"),
            p.Text(""),
            p.HStack(h => [
                h.Button($"Button 1 ({button1Clicks})").OnClick(_ => { button1Clicks++; clickCount++; }),
                h.Text("  "),
                h.Button($"Button 2 ({button2Clicks})").OnClick(_ => { button2Clicks++; clickCount++; }),
                h.Text("  "),
                h.Button($"Button 3 ({button3Clicks})").OnClick(_ => { button3Clicks++; clickCount++; }),
            ]),
            p.Text(""),
            p.Text($"Total button clicks: {button1Clicks + button2Clicks + button3Clicks}"),
        ]),

        1 => v.VStack(p => [
            p.Text("── Counter ─────────────────────────────────────"),
            p.Text(""),
            p.Text("Use +/- buttons or click Reset:"),
            p.Text(""),
            p.HStack(h => [
                h.Button("  -  ").OnClick(_ => { counter--; clickCount++; }),
                h.Text($"    {counter,5}    "),
                h.Button("  +  ").OnClick(_ => { counter++; clickCount++; }),
            ]),
            p.Text(""),
            p.Button("   Reset to Zero   ").OnClick(_ => { counter = 0; clickCount++; }),
        ]),

        2 => v.VStack(p => [
            p.Text("── TextBox ─────────────────────────────────────"),
            p.Text(""),
            p.Text("Click to focus, then type:"),
            p.Text(""),
            p.TextBox(textValue).OnTextChanged(e => textValue = e.NewText),
            p.Text(""),
            p.Text($"Length: {textValue.Length} characters"),
            p.Text($"Content: \"{textValue}\""),
        ]),

        3 => v.VStack(p => [
            p.Text("── Toggle Switch ───────────────────────────────"),
            p.Text(""),
            p.Text("Click or use arrow keys to toggle:"),
            p.Text(""),
            p.HStack(h => [
                h.Text("Power: "),
                h.ToggleSwitch(toggleOptions, toggleSelectedIndex)
                    .OnSelectionChanged(e => toggleSelectedIndex = e.SelectedIndex),
            ]),
            p.Text(""),
            p.Text($"Current value: {toggleOptions[toggleSelectedIndex]}"),
            p.Text($"Selected index: {toggleSelectedIndex}"),
        ]),

        4 => v.VStack(p => [
            p.Text("── Tabs ────────────────────────────────────────"),
            p.Text(""),
            p.Text("Click a tab to select it:"),
            p.Text(""),
            p.HStack(h => [
                h.Button(selectedTab == 0 ? "[ Home ]" : "  Home  ").OnClick(_ => { selectedTab = 0; clickCount++; }),
                h.Text(" "),
                h.Button(selectedTab == 1 ? "[ Settings ]" : "  Settings  ").OnClick(_ => { selectedTab = 1; clickCount++; }),
                h.Text(" "),
                h.Button(selectedTab == 2 ? "[ About ]" : "  About  ").OnClick(_ => { selectedTab = 2; clickCount++; }),
            ]),
            p.Text(""),
            p.Text($"Active tab: {selectedTab switch { 0 => "Home", 1 => "Settings", 2 => "About", _ => "?" }}"),
            p.Text(""),
            p.Text(selectedTab switch
            {
                0 => "Welcome to the Home tab!",
                1 => "Configure your settings here.",
                2 => "MouseTest v1.0 - Testing Hex1b",
                _ => ""
            }),
        ]),

        5 => v.VStack(p => [
            p.Text("── List Selection ──────────────────────────────"),
            p.Text(""),
            p.Text("Click an item or use arrow keys:"),
            p.Text(""),
            p.List(listItems)
                .OnSelectionChanged(e => {
                    selectedItem = e.SelectedText;
                    lastAction = $"Selected: {e.SelectedText}";
                })
                .OnItemActivated(e => {
                    lastAction = $"Activated: {e.ActivatedText}";
                    clickCount++;
                }),
            p.Text(""),
            p.Text($"Selected: {selectedItem}"),
            p.Text($"Last action: {lastAction}"),
        ]),

        _ => v.Text("Select a scenario from the list")
    };
}

try
{
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) =>
            ctx => ctx.VStack(root => [
            // Main content wrapped in a border
            root.Border(
                // Splitter with scenario list on left, controls on right
                root.HSplitter(
                    // Left pane: scenario list
                    left => [
                        left.Text("Scenarios:"),
                        left.Text("──────────────"),
                        left.List(scenarios)
                            .OnSelectionChanged(e => selectedScenario = e.SelectedIndex)
                            .OnItemActivated(e => {
                                selectedScenario = e.ActivatedIndex;
                                clickCount++;
                            }),
                        left.Text(""),
                        left.Text($"Clicks: {clickCount}"),
                    ],
                    // Right pane: selected scenario's controls
                    right => [
                        BuildScenarioPanel(right),
                    ],
                    leftWidth: 20
                )
            ).Title("Mouse & Keyboard Test").Fill(),
            
            // InfoBar at the bottom with instructions
            root.InfoBar([
                "Tab/Arrows", "Navigate", 
                "Enter/Click", "Activate", 
                "Ctrl+C", "Exit"
            ]),
        ]))
        .WithMouse()
        .Build();

    await terminal.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey(true);
}

Console.WriteLine("Done!");
