using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

// Track the last activated item for display
var lastActivated = "(none)";
var selectedCount = 0;

// Externalized loading state for async lazy loading demo
// Instead of storing pre-built widgets, store flags and rebuild each render
var serverLoading = false;
var serverLoaded = false;
var usersLoading = false;
var usersLoaded = false;
var logsLoading = false;
var logsLoaded = false;
var configLoading = false;
var configLoaded = false;

// Helper to build children fresh each render with current loading state
List<TreeItemWidget> BuildServerChildren(TreeContext t, Hex1bApp app)
{
    if (!serverLoaded) return [];
    return [
        t.Item("Users", _ => usersLoaded ? [
            t.Item("alice").Icon("👤"),
            t.Item("bob").Icon("👤"),
            t.Item("charlie").Icon("👤")
        ] : [])
            .Loading(usersLoading)
            .Expanded(usersLoading || usersLoaded)
            .Icon("👥")
            .OnExpanding(async _ => {
                usersLoading = true;
                app.Invalidate();
                await Task.Delay(1000);
                usersLoaded = true;
                usersLoading = false;
                app.Invalidate();
                return [];  // Children built via callback above
            }),
        t.Item("Logs", _ => logsLoaded ? [
            t.Item("app.log").Icon("📄"),
            t.Item("error.log").Icon("📄"),
            t.Item("access.log").Icon("📄")
        ] : [])
            .Loading(logsLoading)
            .Expanded(logsLoading || logsLoaded)
            .Icon("📋")
            .OnExpanding(async _ => {
                logsLoading = true;
                app.Invalidate();
                await Task.Delay(1000);
                logsLoaded = true;
                logsLoading = false;
                app.Invalidate();
                return [];
            }),
        t.Item("Config", _ => configLoaded ? [
            t.Item("settings.json").Icon("📄"),
            t.Item("secrets.env").Icon("🔒")
        ] : [])
            .Loading(configLoading)
            .Expanded(configLoading || configLoaded)
            .Icon("⚙️")
            .OnExpanding(async _ => {
                configLoading = true;
                app.Invalidate();
                await Task.Delay(1000);
                configLoaded = true;
                configLoading = false;
                app.Invalidate();
                return [];
            })
    ];
}

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("🌳 Tree Widget Demo"),
        v.Separator(),
        v.Text(""),
        
        v.HStack(h => [
            // Left side: Static tree using new TreeContext API
            h.Border(b => [
                b.Tree(t => [
                    t.Item("Root", root => [
                        root.Item("Documents", docs => [
                            docs.Item("Work", work => [
                                work.Item("report.docx").Icon("📄"),
                                work.Item("presentation.pptx").Icon("📄")
                            ]).Icon("📁"),
                            docs.Item("Personal", personal => [
                                personal.Item("resume.pdf").Icon("📄"),
                                personal.Item("notes.txt").Icon("📄")
                            ]).Icon("📁")
                        ]).Expanded().Icon("📁"),
                        root.Item("Pictures", pics => [
                            pics.Item("vacation.jpg").Icon("📷"),
                            pics.Item("family.png").Icon("📷")
                        ]).Icon("📸"),
                        root.Item("Downloads", downloads => [
                            downloads.Item("setup.exe").Icon("📦"),
                            downloads.Item("archive.zip").Icon("📦")
                        ]).Icon("📥")
                    ]).Expanded().Icon("📁")
                ])
                .OnItemActivated(e => { lastActivated = e.Item.Label; })
                .FillHeight()
            ], title: "📂 Static Tree").FillWidth().FillHeight(),
            
            // Middle: Async lazy-loading tree with externalized state
            // Children are rebuilt fresh each render to pick up current loading state
            h.Border(b => [
                b.Tree(t => [
                    t.Item("Remote Server", _ => BuildServerChildren(t, app))
                        .Loading(serverLoading)
                        .Expanded(serverLoading || serverLoaded)
                        .Icon("🖥️")
                        .OnExpanding(async _ => {
                            serverLoading = true;
                            app.Invalidate();
                            
                            await Task.Delay(1500);
                            
                            serverLoaded = true;
                            serverLoading = false;
                            app.Invalidate();
                            return [];  // Children built via callback above
                        })
                ])
                .OnItemActivated(e => { lastActivated = e.Item.Label; })
                .FillHeight()
            ], title: "🌐 Async Lazy Load").FillWidth().FillHeight(),
            
            // Right side: Multi-select tree with cascade selection
            h.Border(b => [
                b.Tree(t => [
                    t.Item("Select Features", features => [
                        features.Item("Core Features", core => [
                            core.Item("Authentication"),
                            core.Item("Authorization"),
                            core.Item("Logging")
                        ]).Expanded(),
                        features.Item("Optional Features", optional => [
                            optional.Item("Caching"),
                            optional.Item("Rate Limiting"),
                            optional.Item("Metrics")
                        ]).Expanded(),
                        features.Item("Integrations", integrations => [
                            integrations.Item("Database"),
                            integrations.Item("Message Queue"),
                            integrations.Item("External API")
                        ])
                    ]).Expanded()
                ])
                .MultiSelect()
                .OnSelectionChanged(e => { selectedCount = e.SelectedItems.Count; })
                .FillHeight()
            ], title: "📋 Cascade Select").FillWidth().FillHeight()
        ]).FillHeight(),
        
        v.Text(""),
        
        // Bottom section: Different guide styles using ThemePanel
        v.HStack(h => [
            h.ThemePanel(
                theme => theme,  // Unicode is default
                h.Border(b => [
                    b.Tree(t => [
                        t.Item("Unicode", children => [
                            children.Item("Child 1", grandchildren => [
                                grandchildren.Item("Grandchild")
                            ]),
                            children.Item("Child 2")
                        ]).Expanded()
                    ])
                ], title: "Unicode").FillWidth()),
            
            h.ThemePanel(
                theme => theme
                    .Set(TreeTheme.Branch, "+- ")
                    .Set(TreeTheme.LastBranch, "\\- ")
                    .Set(TreeTheme.Vertical, "|  ")
                    .Set(TreeTheme.Space, "   "),
                h.Border(b => [
                    b.Tree(t => [
                        t.Item("ASCII", children => [
                            children.Item("Child 1", grandchildren => [
                                grandchildren.Item("Grandchild")
                            ]),
                            children.Item("Child 2")
                        ]).Expanded()
                    ])
                ], title: "ASCII").FillWidth()),
            
            h.ThemePanel(
                theme => theme
                    .Set(TreeTheme.Branch, "┣━ ")
                    .Set(TreeTheme.LastBranch, "┗━ ")
                    .Set(TreeTheme.Vertical, "┃  ")
                    .Set(TreeTheme.Space, "   "),
                h.Border(b => [
                    b.Tree(t => [
                        t.Item("Bold", children => [
                            children.Item("Child 1", grandchildren => [
                                grandchildren.Item("Grandchild")
                            ]),
                            children.Item("Child 2")
                        ]).Expanded()
                    ])
                ], title: "Bold").FillWidth()),
            
            h.ThemePanel(
                theme => theme
                    .Set(TreeTheme.Branch, "╠═ ")
                    .Set(TreeTheme.LastBranch, "╚═ ")
                    .Set(TreeTheme.Vertical, "║  ")
                    .Set(TreeTheme.Space, "   "),
                h.Border(b => [
                    b.Tree(t => [
                        t.Item("Double", children => [
                            children.Item("Child 1", grandchildren => [
                                grandchildren.Item("Grandchild")
                            ]),
                            children.Item("Child 2")
                        ]).Expanded()
                    ])
                ], title: "Double").FillWidth())
        ]),
        
        v.Text(""),
        v.Separator(),
        v.HStack(h => [
            h.Text($"Last activated: {lastActivated}"),
            h.Text(" | "),
            h.Text($"Selected items: {selectedCount}")
        ]),
        v.Text(""),
        v.Text("↑↓: Navigate | ←→: Collapse/Expand | Space: Toggle | Enter: Activate | Click ▶: Expand"),
        v.Text("Async tree shows animated spinner during loading")
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();
