using Hex1b;
using Hex1b.Input;
using Hex1b.Terminal;
using Hex1b.Theming;
using Hex1b.Widgets;

// PopupStack Demo - Anchored menus with automatic positioning
// Run with: dotnet run --project samples/ZStackDemo

var selectedAction = "None selected";
var searchQuery = "";

// File dialog state
var currentDirectory = Environment.CurrentDirectory;
var selectedFilePath = "";

// Fake search data
var allItems = new[]
{
    "Document.txt",
    "Project.sln",
    "README.md",
    "Configuration.json",
    "Database.db",
    "Settings.xml",
    "Report.pdf",
    "Script.ps1",
    "Notes.md",
    "Archive.zip"
};

try
{
    var presentation = new ConsolePresentationAdapter(enableMouse: true);
    var workload = new Hex1bAppWorkloadAdapter(presentation.Capabilities);
    
    var terminalOptions = new Hex1bTerminalOptions
    {
        PresentationAdapter = presentation,
        WorkloadAdapter = workload
    };
    terminalOptions.AddHex1bAppRenderOptimization();
    
    using var terminal = new Hex1bTerminal(terminalOptions);

    await using var app = new Hex1bApp(
        ctx => ctx.ThemePanel(
            theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(40, 40, 40)),
            ctx.VStack(main => [
                // Menu bar - buttons use PushAnchored for positioned menus
                main.HStack(menuBar => [
                    menuBar.Button(" File ")
                        .OnClick(e => e.PushAnchored(AnchorPosition.Below, () => BuildFileMenu(ctx, e.Popups, f => selectedAction = $"Opened: {f}"))),
                    menuBar.Button(" Edit ")
                        .OnClick(e => e.PushAnchored(AnchorPosition.Below, () => BuildEditMenu(ctx, e.Popups, a => selectedAction = a))),
                    menuBar.Button(" View ")
                        .OnClick(e => e.PushAnchored(AnchorPosition.Below, () => BuildViewMenu(ctx, e.Popups, a => selectedAction = a))),
                    menuBar.Button(" Help ")
                        .OnClick(e => e.PushAnchored(AnchorPosition.Below, () => BuildHelpMenu(ctx, e.Popups, a => selectedAction = a))),
                    menuBar.Text("").Fill(),
                ]).ContentHeight(),
                
                // Main content area
                main.Border(
                    main.VStack(content => [
                        content.Text("Anchored PopupStack Demo"),
                        content.Text("════════════════════════════════════════"),
                        content.Text(""),
                        content.Text("Menus are positioned relative to their trigger button!"),
                        content.Text("Press Ctrl+S for global search popup."),
                        content.Text(""),
                        content.Text($"Selected action: {selectedAction}"),
                        content.Text(""),
                        content.Text("Try clicking different menu buttons - each menu appears"),
                        content.Text("directly below its trigger button."),
                    ]),
                    title: "Main Content"
                ).Fill(),
                
                main.InfoBar([
                    "Tab", "Navigate",
                    "Ctrl+S", "Search",
                    "Ctrl+C", "Exit"
                ]),
            ])
        ).WithInputBindings(bindings =>
        {
            // Global search binding - Ctrl+S opens centered search popup
            bindings.Ctrl().Key(Hex1bKey.S).Action(actionCtx =>
            {
                searchQuery = ""; // Reset search on open
                actionCtx.Popups.Push(() => BuildSearchPopup(ctx, actionCtx.Popups, s => selectedAction = $"Selected: {s}"));
            });
        }),
        new Hex1bAppOptions
        {
            WorkloadAdapter = workload,
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

// Global search popup - centered with backdrop
Hex1bWidget BuildSearchPopup<TParent>(WidgetContext<TParent> ctx, PopupStack popups, Action<string> onSelect)
    where TParent : Hex1bWidget
{
    // Filter items based on current search query
    var filteredItems = string.IsNullOrEmpty(searchQuery)
        ? allItems.ToList()
        : allItems.Where(i => i.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

    return ctx.Center(
        ctx.ThemePanel(
            theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(50, 50, 70)),
            ctx.Border(
                ctx.VStack(search =>
                {
                    var widgets = new List<Hex1bWidget>
                    {
                        search.TextBox(searchQuery)
                            .OnTextChanged(e => { searchQuery = e.NewText; }),
                        search.Text(""),
                        search.Text(filteredItems.Count > 0 ? "Results:" : "No matches found")
                    };
                    
                    if (filteredItems.Count > 0)
                    {
                        widgets.Add(
                            search.List(filteredItems)
                                .OnItemActivated(e =>
                                {
                                    onSelect(e.ActivatedText);
                                    popups.Clear();
                                })
                                .FixedHeight(Math.Min(filteredItems.Count, 6))
                        );
                    }
                    
                    return widgets.ToArray();
                }).FixedWidth(40),
                title: "🔍 Search (Ctrl+S)"
            )
        )
    );
}

// Menu builders - cascading uses AnchorPosition.Right
Hex1bWidget BuildFileMenu<TParent>(WidgetContext<TParent> ctx, PopupStack popups, Action<string> onFileOpened)
    where TParent : Hex1bWidget
{
    return ctx.ThemePanel(
        theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(50, 50, 80)),
        ctx.Border(
            ctx.VStack(m => [
                m.Button(" New         ").OnClick(_ => popups.Clear()),
                m.Button(" Open...     ").OnClick(_ => {
                    // Clear menus first, then push modal dialog
                    popups.Clear();
                    currentDirectory = Environment.CurrentDirectory;
                    selectedFilePath = "";
                    popups.Push(() => BuildOpenFileDialog(ctx, popups, onFileOpened)).AsBarrier();
                }),
                m.Button(" Recent    ► ").OnClick(e => e.Popups.PushAnchored(e.Node, AnchorPosition.Right, () => BuildRecentMenu(ctx, popups))),
                m.Text("─────────────"),
                m.Button(" Save        ").OnClick(_ => popups.Clear()),
                m.Button(" Save As...  ").OnClick(_ => popups.Clear()),
                m.Text("─────────────"),
                m.Button(" Exit        ").OnClick(_ => popups.Clear()),
            ]),
            title: "File"
        ).FixedWidth(17)
    );
}

Hex1bWidget BuildRecentMenu<TParent>(WidgetContext<TParent> ctx, PopupStack popups)
    where TParent : Hex1bWidget
{
    return ctx.ThemePanel(
        theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(50, 80, 50)),
        ctx.Border(
            ctx.VStack(m => [
                m.Button(" document1.txt  ").OnClick(_ => popups.Clear()),
                m.Button(" report.md      ").OnClick(_ => popups.Clear()),
                m.Button(" code.cs        ").OnClick(_ => popups.Clear()),
                m.Text("────────────────"),
                m.Button(" More...      ► ").OnClick(e => e.Popups.PushAnchored(e.Node, AnchorPosition.Right, () => BuildMoreRecentMenu(ctx, popups))),
            ]),
            title: "Recent"
        ).FixedWidth(20)
    );
}

