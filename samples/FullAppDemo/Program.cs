using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

// =============================================================================
// FullAppDemo - Comprehensive demo showcasing multiple widgets working together
// This serves as a testbed for the notification system implementation.
// =============================================================================

// Application state
var currentView = "Dashboard";
var lastAction = "Welcome to FullAppDemo";
var isNavExpanded = true;
var isDetailsExpanded = false;

// Navigation items with icons
var navItems = new[]
{
    ("📊", "Dashboard"),
    ("📋", "Tasks"),
    ("📁", "Files"),
    ("⚙️", "Settings"),
    ("❓", "Help")
};
var selectedNavIndex = 0;

// Tasks data (for Tasks view)
var tasks = new List<(string status, string title, string priority)>
{
    ("✓", "Set up project structure", "Low"),
    ("○", "Implement notification widget", "High"),
    ("○", "Add keyboard navigation", "Medium"),
    ("○", "Write unit tests", "High"),
    ("○", "Update documentation", "Low"),
};
var selectedTaskIndex = 0;

// Files data (for Files view)
var files = new[]
{
    ("📄", "README.md", "2.3 KB"),
    ("📄", "Program.cs", "4.1 KB"),
    ("📁", "src/", "—"),
    ("📁", "tests/", "—"),
    ("📄", "LICENSE", "1.1 KB"),
};

// Settings state
var settingsOptions = new[] { "Off", "On" };
var darkModeIndex = 1;
var notificationsIndex = 1;
var autoSaveIndex = 0;

