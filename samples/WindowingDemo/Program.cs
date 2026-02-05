using Hex1b;
using Hex1b.Widgets;

// Demo state
var windowCounter = 0;
var lastAction = "None";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
        // Use WindowPanel to host floating windows
        ctx.WindowPanel(w =>
            w.VStack(main => [
                main.Text(""),
                main.Text("  ╔═══════════════════════════════════════╗"),
                main.Text("  ║     Floating Windows Demo             ║"),
                main.Text("  ╚═══════════════════════════════════════╝"),
                main.Text(""),
                main.Text($"  Last Action: {lastAction}"),
                main.Text(""),
                main.HStack(buttons => [
                    buttons.Text("  "),
                    buttons.Button("Open Window").OnClick(e =>
                    {
                        windowCounter++;
                        var windowId = $"window-{windowCounter}";
                        var windowNum = windowCounter;

                        // Cycle through different positions for each window
                        var positions = new[] {
                            WindowPositionSpec.Center,
                            WindowPositionSpec.TopLeft,
                            WindowPositionSpec.TopRight,
                            WindowPositionSpec.BottomLeft,
                            WindowPositionSpec.BottomRight
                        };
                        var positionSpec = positions[(windowCounter - 1) % positions.Length];

                        e.Windows.Open(
                            id: windowId,
                            title: $"Window {windowNum}",
                            content: () => BuildWindowContent(windowNum, windowId),
                            width: 40,
                            height: 12,
                            position: positionSpec
                        );

                        lastAction = $"Opened Window {windowNum}";
                    }),
                    buttons.Text(" "),
                    buttons.Button("Open Modal").OnClick(e =>
                    {
                        windowCounter++;
                        var windowId = $"modal-{windowCounter}";
                        var windowNum = windowCounter;

                        e.Windows.Open(
                            id: windowId,
                            title: $"Modal Dialog {windowNum}",
                            content: () => BuildModalContent(windowId),
                            width: 50,
                            height: 10,
                            isModal: true
                        );

                        lastAction = $"Opened Modal {windowNum}";
                    }),
                    buttons.Text(" "),
                    buttons.Button("Close All").OnClick(e =>
                    {
                        e.Windows.CloseAll();
                        lastAction = "Closed all windows";
                    })
                ]).FixedHeight(1),
                main.Text(""),
                main.HStack(buttons => [
                    buttons.Text("  "),
                    buttons.Button("Exit").OnClick(e => e.Context.RequestStop())
                ]).FixedHeight(1),
                main.Text(""),
                main.InfoBar([
                    "Tab", "Navigate",
                    "Escape", "Close Window",
                    "Ctrl+C", "Exit"
                ])
            ])
        )
    )
    .WithMouse()
    .Build();

await terminal.RunAsync();

// Helper functions to build window content using widget constructors directly
static Hex1bWidget BuildWindowContent(int windowNum, string windowId)
{
    return new VStackWidget([
        new TextBlockWidget(""),
        new TextBlockWidget($"  This is Window #{windowNum}"),
        new TextBlockWidget(""),
        new TextBlockWidget("  You can:"),
        new TextBlockWidget("  • Tab through focusables"),
        new TextBlockWidget("  • Press Escape to close"),
        new TextBlockWidget(""),
        new HStackWidget([
            new TextBlockWidget("  "),
            new ButtonWidget("OK")
        ])
    ]);
}

static Hex1bWidget BuildModalContent(string windowId)
{
    return new VStackWidget([
        new TextBlockWidget(""),
        new TextBlockWidget("  ⚠️  This is a modal dialog!"),
        new TextBlockWidget(""),
        new TextBlockWidget("  Background windows are blocked."),
        new TextBlockWidget(""),
        new HStackWidget([
            new TextBlockWidget("  "),
            new ButtonWidget("Confirm"),
            new TextBlockWidget(" "),
            new ButtonWidget("Cancel")
        ])
    ]);
}
