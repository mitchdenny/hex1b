using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

// Demo state
var windowCounter = 0;
var openWindowCount = 0;
var statusMessage = "Ready";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
        ctx.VStack(main => [
            // ─────────────────────────────────────────────────────────────────
            // MENU BAR
            // ─────────────────────────────────────────────────────────────────
            main.MenuBar(m => [
                m.Menu("File", m => [
                    m.MenuItem("New Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            id: $"window-{num}",
                            title: $"Window {num}",
                            content: () => BuildWindowContent(num),
                            width: 45,
                            height: 12,
                            position: new WindowPositionSpec(WindowPosition.Center, OffsetX: (num - 1) * 3, OffsetY: (num - 1) * 2),
                            chromeStyle: WindowChromeStyle.TitleAndClose,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Window {num}"; }
                        );
                        statusMessage = $"Opened: Window {num}";
                    }),
                    m.MenuItem("New Full Chrome Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            id: $"full-{num}",
                            title: $"Full Chrome {num}",
                            content: () => BuildWindowContent(num),
                            width: 45,
                            height: 12,
                            chromeStyle: WindowChromeStyle.Full,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Window {num}"; }
                        );
                        statusMessage = $"Opened: Full Chrome {num}";
                    }),
                    m.MenuItem("New Frameless Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            id: $"frame-{num}",
                            title: $"Frameless {num}",
                            content: () => BuildFramelessContent(num),
                            width: 35,
                            height: 8,
                            chromeStyle: WindowChromeStyle.None,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Frameless {num}"; }
                        );
                        statusMessage = $"Opened: Frameless {num}";
                    }),
                    m.Separator(),
                    m.MenuItem("Close All Windows").OnActivated(e => {
                        e.Windows.CloseAll();
                        openWindowCount = 0;
                        statusMessage = "Closed all windows";
                    }),
                    m.Separator(),
                    m.MenuItem("Exit").OnActivated(e => e.Context.RequestStop())
                ]),
                m.Menu("Window", m => [
                    m.MenuItem("Open Modal Dialog").OnActivated(e => {
                        openWindowCount++;
                        e.Windows.Open(
                            id: "modal",
                            title: "Modal Dialog",
                            content: () => new VStackWidget([
                                new TextBlockWidget(""),
                                new TextBlockWidget("  ⚠️  This is a modal dialog!"),
                                new TextBlockWidget(""),
                                new TextBlockWidget("  Background windows are blocked."),
                                new TextBlockWidget(""),
                                new HStackWidget([
                                    new TextBlockWidget("  "),
                                    new ButtonWidget("OK"),
                                    new TextBlockWidget(" "),
                                    new ButtonWidget("Cancel")
                                ])
                            ]),
                            width: 50,
                            height: 10,
                            isModal: true,
                            onClose: () => { openWindowCount--; statusMessage = "Modal closed"; }
                        );
                        statusMessage = "Opened modal dialog";
                    }),
                    m.Separator(),
                    m.MenuItem("Tile Windows").OnActivated(e => statusMessage = "Tile not yet implemented"),
                    m.MenuItem("Cascade Windows").OnActivated(e => statusMessage = "Cascade not yet implemented")
                ]),
                m.Menu("Help", m => [
                    m.MenuItem("About").OnActivated(e => {
                        if (e.Windows.IsOpen("about")) {
                            e.Windows.BringToFront("about");
                            return;
                        }
                        openWindowCount++;
                        e.Windows.Open(
                            id: "about",
                            title: "About",
                            content: () => new VStackWidget([
                                new TextBlockWidget(""),
                                new TextBlockWidget("  Hex1b Floating Windows Demo"),
                                new TextBlockWidget("  Version: 1.0.0"),
                                new TextBlockWidget(""),
                                new TextBlockWidget("  Features:"),
                                new TextBlockWidget("  • Multiple window styles"),
                                new TextBlockWidget("  • Drag to move"),
                                new TextBlockWidget("  • Min/Max/Close buttons"),
                                new TextBlockWidget(""),
                                new HStackWidget([
                                    new TextBlockWidget("  "),
                                    new ButtonWidget("Close")
                                ])
                            ]),
                            width: 40,
                            height: 13,
                            onClose: () => { openWindowCount--; statusMessage = "About closed"; }
                        );
                        statusMessage = "Opened About";
                    })
                ])
            ]),

            // ─────────────────────────────────────────────────────────────────
            // WINDOW PANEL (MDI area)
            // ─────────────────────────────────────────────────────────────────
            main.WindowPanel(w =>
                w.VStack(center => [
                    center.Text(""),
                    center.Text(""),
                    center.Text(""),
                    center.Text("╔═══════════════════════════════════════╗"),
                    center.Text("║      Floating Windows Demo            ║"),
                    center.Text("║                                       ║"),
                    center.Text("║  Use File menu to open windows        ║"),
                    center.Text("║  Drag title bar to move               ║"),
                    center.Text("║  Click buttons to min/max/close       ║"),
                    center.Text("╚═══════════════════════════════════════╝")
                ])
            ).Fill(),

            // ─────────────────────────────────────────────────────────────────
            // STATUS BAR
            // ─────────────────────────────────────────────────────────────────
            main.InfoBar([
                "Status", statusMessage,
                "Windows", $"{openWindowCount} open"
            ])
        ])
    )
    .WithMouse()
    .Build();

await terminal.RunAsync();

// Build window content
static Hex1bWidget BuildWindowContent(int windowNum)
{
    return new VStackWidget([
        new TextBlockWidget(""),
        new TextBlockWidget($"  This is Window #{windowNum}"),
        new TextBlockWidget(""),
        new TextBlockWidget("  Features:"),
        new TextBlockWidget("  • Drag title bar to move"),
        new TextBlockWidget("  • Click buttons to interact"),
        new TextBlockWidget("  • Press Escape to close"),
        new TextBlockWidget(""),
        new HStackWidget([
            new TextBlockWidget("  "),
            new ButtonWidget("Action"),
            new TextBlockWidget(" "),
            new ButtonWidget("Close")
        ])
    ]);
}

static Hex1bWidget BuildFramelessContent(int windowNum)
{
    return new VStackWidget([
        new TextBlockWidget($"  Frameless Window #{windowNum}"),
        new TextBlockWidget(""),
        new TextBlockWidget("  No title bar - just content."),
        new TextBlockWidget("  Press Escape to close."),
        new TextBlockWidget(""),
        new HStackWidget([
            new TextBlockWidget("  "),
            new ButtonWidget("Close")
        ])
    ]);
}
