using Hex1b;
using Hex1b.Terminal;
using Hex1b.Theming;

// Application state
var lastAction = "None";
var documentName = "Untitled";
var isModified = false;
var recentDocuments = new List<string> { "Report.md", "Notes.txt", "Config.json", "README.md" };

// Input control state
var searchText = "";
var notesText = "Enter your notes here...";
var toggleOptions = new[] { "Off", "On" };
var autoSaveIndex = 0;
var wordWrapIndex = 1;
var fontSizes = new[] { "Small", "Medium", "Large", "Extra Large" };
var selectedFontSize = 1; // Medium

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
                m.MenuItem("New").OnActivated(e => {
                    documentName = "Untitled";
                    isModified = false;
                    lastAction = "Created new document";
                }),
                m.MenuItem("Open").OnActivated(e => {
                    lastAction = "Open dialog would appear here";
                }),
                m.Separator(),
                m.Menu("Recent", m => [
                    ..recentDocuments.Select(doc => 
                        m.MenuItem(doc).OnActivated(e => {
                            documentName = doc;
                            isModified = false;
                            lastAction = $"Opened: {doc}";
                        })
                    )
                ]),
                m.Separator(),
                m.MenuItem("Save").OnActivated(e => {
                    isModified = false;
                    lastAction = $"Saved: {documentName}";
                }),
                m.MenuItem("Save As").OnActivated(e => {
                    lastAction = "Save As dialog would appear here";
                }),
                m.Separator(),
                m.MenuItem("Quit").OnActivated(e => {
                    e.Context.RequestStop();
                })
            ]),
            m.Menu("Edit", m => [
                m.MenuItem("Undo").Disabled(),
                m.MenuItem("Redo").Disabled(),
                m.Separator(),
                m.MenuItem("Cut").OnActivated(e => {
                    lastAction = "Cut";
                }),
                m.MenuItem("Copy").OnActivated(e => {
                    lastAction = "Copy";
                }),
                m.MenuItem("Paste").OnActivated(e => {
                    lastAction = "Paste";
                    isModified = true;
                }),
                m.Separator(),
                m.MenuItem("Select All").OnActivated(e => {
                    lastAction = "Select All";
                })
            ]),
            m.Menu("View", m => [
                m.MenuItem("Zoom In").OnActivated(e => {
                    lastAction = "Zoom In";
                }),
                m.MenuItem("Zoom Out").OnActivated(e => {
                    lastAction = "Zoom Out";
                }),
                m.Separator(),
                m.Menu("Appearance", m => [
                    m.MenuItem("Light Theme").OnActivated(e => {
                        lastAction = "Switched to Light Theme";
                    }),
                    m.MenuItem("Dark Theme").OnActivated(e => {
                        lastAction = "Switched to Dark Theme";
                    })
                ]),
                m.Separator(),
                m.MenuItem("Full Screen").OnActivated(e => {
                    lastAction = "Toggle Full Screen";
                })
            ]),
            m.Menu("Help", m => [
                m.MenuItem("Documentation").OnActivated(e => {
                    lastAction = "Opening documentation...";
                }),
                m.MenuItem("Keyboard Shortcuts").OnActivated(e => {
                    lastAction = "Showing keyboard shortcuts...";
                }),
                m.Separator(),
                m.MenuItem("About").OnActivated(e => {
                    lastAction = "Hex1b Menu Demo v1.0";
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
                
                // Search box
                content.HStack(row => [
                    row.Text("  Search: "),
                    row.TextBox(searchText)
                        .FixedWidth(30)
                        .OnTextChanged(e => searchText = e.NewText)
                ]).FixedHeight(1),
                
                content.Text(""),
                
                // Options row with toggles
                content.HStack(row => [
                    row.Text("  "),
                    row.ToggleSwitch(toggleOptions, autoSaveIndex)
                        .OnSelectionChanged(e => { autoSaveIndex = e.SelectedIndex; lastAction = $"Auto-save: {toggleOptions[e.SelectedIndex]}"; }),
                    row.Text(" Auto-save   "),
                    row.ToggleSwitch(toggleOptions, wordWrapIndex)
                        .OnSelectionChanged(e => { wordWrapIndex = e.SelectedIndex; lastAction = $"Word wrap: {toggleOptions[e.SelectedIndex]}"; }),
                    row.Text(" Word wrap")
                ]).FixedHeight(1),
                
                content.Text(""),
                
                // Notes text area
                content.HStack(row => [
                    row.Text("  Notes: "),
                    row.TextBox(notesText)
                        .FixedWidth(40)
                        .OnTextChanged(e => { notesText = e.NewText; isModified = true; })
                ]).FixedHeight(1),
                
                content.Text(""),
                
                // Picker control
                content.HStack(row => [
                    row.Text("  Font Size: "),
                    row.Picker(fontSizes, selectedFontSize)
                        .OnSelectionChanged(e => { selectedFontSize = e.SelectedIndex; lastAction = $"Font size: {fontSizes[e.SelectedIndex]}"; })
                ]).FixedHeight(1),
                
                content.Text(""),
                
                // Action buttons
                content.HStack(row => [
                    row.Text("  "),
                    row.Button("Save").OnClick(_ => { 
                        isModified = false; 
                        lastAction = $"Saved: {documentName}"; 
                    }),
                    row.Text(" "),
                    row.Button("Clear").OnClick(_ => { 
                        searchText = "";
                        notesText = "";
                        lastAction = "Cleared all fields"; 
                    }),
                    row.Text(" "),
                    row.Button("Exit").OnClick(e => e.Context.RequestStop())
                ]).FixedHeight(1),
                
                content.Text(""),
                content.Text("  Keyboard: Tab to navigate, Enter/Space to activate"),
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