// Status
var statusMessage = "Ready";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    // Wrap in NotificationPanel to enable notifications throughout the app
    ctx.NotificationPanel(
        ctx.ZStack(z => [
            z.VStack(outer => [
                // ─────────────────────────────────────────────────────────────────
                // MENU BAR
                // ─────────────────────────────────────────────────────────────────
                outer.MenuBar(m => [
                    m.Menu("File", m => [
                    m.MenuItem("New Task").OnActivated(e => {
                        tasks.Add(("○", $"New Task {tasks.Count + 1}", "Medium"));
                        lastAction = "Created new task";
                        statusMessage = "Task created";
                        // Post a notification with secondary actions
                        e.Context.Notifications.Post(
                            new Notification("📋 Task Created", $"New Task {tasks.Count}")
                                .WithTimeout(TimeSpan.FromSeconds(30))
                                .PrimaryAction("View", async ctx => {
                                    selectedNavIndex = 1; // Switch to Tasks view
                                    currentView = "Tasks";
                                    lastAction = "Viewing tasks";
                                    ctx.Dismiss();
                                })
                                .SecondaryAction("Edit", async ctx => {
                                    lastAction = "Editing new task";
                                    ctx.Dismiss();
                                })
                                .SecondaryAction("Set Priority", async ctx => {
                                    lastAction = "Setting priority";
                                    ctx.Dismiss();
                                }));
                    }),
                    m.Separator(),
                    m.MenuItem("Save").OnActivated(e => {
                        lastAction = "Saved";
                        statusMessage = "All changes saved";
                        // Post a notification with secondary actions
                        e.Context.Notifications.Post(
                            new Notification("✓ Saved", "All changes saved successfully")
                                .WithTimeout(TimeSpan.FromSeconds(30))
                                .PrimaryAction("Undo", async ctx => {
                                    lastAction = "Undo save";
                                    statusMessage = "Save undone";
                                    ctx.Dismiss();
                                })
                                .SecondaryAction("View Changes", async ctx => {
                                    lastAction = "Viewing changes";
                                    ctx.Dismiss();
                                })
                                .SecondaryAction("Save Copy", async ctx => {
                                    lastAction = "Saving copy";
                                    ctx.Dismiss();
                                }));
                    }),
                    m.MenuItem("Save As...").OnActivated(e => {
                        lastAction = "Save As dialog";
                    }),
                    m.Separator(),
                    m.MenuItem("Export...").OnActivated(e => {
                        lastAction = "Export dialog";
                    }),
                    m.Separator(),
                    m.MenuItem("Quit").OnActivated(e => e.Context.RequestStop())
                ]),
                m.Menu("Edit", m => [
                    m.MenuItem("Undo").Disabled(),
                    m.MenuItem("Redo").Disabled(),
                    m.Separator(),
                    m.MenuItem("Cut"),
                    m.MenuItem("Copy"),
                    m.MenuItem("Paste"),
                    m.Separator(),
                    m.MenuItem("Select All")
                ]),
                m.Menu("View", m => [
                    m.MenuItem("Toggle Sidebar").OnActivated(e => {
                        isNavExpanded = !isNavExpanded;
                        lastAction = isNavExpanded ? "Sidebar shown" : "Sidebar hidden";
                    }),
                    m.MenuItem("Toggle Details").OnActivated(e => {
                        isDetailsExpanded = !isDetailsExpanded;
                        lastAction = isDetailsExpanded ? "Details shown" : "Details hidden";
                    }),
                    m.Separator(),
                    m.Menu("Go To", m => [
                        ..navItems.Select((nav, i) => 
                            m.MenuItem(nav.Item2).OnActivated(e => {
                                selectedNavIndex = i;
                                currentView = nav.Item2;
                                lastAction = $"Navigated to {nav.Item2}";
                            })
                        )
                    ]),
                    m.Separator(),
                    m.MenuItem("Refresh").OnActivated(e => {
                        lastAction = "Refreshed";
                        statusMessage = "Content refreshed";
                    })
                ]),
                m.Menu("Help", m => [
                    m.MenuItem("Documentation").OnActivated(e => {
                        lastAction = "Opening documentation...";
                    }),
                    m.MenuItem("Keyboard Shortcuts").OnActivated(e => {
                        lastAction = "Showing shortcuts...";
                    }),
                    m.Separator(),
                    m.MenuItem("About").OnActivated(e => {
                        lastAction = "FullAppDemo v1.0 - Hex1b Demo Application";
                    })
                ])
            ]),

            // ─────────────────────────────────────────────────────────────────
            // MAIN CONTENT AREA (with sidebars)
            // ─────────────────────────────────────────────────────────────────
            outer.HStack(content => [
                // LEFT SIDEBAR - Navigation Drawer
                content.Drawer()
                    .Expanded(isNavExpanded)
                    .CollapsedContent(c => [
                        c.VStack(collapsed => [
                            collapsed.Button("»").OnClick(_ => {
                                isNavExpanded = true;
                                lastAction = "Sidebar expanded";
                            }),
                            ..navItems.Select((nav, i) =>
                                collapsed.Button(nav.Item1)
                                    .OnClick(_ => {
                                        selectedNavIndex = i;
                                        currentView = nav.Item2;
                                        lastAction = $"Navigated to {nav.Item2}";
                                    })
                            )
                        ])
                    ])
                    .ExpandedContent(e => [
                        e.VStack(nav => [
                            nav.HStack(header => [
                                header.Text(" Navigation"),
                                header.Text("").Fill(),
                                header.Button("«").OnClick(_ => {
                                    isNavExpanded = false;
                                    lastAction = "Sidebar collapsed";
                                })
                            ]).FixedHeight(1),
                            nav.Text("─────────────────"),
                            ..navItems.Select((item, i) =>
                                nav.Button(selectedNavIndex == i 
                                    ? $" ▸ {item.Item1} {item.Item2}" 
                                    : $"   {item.Item1} {item.Item2}")
                                    .OnClick(_ => {
                                        selectedNavIndex = i;
                                        currentView = item.Item2;
                                        lastAction = $"Navigated to {item.Item2}";
                                    })
                            ),
                            nav.Text("").Fill(),
                            nav.Text("─────────────────"),
                            nav.Text($" {lastAction}").FixedHeight(1)
                        ])
                    ]),

                // MAIN CONTENT
                content.Border(
                    content.VStack(main => [
                        main.Text($"  {navItems[selectedNavIndex].Item1} {currentView}"),
                        main.Text("  " + new string('═', 40)),
                        main.Text(""),
                        ..BuildViewContent(main, currentView, tasks, selectedTaskIndex, files,
                            settingsOptions, darkModeIndex, notificationsIndex, autoSaveIndex,
                            idx => selectedTaskIndex = idx,
                            idx => darkModeIndex = idx,
                            idx => notificationsIndex = idx,
                            idx => autoSaveIndex = idx,
                            msg => { lastAction = msg; statusMessage = msg; })
                    ]),
                    title: currentView
                ).Fill(),

                // RIGHT SIDEBAR - Details Panel
                content.Drawer()
                    .Expanded(isDetailsExpanded)
                    .CollapsedContent(c => [
                        c.Button("«").OnClick(_ => {
                            isDetailsExpanded = true;
                            lastAction = "Details panel expanded";
                        })
                    ])
                    .ExpandedContent(e => [
                        e.VStack(details => [
                            details.HStack(header => [
                                header.Button("»").OnClick(_ => {
                                    isDetailsExpanded = false;
                                    lastAction = "Details panel collapsed";
                                }),
                                header.Text("").Fill(),
                                header.Text("Details ")
                            ]).FixedHeight(1),
                            details.Text("─────────────────"),
                            details.Text($" View: {currentView}"),
                            details.Text(""),
                            ..BuildDetailsContent(details, currentView, tasks, selectedTaskIndex)
                        ])
                    ])
            ]).Fill(),

            // ─────────────────────────────────────────────────────────────────
            // INFO BAR (Status Bar)
            // ─────────────────────────────────────────────────────────────────
            outer.InfoBar(s => [
                s.Section(currentView).FixedWidth(12),
                s.Separator(" │ "),
                s.Section(statusMessage).FillWidth(),
                s.Separator(" │ "),
                s.Section("Alt+Letter: Menu"),
                s.Separator(" │ "),
                s.Section("Tab: Navigate"),
                s.Separator(" │ "),
                s.Section("Ctrl+C: Exit")
            ])
        ])
    ])).WithOffset(2, 2))  // Close ZStack and NotificationPanel with offset
    .WithMouse()
    .Build();

