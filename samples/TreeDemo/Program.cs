using Hex1b;
using Hex1b.Events;
using Hex1b.Widgets;

// Track the last activated item for display
var lastActivated = "(none)";
var selectedCount = 0;

// Simulated async data source for lazy loading demo
async Task<IEnumerable<TreeItemWidget>> LoadChildrenAsync(TreeItemExpandingEventArgs e)
{
    // Simulate network/database delay (1.5 seconds to make loading indicator visible)
    await Task.Delay(1500);
    
    return e.Item.Label switch
    {
        "Remote Server" => [
            new TreeItemWidget("Users").WithIcon("👥").OnExpanding(LoadChildrenAsync),
            new TreeItemWidget("Logs").WithIcon("📋").OnExpanding(LoadChildrenAsync),
            new TreeItemWidget("Config").WithIcon("⚙️").OnExpanding(LoadChildrenAsync),
        ],
        "Users" => [
            new TreeItemWidget("alice").WithIcon("👤"),
            new TreeItemWidget("bob").WithIcon("👤"),
            new TreeItemWidget("charlie").WithIcon("👤"),
        ],
        "Logs" => [
            new TreeItemWidget("app.log").WithIcon("📄"),
            new TreeItemWidget("error.log").WithIcon("📄"),
            new TreeItemWidget("access.log").WithIcon("📄"),
        ],
        "Config" => [
            new TreeItemWidget("settings.json").WithIcon("📄"),
            new TreeItemWidget("secrets.env").WithIcon("🔒"),
        ],
        _ => []
    };
}

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("🌳 Tree Widget Demo"),
        v.Separator(),
        v.Text(""),
        
        v.HStack(h => [
            // Left side: Static tree
            h.Border(b => [
                b.Tree(
                    new TreeItemWidget("Root").WithIcon("📁").Expanded().WithChildren(
                        new TreeItemWidget("Documents").WithIcon("📁").Expanded().WithChildren(
                            new TreeItemWidget("Work").WithIcon("📁").WithChildren(
                                new TreeItemWidget("report.docx").WithIcon("📄"),
                                new TreeItemWidget("presentation.pptx").WithIcon("📄")
                            ),
                            new TreeItemWidget("Personal").WithIcon("📁").WithChildren(
                                new TreeItemWidget("resume.pdf").WithIcon("📄"),
                                new TreeItemWidget("notes.txt").WithIcon("📄")
                            )
                        ),
                        new TreeItemWidget("Pictures").WithIcon("📸").WithChildren(
                            new TreeItemWidget("vacation.jpg").WithIcon("📷"),
                            new TreeItemWidget("family.png").WithIcon("📷")
                        ),
                        new TreeItemWidget("Downloads").WithIcon("📥").WithChildren(
                            new TreeItemWidget("setup.exe").WithIcon("📦"),
                            new TreeItemWidget("archive.zip").WithIcon("📦")
                        )
                    )
                )
                .OnItemActivated(e => { lastActivated = e.Item.Label; })
                .FillHeight()
            ], title: "📂 Static Tree").FillWidth().FillHeight(),
            
            // Middle: Async lazy-loading tree
            h.Border(b => [
                b.Tree(
                    new TreeItemWidget("Remote Server").WithIcon("🖥️")
                        .OnExpanding(LoadChildrenAsync)  // Async lazy load with 500ms delay
                )
                .OnItemActivated(e => { lastActivated = e.Item.Label; })
                .FillHeight()
            ], title: "🌐 Async Lazy Load").FillWidth().FillHeight(),
            
            // Right side: Multi-select tree with cascade selection
            h.Border(b => [
                b.Tree(
                    new TreeItemWidget("Select Features").Expanded().WithChildren(
                        new TreeItemWidget("Core Features").Expanded().WithChildren(
                            new TreeItemWidget("Authentication"),
                            new TreeItemWidget("Authorization"),
                            new TreeItemWidget("Logging")
                        ),
                        new TreeItemWidget("Optional Features").Expanded().WithChildren(
                            new TreeItemWidget("Caching"),
                            new TreeItemWidget("Rate Limiting"),
                            new TreeItemWidget("Metrics")
                        ),
                        new TreeItemWidget("Integrations").WithChildren(
                            new TreeItemWidget("Database"),
                            new TreeItemWidget("Message Queue"),
                            new TreeItemWidget("External API")
                        )
                    )
                )
                .WithCascadeSelection()
                .OnSelectionChanged(e => { selectedCount = e.SelectedItems.Count; })
                .FillHeight()
            ], title: "📋 Cascade Select").FillWidth().FillHeight()
        ]).FillHeight(),
        
        v.Text(""),
        
        // Bottom section: Different guide styles
        v.HStack(h => [
            h.Border(b => [
                b.Tree(
                    new TreeItemWidget("Unicode").Expanded().WithChildren(
                        new TreeItemWidget("Child 1").WithChildren(
                            new TreeItemWidget("Grandchild")
                        ),
                        new TreeItemWidget("Child 2")
                    )
                ).WithGuideStyle(TreeGuideStyle.Unicode)
            ], title: "Unicode").FillWidth(),
            
            h.Border(b => [
                b.Tree(
                    new TreeItemWidget("ASCII").Expanded().WithChildren(
                        new TreeItemWidget("Child 1").WithChildren(
                            new TreeItemWidget("Grandchild")
                        ),
                        new TreeItemWidget("Child 2")
                    )
                ).WithGuideStyle(TreeGuideStyle.Ascii)
            ], title: "ASCII").FillWidth(),
            
            h.Border(b => [
                b.Tree(
                    new TreeItemWidget("Bold").Expanded().WithChildren(
                        new TreeItemWidget("Child 1").WithChildren(
                            new TreeItemWidget("Grandchild")
                        ),
                        new TreeItemWidget("Child 2")
                    )
                ).WithGuideStyle(TreeGuideStyle.Bold)
            ], title: "Bold").FillWidth(),
            
            h.Border(b => [
                b.Tree(
                    new TreeItemWidget("Double").Expanded().WithChildren(
                        new TreeItemWidget("Child 1").WithChildren(
                            new TreeItemWidget("Grandchild")
                        ),
                        new TreeItemWidget("Child 2")
                    )
                ).WithGuideStyle(TreeGuideStyle.Double)
            ], title: "Double").FillWidth()
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
        v.Text("Async tree shows ◌ loading indicator during 1.5s simulated delay")
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();
