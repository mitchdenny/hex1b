using Hex1b;
using Hex1b.Terminal;
using Hex1b.Theming;

// Application state
var lastAction = "None";
var documentName = "Untitled";
var isModified = false;
var recentDocuments = new List<string> { "Report.md", "Notes.txt", "Config.json", "README.md" };

var presentation = new ConsolePresentationAdapter(enableMouse: true);
var workload = new Hex1bAppWorkloadAdapter(presentation.Capabilities);

var terminalOptions = new Hex1bTerminalOptions
{
    PresentationAdapter = presentation,
    WorkloadAdapter = workload
};
terminalOptions.AddHex1bAppRenderOptimization();

using var terminal = new Hex1bTerminal(terminalOptions);

await using var app = new Hex1bApp(ctx =>
    ctx.VStack(main => [
        // Menu bar at the top
        main.MenuBar(m => [
            m.Menu("File", m => [
                m.MenuItem("New").OnSelect(e => {
                    documentName = "Untitled";
                    isModified = false;
                    lastAction = "Created new document";
                    e.CloseMenu();
                }),
                m.MenuItem("Open").OnSelect(e => {
                    lastAction = "Open dialog would appear here";
                    e.CloseMenu();
                }),
                m.Separator(),
                m.Menu("Recent", m => [
                    ..recentDocuments.Select(doc => 
                        m.MenuItem(doc).OnSelect(e => {
                            documentName = doc;
                            isModified = false;
                            lastAction = $"Opened: {doc}";
                            e.CloseMenu();
                        })
                    )
                ]),
                m.Separator(),
                m.MenuItem("Save").OnSelect(e => {
                    isModified = false;
                    lastAction = $"Saved: {documentName}";
                    e.CloseMenu();
                }),
                m.MenuItem("Save As").OnSelect(e => {
                    lastAction = "Save As dialog would appear here";
                    e.CloseMenu();
                }),
                m.Separator(),
                m.MenuItem("Quit").OnSelect(e => {
                    e.CloseMenu();
                    e.Context.RequestStop();
                })
            ]),
            m.Menu("Edit", m => [
                m.MenuItem("Undo").Disabled(),
                m.MenuItem("Redo").Disabled(),
                m.Separator(),
                m.MenuItem("Cut").OnSelect(e => {
                    lastAction = "Cut";
                    e.CloseMenu();
                }),
                m.MenuItem("Copy").OnSelect(e => {
                    lastAction = "Copy";
                    e.CloseMenu();
                }),
                m.MenuItem("Paste").OnSelect(e => {
                    lastAction = "Paste";
                    isModified = true;
                    e.CloseMenu();
                }),
                m.Separator(),
                m.MenuItem("Select All").OnSelect(e => {
                    lastAction = "Select All";
                    e.CloseMenu();
                })
            ]),
            m.Menu("View", m => [
                m.MenuItem("Zoom In").OnSelect(e => {
                    lastAction = "Zoom In";
                    e.CloseMenu();
                }),
                m.MenuItem("Zoom Out").OnSelect(e => {
                    lastAction = "Zoom Out";
                    e.CloseMenu();
                }),
                m.Separator(),
                m.Menu("Appearance", m => [
                    m.MenuItem("Light Theme").OnSelect(e => {
                        lastAction = "Switched to Light Theme";
                        e.CloseMenu();
                    }),
                    m.MenuItem("Dark Theme").OnSelect(e => {
                        lastAction = "Switched to Dark Theme";
                        e.CloseMenu();
                    })
                ]),
                m.Separator(),
                m.MenuItem("Full Screen").OnSelect(e => {
                    lastAction = "Toggle Full Screen";
                    e.CloseMenu();
                })
            ]),
            m.Menu("Help", m => [
                m.MenuItem("Documentation").OnSelect(e => {
                    lastAction = "Opening documentation...";
                    e.CloseMenu();
                }),
                m.MenuItem("Keyboard Shortcuts").OnSelect(e => {
                    lastAction = "Showing keyboard shortcuts...";
                    e.CloseMenu();
                }),
                m.Separator(),
                m.MenuItem("About").OnSelect(e => {
                    lastAction = "Hex1b Menu Demo v1.0";
                    e.CloseMenu();
                })
            ])
        ]),
        
        // Main content area
        main.Border(
            main.VStack(content => [
                content.Text(""),
                content.Text("  Menu Bar Demo"),
                content.Text("  ═══════════════════════════════════════"),
                content.Text(""),
                content.Text($"  Document: {documentName}{(isModified ? " *" : "")}"),
                content.Text($"  Last Action: {lastAction}"),
                content.Text(""),
                content.Text("  Keyboard Navigation:"),
                content.Text("  • Alt+F/E/V/H - Open menu by accelerator"),
                content.Text("  • ↑/↓ - Navigate menu items"),
                content.Text("  • → - Open submenu"),
                content.Text("  • ← - Close submenu"),
                content.Text("  • Enter/Space - Activate item"),
                content.Text("  • Escape - Close menu"),
                content.Text(""),
                content.Text("  Mouse:"),
                content.Text("  • Click menu to open"),
                content.Text("  • Click item to activate"),
                content.Text("  • Click outside to close"),
            ]),
            title: "Main Content"
        ).Fill(),
        
        // Status bar
        main.InfoBar([
            "Tab", "Navigate",
            "Alt+Letter", "Menu",
            "Ctrl+C", "Exit"
        ])
    ]),
    new Hex1bAppOptions
    {
        WorkloadAdapter = workload,
        EnableMouse = true
    }
);

await app.RunAsync();