await terminal.RunAsync();

// =============================================================================
// View Content Builders
// =============================================================================

static IEnumerable<Hex1bWidget> BuildViewContent(
    WidgetContext<VStackWidget> ctx,
    string view,
    List<(string status, string title, string priority)> tasks,
    int selectedTaskIndex,
    (string icon, string name, string size)[] files,
    string[] settingsOptions,
    int darkModeIndex,
    int notificationsIndex,
    int autoSaveIndex,
    Action<int> setSelectedTask,
    Action<int> setDarkMode,
    Action<int> setNotifications,
    Action<int> setAutoSave,
    Action<string> setStatus)
{
    return view switch
    {
        "Dashboard" => BuildDashboardView(ctx, tasks),
        "Tasks" => BuildTasksView(ctx, tasks, selectedTaskIndex, setSelectedTask, setStatus),
        "Files" => BuildFilesView(ctx, files),
        "Settings" => BuildSettingsView(ctx, settingsOptions, darkModeIndex, notificationsIndex, 
            autoSaveIndex, setDarkMode, setNotifications, setAutoSave, setStatus),
        "Help" => BuildHelpView(ctx),
        _ => [ctx.Text("  Unknown view")]
    };
}

static IEnumerable<Hex1bWidget> BuildDashboardView(
    WidgetContext<VStackWidget> ctx,
    List<(string status, string title, string priority)> tasks)
{
    var completedCount = tasks.Count(t => t.status == "✓");
    var pendingCount = tasks.Count - completedCount;
    var highPriorityCount = tasks.Count(t => t.priority == "High" && t.status != "✓");

    return [
        ctx.Text("  Welcome to FullAppDemo!"),
        ctx.Text(""),
        ctx.Text("  ┌─────────────────────────────────────────────┐"),
        ctx.Text("  │  Quick Stats                                │"),
        ctx.Text("  ├─────────────────────────────────────────────┤"),
        ctx.Text($"  │  📋 Total Tasks:      {tasks.Count,-20} │"),
        ctx.Text($"  │  ✓  Completed:        {completedCount,-20} │"),
        ctx.Text($"  │  ○  Pending:          {pendingCount,-20} │"),
        ctx.Text($"  │  🔴 High Priority:    {highPriorityCount,-20} │"),
        ctx.Text("  └─────────────────────────────────────────────┘"),
        ctx.Text(""),
        ctx.Text("  Use the navigation sidebar to explore different views."),
        ctx.Text("  Press Alt+F for File menu, Alt+V for View menu."),
        ctx.Text(""),
        ctx.Text("  ── Test SplitButton (standalone) ──"),
        ctx.HStack(h => [
            h.Text("  "),
            h.SplitButton("Action")
                .OnPrimaryClick(_ => { /* Primary action */ })
                .WithSecondaryAction("Option A", _ => { })
                .WithSecondaryAction("Option B", _ => { }),
            h.Text("  "),
            h.Button("Regular Button"),
        ]),
    ];
}

