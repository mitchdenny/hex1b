using Hex1b;
using Hex1b.Terminal;
using Hex1b.Widgets;

// Comprehensive test for mouse and keyboard support with the new ConsolePresentationAdapter
// Run with: dotnet run --project samples/MouseTest

Console.WriteLine("Mouse & Keyboard Test - Raw Console Mode");
Console.WriteLine("=========================================");
Console.WriteLine("Press any key to start...");
Console.ReadKey(true);

// State for various controls
var clickCount = 0;
var button1Clicks = 0;
var button2Clicks = 0;
var button3Clicks = 0;
var textValue = "Type here...";
var counter = 0;
var selectedTab = 0;
var toggleState = new ToggleSwitchState { Options = ["Off", "On"], SelectedIndex = 0 };
var listItems = new List<string> { "Apple", "Banana", "Cherry", "Date", "Elderberry" };
var selectedItem = "Apple";
var lastAction = "None";

try
{
    var presentation = new ConsolePresentationAdapter(enableMouse: true);
    var terminal = new Hex1bTerminalCore(presentation);

    await using var app = new Hex1bApp(
        ctx => ctx.VStack(v => [
            // Title
            v.Text("══════════════════════════════════════════════════════════"),
            v.Text("         Mouse & Keyboard Test - Raw Console Mode         "),
            v.Text("══════════════════════════════════════════════════════════"),
            v.Text(""),
            
            // Click counter section
            v.Text($"Total Clicks: {clickCount}"),
            v.Text(""),
            
            // Row of buttons
            v.Text("── Buttons ──────────────────────────────────────────────"),
            v.HStack(h => [
                h.Button($"Button 1 ({button1Clicks})").OnClick(_ => { button1Clicks++; clickCount++; }),
                h.Text("  "),
                h.Button($"Button 2 ({button2Clicks})").OnClick(_ => { button2Clicks++; clickCount++; }),
                h.Text("  "),
                h.Button($"Button 3 ({button3Clicks})").OnClick(_ => { button3Clicks++; clickCount++; }),
            ]),
            v.Text(""),
            
            // Counter with increment/decrement
            v.Text("── Counter ──────────────────────────────────────────────"),
            v.HStack(h => [
                h.Button(" - ").OnClick(_ => counter--),
                h.Text($"  Counter: {counter}  "),
                h.Button(" + ").OnClick(_ => counter++),
                h.Text("  "),
                h.Button("Reset").OnClick(_ => counter = 0),
            ]),
            v.Text(""),
            
            // TextBox section
            v.Text("── TextBox ──────────────────────────────────────────────"),
            v.TextBox(textValue).OnTextChanged(e => textValue = e.NewText),
            v.Text($"   Length: {textValue.Length} characters"),
            v.Text(""),
            
            // Toggle switch
            v.Text("── Toggle Switch ────────────────────────────────────────"),
            v.HStack(h => [
                h.Text("Option: "),
                h.ToggleSwitch(toggleState),
                h.Text($"  (Selected: {toggleState.Options[toggleState.SelectedIndex]})"),
            ]),
            v.Text(""),
            
            // Tab-like buttons
            v.Text("── Tabs ─────────────────────────────────────────────────"),
            v.HStack(h => [
                h.Button(selectedTab == 0 ? "[ Tab 1 ]" : "  Tab 1  ").OnClick(_ => selectedTab = 0),
                h.Button(selectedTab == 1 ? "[ Tab 2 ]" : "  Tab 2  ").OnClick(_ => selectedTab = 1),
                h.Button(selectedTab == 2 ? "[ Tab 3 ]" : "  Tab 3  ").OnClick(_ => selectedTab = 2),
            ]),
            v.Text($"   Selected Tab: {selectedTab + 1}"),
            v.Text(""),
            
            // List selection
            v.Text("── List (click or use arrow keys) ───────────────────────"),
            v.List(listItems)
                .OnSelectionChanged(e => {
                    selectedItem = e.SelectedText;
                    lastAction = $"Selected: {e.SelectedText}";
                })
                .OnItemActivated(e => {
                    lastAction = $"Activated: {e.ActivatedText}";
                    clickCount++;
                }),
            v.Text($"   Selected: {selectedItem} | Last: {lastAction}"),
            v.Text(""),
            
            // Instructions
            v.Text("══════════════════════════════════════════════════════════"),
            v.Text("  Tab = Navigate | Enter/Click = Activate | Ctrl+C = Exit"),
            v.Text("══════════════════════════════════════════════════════════"),
        ]),
        new Hex1bAppOptions
        {
            WorkloadAdapter = terminal,
            EnableMouse = true
        }
    );

    await app.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey(true);
}

Console.WriteLine("Done!");