Hex1bWidget BuildMoreRecentMenu<TParent>(WidgetContext<TParent> ctx, PopupStack popups)
    where TParent : Hex1bWidget
{
    return ctx.ThemePanel(
        theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(80, 80, 50)),
        ctx.Border(
            ctx.VStack(m => [
                m.Button(" project.sln  ").OnClick(_ => popups.Clear()),
                m.Button(" notes.txt    ").OnClick(_ => popups.Clear()),
                m.Button(" config.json  ").OnClick(_ => popups.Clear()),
            ]),
            title: "More Recent"
        ).FixedWidth(18)
    );
}

Hex1bWidget BuildEditMenu<TParent>(WidgetContext<TParent> ctx, PopupStack popups, Action<string> onAction)
    where TParent : Hex1bWidget
{
    return ctx.ThemePanel(
        theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(50, 50, 80)),
        ctx.Border(
            ctx.VStack(m => [
                m.Button(" Undo        ").OnClick(_ => { onAction("Undo"); popups.Clear(); }),
                m.Button(" Redo        ").OnClick(_ => { onAction("Redo"); popups.Clear(); }),
                m.Text("─────────────"),
                m.Button(" Cut         ").OnClick(_ => { onAction("Cut"); popups.Clear(); }),
                m.Button(" Copy        ").OnClick(_ => { onAction("Copy"); popups.Clear(); }),
                m.Button(" Paste       ").OnClick(_ => { onAction("Paste"); popups.Clear(); }),
            ]),
            title: "Edit"
        ).FixedWidth(17)
    );
}

Hex1bWidget BuildViewMenu<TParent>(WidgetContext<TParent> ctx, PopupStack popups, Action<string> onAction)
    where TParent : Hex1bWidget
{
    return ctx.ThemePanel(
        theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(50, 50, 80)),
        ctx.Border(
            ctx.VStack(m => [
                m.Button(" Zoom In     ").OnClick(_ => { onAction("Zoom In"); popups.Clear(); }),
                m.Button(" Zoom Out    ").OnClick(_ => { onAction("Zoom Out"); popups.Clear(); }),
                m.Text("─────────────"),
                m.Button(" Full Screen ").OnClick(_ => { onAction("Full Screen"); popups.Clear(); }),
            ]),
            title: "View"
        ).FixedWidth(17)
    );
}