static IEnumerable<Hex1bWidget> BuildTasksView(
    WidgetContext<VStackWidget> ctx,
    List<(string status, string title, string priority)> tasks,
    int selectedTaskIndex,
    Action<int> setSelectedTask,
    Action<string> setStatus)
{
    var widgets = new List<Hex1bWidget>
    {
        ctx.HStack(h => [
            h.Text("  "),
            h.Button("+ Add Task").OnClick(_ => {
                tasks.Add(("○", $"New Task {tasks.Count + 1}", "Medium"));
                setStatus("Task added");
            }),
            h.Text(" "),
            h.Button("Toggle Selected").OnClick(_ => {
                if (selectedTaskIndex >= 0 && selectedTaskIndex < tasks.Count)
                {
                    var task = tasks[selectedTaskIndex];
                    tasks[selectedTaskIndex] = (task.status == "✓" ? "○" : "✓", task.title, task.priority);
                    setStatus(task.status == "✓" ? "Task marked incomplete" : "Task completed!");
                }
            })
        ]).FixedHeight(1),
        ctx.Text(""),
        ctx.Text("  Status │ Task                          │ Priority"),
        ctx.Text("  ───────┼───────────────────────────────┼─────────"),
    };

    for (int i = 0; i < tasks.Count; i++)
    {
        var task = tasks[i];
        var index = i;
        var prefix = i == selectedTaskIndex ? "▸ " : "  ";
        var priorityColor = task.priority switch
        {
            "High" => "🔴",
            "Medium" => "🟡",
            _ => "🟢"
        };
        
        widgets.Add(ctx.Button($"{prefix}{task.status}    │ {task.title,-29} │ {priorityColor} {task.priority}")
            .OnClick(_ => setSelectedTask(index)));
    }

    return widgets;
}

static IEnumerable<Hex1bWidget> BuildFilesView(
    WidgetContext<VStackWidget> ctx,
    (string icon, string name, string size)[] files)
{
    var widgets = new List<Hex1bWidget>
    {
        ctx.Text("  Icon │ Name                  │ Size"),
        ctx.Text("  ─────┼───────────────────────┼────────"),
    };

    foreach (var file in files)
    {
        widgets.Add(ctx.Text($"   {file.icon}  │ {file.name,-21} │ {file.size}"));
    }

    widgets.Add(ctx.Text(""));
    widgets.Add(ctx.Text($"  {files.Length} items"));

    return widgets;
}

