using Hex1b;
using Hex1b.Widgets;

// Track the last activated item for display
var lastActivated = "(none)";
var selectedCount = 0;

// Sample file system data for lazy loading demo
var fileSystem = new Dictionary<string, string[]>
{
    ["Documents"] = ["Work", "Personal", "Archive"],
    ["Work"] = ["Projects", "Reports", "Meetings"],
    ["Personal"] = ["Photos", "Music", "Videos"],
    ["Projects"] = ["ProjectA", "ProjectB", "ProjectC"],
    ["Pictures"] = ["2023", "2024", "2025"],
    ["Music"] = ["Rock", "Jazz", "Classical"],
    ["Downloads"] = ["setup.exe", "document.pdf", "image.png"],
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("🌳 Tree Widget Demo"),
        v.Separator(),
        v.Text(""),
        
        v.HStack(h => [
            // Left side: Basic Tree with static data (many items for scrolling)
            h.Border(b => [
                b.Tree(
                    new TreeItemWidget("Root").WithIcon("📁").Expanded().WithChildren(
                        new TreeItemWidget("Documents").WithIcon("📁").Expanded().WithChildren(
                            new TreeItemWidget("Work").WithIcon("📁").Expanded().WithChildren(
                                new TreeItemWidget("report.docx").WithIcon("📄"),
                                new TreeItemWidget("presentation.pptx").WithIcon("📄"),
                                new TreeItemWidget("spreadsheet.xlsx").WithIcon("📄"),
                                new TreeItemWidget("budget.xlsx").WithIcon("📄"),
                                new TreeItemWidget("memo.docx").WithIcon("📄"),
                                new TreeItemWidget("proposal.pdf").WithIcon("📄")
                            ),
                            new TreeItemWidget("Personal").WithIcon("📁").Expanded().WithChildren(
                                new TreeItemWidget("resume.pdf").WithIcon("📄"),
                                new TreeItemWidget("notes.txt").WithIcon("📄"),
                                new TreeItemWidget("journal.md").WithIcon("📄"),
                                new TreeItemWidget("recipes.txt").WithIcon("📄")
                            ),
                            new TreeItemWidget("Archive").WithIcon("📁").Expanded().WithChildren(
                                new TreeItemWidget("2023").WithIcon("📁").WithChildren(
                                    new TreeItemWidget("taxes.pdf").WithIcon("📄"),
                                    new TreeItemWidget("receipts.zip").WithIcon("📦")
                                ),
                                new TreeItemWidget("2024").WithIcon("📁").WithChildren(
                                    new TreeItemWidget("taxes.pdf").WithIcon("📄"),
                                    new TreeItemWidget("receipts.zip").WithIcon("📦")
                                ),
                                new TreeItemWidget("backup.tar.gz").WithIcon("📦")
                            )
                        ),
                        new TreeItemWidget("Pictures").WithIcon("📸").Expanded().WithChildren(
                            new TreeItemWidget("vacation.jpg").WithIcon("📷"),
                            new TreeItemWidget("family.png").WithIcon("📷"),
                            new TreeItemWidget("birthday.jpg").WithIcon("📷"),
                            new TreeItemWidget("sunset.png").WithIcon("📷"),
                            new TreeItemWidget("portrait.jpg").WithIcon("📷")
                        ),
                        new TreeItemWidget("Music").WithIcon("🎵").Expanded().WithChildren(
                            new TreeItemWidget("song1.mp3").WithIcon("🎶"),
                            new TreeItemWidget("song2.mp3").WithIcon("🎶"),
                            new TreeItemWidget("song3.mp3").WithIcon("🎶"),
                            new TreeItemWidget("song4.mp3").WithIcon("🎶"),
                            new TreeItemWidget("playlist.m3u").WithIcon("📝"),
                            new TreeItemWidget("album.flac").WithIcon("🎶")
                        ),
                        new TreeItemWidget("Videos").WithIcon("🎬").Expanded().WithChildren(
                            new TreeItemWidget("movie.mp4").WithIcon("🎥"),
                            new TreeItemWidget("clip.avi").WithIcon("🎥"),
                            new TreeItemWidget("tutorial.mkv").WithIcon("🎥")
                        ),
                        new TreeItemWidget("Downloads").WithIcon("📥").Expanded().WithChildren(
                            new TreeItemWidget("setup.exe").WithIcon("📦"),
                            new TreeItemWidget("archive.zip").WithIcon("📦"),
                            new TreeItemWidget("installer.dmg").WithIcon("📦"),
                            new TreeItemWidget("package.deb").WithIcon("📦")
                        ),
                        new TreeItemWidget("Projects").WithIcon("💻").Expanded().WithChildren(
                            new TreeItemWidget("website").WithIcon("🌐"),
                            new TreeItemWidget("mobile-app").WithIcon("📱"),
                            new TreeItemWidget("api-server").WithIcon("💻")
                        )
                    )
                )
                .OnItemActivated(e => { lastActivated = e.Item.Label; })
                .FillHeight()
            ], title: "📂 File Browser").FillWidth().FillHeight(),
            
            // Right side: Multi-select tree
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
                .WithMultiSelect()
                .OnSelectionChanged(e => { selectedCount = e.SelectedItems.Count; })
                .FillHeight()
            ], title: "📋 Feature Selection (Multi-Select)").FillWidth().FillHeight()
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
        v.Text("↑↓: Navigate | ←: Collapse/Parent | →: Expand/Child | Space: Toggle | Enter: Activate"),
        v.Text("Tab: Switch trees | Ctrl+C: Exit")
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();