Hex1bWidget BuildHelpMenu<TParent>(WidgetContext<TParent> ctx, PopupStack popups, Action<string> onAction)
    where TParent : Hex1bWidget
{
    return ctx.ThemePanel(
        theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(50, 50, 80)),
        ctx.Border(
            ctx.VStack(m => [
                m.Button(" Documentation ").OnClick(_ => { onAction("Documentation"); popups.Clear(); }),
                m.Button(" About         ").OnClick(_ => { onAction("About"); popups.Clear(); }),
            ]),
            title: "Help"
        ).FixedWidth(18)
    );
}

// File Open Dialog - Modal dialog with directory/file browser
Hex1bWidget BuildOpenFileDialog<TParent>(WidgetContext<TParent> ctx, PopupStack popups, Action<string> onFileOpened)
    where TParent : Hex1bWidget
{
    // Get directories in current path (including . and ..)
    var directories = new List<string> { ".", ".." };
    try
    {
        directories.AddRange(
            Directory.GetDirectories(currentDirectory)
                .Select(d => Path.GetFileName(d))
                .OrderBy(d => d)
        );
    }
    catch { /* Ignore access errors */ }
    
    // Get files in current path
    var files = new List<string>();
    try
    {
        files.AddRange(
            Directory.GetFiles(currentDirectory)
                .Select(f => Path.GetFileName(f))
                .OrderBy(f => f)
        );
    }
    catch { /* Ignore access errors */ }
    
    // Calculate relative path from original working directory
    var basePath = Environment.CurrentDirectory;
    
    // Modal dialog - NO OnClickAway handler, so clicking outside does nothing
    return ctx.Backdrop(
        ctx.Center(
            ctx.ThemePanel(
                theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(45, 45, 55)),
                ctx.Border(
                    ctx.VStack(dialog => [
                        // Current directory display
                        dialog.Text($"📁 {currentDirectory}").ContentHeight(),
                        dialog.Text("").ContentHeight(),
                        
                        // Selected file path textbox
                        dialog.HStack(pathRow => [
                            pathRow.Text("File: ").ContentWidth(),
                            pathRow.TextBox(selectedFilePath)
                                .OnTextChanged(e => { selectedFilePath = e.NewText; })
                                .Fill(),
                        ]).ContentHeight(),
                        dialog.Text("").ContentHeight(),
                        
                        // Splitter: directories on left, files on right
                        dialog.HSplitter(
                            // Left pane: directories
                            dialog.Border(
                                dialog.VStack(left => [
                                    left.Text("Directories").ContentHeight(),
                                    left.Text("───────────").ContentHeight(),
                                    left.List(directories)
                                        .OnItemActivated(e => {
                                            // Navigate to directory
                                            var targetDir = e.ActivatedText;
                                            if (targetDir == ".")
                                            {
                                                // Stay in current directory
                                            }
                                            else if (targetDir == "..")
                                            {
                                                var parent = Directory.GetParent(currentDirectory);
                                                if (parent != null)
                                                {
                                                    currentDirectory = parent.FullName;
                                                    selectedFilePath = "";
                                                }
                                            }
                                            else
                                            {
                                                currentDirectory = Path.Combine(currentDirectory, targetDir);
                                                selectedFilePath = "";
                                            }
                                        })
                                        .Fill(),
                                ])
                            ),
                            // Right pane: files
                            dialog.Border(
                                dialog.VStack(right => [
                                    right.Text("Files").ContentHeight(),
                                    right.Text("─────").ContentHeight(),
                                    files.Count > 0
                                        ? right.List(files)
                                            .OnItemActivated(e => {
                                                // Select file - show relative path
                                                var fullPath = Path.Combine(currentDirectory, e.ActivatedText);
                                                selectedFilePath = Path.GetRelativePath(basePath, fullPath);
                                            })
                                            .Fill()
                                        : right.Text("(no files)").Fill(),
                                ])
                            ),
                            leftWidth: 25
                        ).Fill(),
                        
                        dialog.Text("").ContentHeight(),
                        
                        // Button row
                        dialog.HStack(buttons => [
                            buttons.Text("").Fill(),
                            buttons.Button(" Open ")
                                .OnClick(_ => {
                                    if (!string.IsNullOrEmpty(selectedFilePath))
                                    {
                                        onFileOpened(selectedFilePath);
                                    }
                                    popups.Pop();
                                }),
                            buttons.Text(" ").ContentWidth(),
                            buttons.Button(" Cancel ")
                                .OnClick(_ => {
                                    popups.Pop();
                                }),
                        ]).ContentHeight(),
                    ]).FixedWidth(60).FixedHeight(20),
                    title: "📂 Open File"
                )
            )
        )
    ).Transparent(); // Transparent backdrop, but modal (no click-away)
}