static IEnumerable<Hex1bWidget> BuildSettingsView(
    WidgetContext<VStackWidget> ctx,
    string[] options,
    int darkModeIndex,
    int notificationsIndex,
    int autoSaveIndex,
    Action<int> setDarkMode,
    Action<int> setNotifications,
    Action<int> setAutoSave,
    Action<string> setStatus)
{
    return [
        ctx.Text("  Appearance"),
        ctx.Text("  ──────────────────────────────────"),
        ctx.HStack(h => [
            h.Text("    Dark Mode:       "),
            h.ToggleSwitch(options, darkModeIndex)
                .OnSelectionChanged(e => { setDarkMode(e.SelectedIndex); setStatus($"Dark mode: {options[e.SelectedIndex]}"); })
        ]).FixedHeight(1),
        ctx.Text(""),
        ctx.Text("  Notifications"),
        ctx.Text("  ──────────────────────────────────"),
        ctx.HStack(h => [
            h.Text("    Show Toasts:     "),
            h.ToggleSwitch(options, notificationsIndex)
                .OnSelectionChanged(e => { setNotifications(e.SelectedIndex); setStatus($"Notifications: {options[e.SelectedIndex]}"); })
        ]).FixedHeight(1),
        ctx.Text(""),
        ctx.Text("  Behavior"),
        ctx.Text("  ──────────────────────────────────"),
        ctx.HStack(h => [
            h.Text("    Auto-save:       "),
            h.ToggleSwitch(options, autoSaveIndex)
                .OnSelectionChanged(e => { setAutoSave(e.SelectedIndex); setStatus($"Auto-save: {options[e.SelectedIndex]}"); })
        ]).FixedHeight(1),
        ctx.Text(""),
        ctx.HStack(h => [
            h.Text("  "),
            h.Button("Save Settings").OnClick(_ => setStatus("Settings saved")),
            h.Text(" "),
            h.Button("Reset to Defaults").OnClick(_ => setStatus("Settings reset"))
        ]).FixedHeight(1)
    ];
}

static IEnumerable<Hex1bWidget> BuildHelpView(WidgetContext<VStackWidget> ctx)
{
    return [
        ctx.Text("  Keyboard Shortcuts"),
        ctx.Text("  ──────────────────────────────────────"),
        ctx.Text(""),
        ctx.Text("  Navigation"),
        ctx.Text("    Tab           Move to next control"),
        ctx.Text("    Shift+Tab     Move to previous control"),
        ctx.Text("    Arrow Keys    Navigate within lists"),
        ctx.Text(""),
        ctx.Text("  Menus"),
        ctx.Text("    Alt+F         Open File menu"),
        ctx.Text("    Alt+E         Open Edit menu"),
        ctx.Text("    Alt+V         Open View menu"),
        ctx.Text("    Alt+H         Open Help menu"),
        ctx.Text(""),
        ctx.Text("  Actions"),
        ctx.Text("    Enter/Space   Activate button/toggle"),
        ctx.Text("    Escape        Close menu/popup"),
        ctx.Text("    Ctrl+C        Exit application"),
    ];
}

static IEnumerable<Hex1bWidget> BuildDetailsContent(
    WidgetContext<VStackWidget> ctx,
    string view,
    List<(string status, string title, string priority)> tasks,
    int selectedTaskIndex)
{
    return view switch
    {
        "Tasks" when selectedTaskIndex >= 0 && selectedTaskIndex < tasks.Count => [
            ctx.Text($" Task #{selectedTaskIndex + 1}"),
            ctx.Text(""),
            ctx.Text($" Title:"),
            ctx.Text($"   {tasks[selectedTaskIndex].title}"),
            ctx.Text(""),
            ctx.Text($" Priority:"),
            ctx.Text($"   {tasks[selectedTaskIndex].priority}"),
            ctx.Text(""),
            ctx.Text($" Status:"),
            ctx.Text($"   {(tasks[selectedTaskIndex].status == "✓" ? "Complete" : "Pending")}"),
        ],
        _ => [
            ctx.Text(" Select an item"),
            ctx.Text(" to see details"),
        ]
    };
}
