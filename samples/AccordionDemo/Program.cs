using Hex1b;
using Hex1b.Documents;
using Hex1b.Events;
using Hex1b.Widgets;

// ── Create a fake workspace with sample files ─────────────────
var workspaceDir = Path.Combine(Path.GetTempPath(), "hex1b-ide-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(workspaceDir);
Directory.CreateDirectory(Path.Combine(workspaceDir, "src"));
Directory.CreateDirectory(Path.Combine(workspaceDir, "src", "Models"));
Directory.CreateDirectory(Path.Combine(workspaceDir, "tests"));
Directory.CreateDirectory(Path.Combine(workspaceDir, "docs"));

var sampleFiles = new Dictionary<string, string>
{
    ["README.md"] = """
        # My Project

        A sample project demonstrating the Hex1b TUI IDE.

        ## Getting Started

        Run `dotnet run` to start the application.

        ## Features

        - Accordion sidebar with file explorer
        - Tab-based editor with syntax-aware editing
        - Hex editor view for binary files
        - Status bar with cursor position
        """,
    ["src/Program.cs"] = """
        using System;
        using MyProject.Models;

        namespace MyProject;

        class Program
        {
            static async Task Main(string[] args)
            {
                Console.WriteLine("Hello, World!");

                var user = new User("Alice", "alice@example.com");
                var greeting = Utils.Greet(user.Name);
                Console.WriteLine(greeting);

                await Task.Delay(100);
                Console.WriteLine("Done!");
            }
        }
        """,
    ["src/Utils.cs"] = """
        namespace MyProject;

        public static class Utils
        {
            public static string Greet(string name)
                => $"Hello, {name}!";

            public static int Add(int a, int b) => a + b;

            public static string FormatDate(DateTime date)
                => date.ToString("yyyy-MM-dd HH:mm:ss");

            public static bool IsValidEmail(string email)
                => email.Contains('@') && email.Contains('.');
        }
        """,
    ["src/Models/User.cs"] = """
        namespace MyProject.Models;

        public record User(string Name, string Email)
        {
            public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
            public bool IsActive { get; init; } = true;

            public string DisplayName => $"{Name} <{Email}>";
        }
        """,
    ["src/Models/Product.cs"] = """
        namespace MyProject.Models;

        public record Product(string Name, decimal Price, string Category)
        {
            public int Stock { get; init; }
            public bool IsAvailable => Stock > 0;

            public string FormattedPrice => $"${Price:F2}";
        }
        """,
    ["src/Config.json"] = """
        {
          "name": "my-project",
          "version": "1.0.0",
          "settings": {
            "theme": "dark",
            "fontSize": 14,
            "tabSize": 4,
            "wordWrap": true
          },
          "dependencies": {
            "Hex1b": "1.0.0"
          }
        }
        """,
    ["tests/ProgramTests.cs"] = """
        using Xunit;
        using MyProject;

        public class ProgramTests
        {
            [Fact]
            public void Greet_ReturnsExpected()
            {
                Assert.Equal("Hello, Alice!", Utils.Greet("Alice"));
            }

            [Fact]
            public void Add_ReturnsSum()
            {
                Assert.Equal(5, Utils.Add(2, 3));
            }

            [Fact]
            public void FormatDate_ReturnsIso()
            {
                var date = new DateTime(2025, 1, 15, 10, 30, 0);
                Assert.Equal("2025-01-15 10:30:00", Utils.FormatDate(date));
            }

            [Fact]
            public void IsValidEmail_ValidEmail_ReturnsTrue()
            {
                Assert.True(Utils.IsValidEmail("test@example.com"));
            }
        }
        """,
    ["docs/ARCHITECTURE.md"] = """
        # Architecture

        This project follows a simple layered architecture:

        1. **Program** — entry point
        2. **Models** — data records (User, Product)
        3. **Utils** — shared helper methods
        4. **Tests** — xUnit test suite

        ## Data Flow

        Input → Program → Models → Utils → Output

        ## Key Decisions

        - Records for immutable data models
        - Static utility class for shared logic
        - xUnit for testing
        """,
    [".gitignore"] = "bin/\nobj/\n*.user\n.vs/\n*.swp\n",
};

foreach (var (path, content) in sampleFiles)
{
    File.WriteAllText(Path.Combine(workspaceDir, path), content);
}

// ── State ─────────────────────────────────────────────────────
var openDocs = new Dictionary<string, (Hex1bDocument Doc, EditorState TextState, EditorState HexState)>();
var openTabs = new List<string>();
var editorModes = new Dictionary<string, int>(); // 0=Code, 1=Hex
var activeTab = -1;
var statusMessage = "Ready";
var expandedSection = 0; // which accordion section is expanded

// Timeline / git log (fake)
var timelineItems = new List<(string Hash, string Message, string Author)>
{
    ("a1b2c3d", "Add accordion widget", "Alice"),
    ("e4f5g6h", "Fix build script", "Bob"),
    ("i7j8k9l", "Update README.md", "Alice"),
    ("m0n1o2p", "Initial commit", "Alice"),
};

// Source control changes (fake)
var scChanges = new List<(string Status, string File)>
{
    ("M", "src/Program.cs"),
    ("M", "src/Utils.cs"),
    ("A", "src/Models/Product.cs"),
    ("?", "docs/ARCHITECTURE.md"),
};

(Hex1bDocument Doc, EditorState TextState, EditorState HexState) OpenFile(string relativePath)
{
    if (!openDocs.ContainsKey(relativePath))
    {
        var fullPath = Path.Combine(workspaceDir, relativePath);
        Hex1bDocument doc;
        if (relativePath.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : [];
            doc = new Hex1bDocument(bytes);
        }
        else
        {
            var content = File.Exists(fullPath) ? File.ReadAllText(fullPath) : "";
            doc = new Hex1bDocument(content);
        }
        openDocs[relativePath] = (doc, new EditorState(doc), new EditorState(doc));
    }

    if (!openTabs.Contains(relativePath))
    {
        openTabs.Add(relativePath);
        editorModes[relativePath] = 0;
    }

    activeTab = openTabs.IndexOf(relativePath);
    return openDocs[relativePath];
}

// Open a default file
OpenFile("src/Program.cs");

// ── Build file tree ───────────────────────────────────────────
var rootEntry = FileEntry.ScanDirectory(workspaceDir, workspaceDir);

// ── UI ────────────────────────────────────────────────────────
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithDiagnostics()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v =>
        [
            // ── Menu bar ──
            v.MenuBar(m =>
            [
                m.Menu("File", m2 =>
                [
                    m2.MenuItem("New File").OnActivated(_ => statusMessage = "New file..."),
                    m2.MenuItem("Open...").OnActivated(_ => statusMessage = "Open file..."),
                    m2.Separator(),
                    m2.MenuItem("Save").OnActivated(_ =>
                    {
                        if (activeTab >= 0 && activeTab < openTabs.Count)
                        {
                            var path = openTabs[activeTab];
                            if (openDocs.TryGetValue(path, out var entry))
                            {
                                File.WriteAllText(Path.Combine(workspaceDir, path), entry.Doc.GetText());
                                statusMessage = $"Saved {path}";
                            }
                        }
                    }),
                    m2.MenuItem("Save All"),
                    m2.Separator(),
                    m2.MenuItem("Exit").OnActivated(e => e.Context.RequestStop()),
                ]),
                m.Menu("Edit", m2 =>
                [
                    m2.MenuItem("Undo"),
                    m2.MenuItem("Redo"),
                    m2.Separator(),
                    m2.MenuItem("Cut"),
                    m2.MenuItem("Copy"),
                    m2.MenuItem("Paste"),
                    m2.Separator(),
                    m2.MenuItem("Find"),
                    m2.MenuItem("Replace"),
                ]),
                m.Menu("View", m2 =>
                [
                    m2.MenuItem("Explorer").OnActivated(_ => expandedSection = 0),
                    m2.MenuItem("Document").OnActivated(_ => expandedSection = 1),
                    m2.MenuItem("Timeline").OnActivated(_ => expandedSection = 2),
                    m2.MenuItem("Source Control").OnActivated(_ => expandedSection = 3),
                ]),
                m.Menu("Help", m2 =>
                [
                    m2.MenuItem("Keyboard Shortcuts").OnActivated(_ => statusMessage = "Tab=navigate, Enter/Space=activate, ↑↓=move, Ctrl+C=exit"),
                    m2.MenuItem("About").OnActivated(_ => statusMessage = "Hex1b IDE Demo v1.0"),
                ]),
            ]).ContentHeight(),

            // ── Main content: sidebar accordion + editor tabs ──
            v.HSplitter(
                // LEFT: Accordion sidebar
                left =>
                [
                    left.Accordion(a =>
                    [
                        // ── EXPLORER section with file tree ──
                        a.Section(s =>
                        [
                            s.Tree(tc => BuildTreeItems(tc, rootEntry.Children))
                                .OnItemActivated(e =>
                                {
                                    var relPath = FindRelativePath(rootEntry, e.Item.Label);
                                    if (relPath != null && !relPath.EndsWith(Path.DirectorySeparatorChar))
                                    {
                                        OpenFile(relPath);
                                        statusMessage = $"Opened {relPath}";
                                    }
                                })
                                .FillHeight(),
                        ]).Title("EXPLORER")
                        .Expanded(expandedSection == 0)
                        .RightActions(ra =>
                        [
                            ra.Icon("+").OnClick(_ => statusMessage = "New file..."),
                            ra.Icon("⟳").OnClick(_ =>
                            {
                                rootEntry = FileEntry.ScanDirectory(workspaceDir, workspaceDir);
                                statusMessage = "Explorer refreshed";
                            }),
                        ]),

                        // ── OUTLINE section (document internals) ──
                        a.Section(s =>
                        {
                            if (activeTab >= 0 && activeTab < openTabs.Count)
                            {
                                var path = openTabs[activeTab];
                                if (openDocs.TryGetValue(path, out var entry))
                                {
                                    return [s.DocumentDiagnosticPanel(entry.Doc).FillHeight()];
                                }
                            }
                            return [s.Text("  No file open")];
                        }).Title("DOCUMENT")
                        .Expanded(expandedSection == 1)
                        .RightActions(ra =>
                        [
                            ra.Icon("⟳").OnClick(_ => statusMessage = "Document view refreshed"),
                        ]),

                        // ── TIMELINE section ──
                        a.Section(s =>
                        {
                            var widgets = new List<Hex1bWidget>();
                            foreach (var item in timelineItems)
                            {
                                widgets.Add(s.Text($"  ● {item.Message}"));
                                widgets.Add(s.Text($"    {item.Hash[..7]} · {item.Author}"));
                            }
                            return widgets;
                        }).Title("TIMELINE")
                        .Expanded(expandedSection == 2)
                        .RightActions(ra =>
                        [
                            ra.Icon("🔍").OnClick(_ => statusMessage = "Filter timeline..."),
                        ]),

                        // ── SOURCE CONTROL section ──
                        a.Section(s =>
                        {
                            var widgets = new List<Hex1bWidget>();
                            foreach (var change in scChanges)
                            {
                                var statusIcon = change.Status switch
                                {
                                    "M" => "M",
                                    "A" => "A",
                                    "D" => "D",
                                    "?" => "U",
                                    _ => " ",
                                };
                                widgets.Add(s.Text($"  {statusIcon}  {change.File}"));
                            }
                            widgets.Add(s.Text(""));
                            widgets.Add(s.Text($"  {scChanges.Count} changes"));
                            return widgets;
                        }).Title("SOURCE CONTROL")
                        .Expanded(expandedSection == 3)
                        .LeftActions(la =>
                        [
                            la.Toggle(),
                            la.Icon("✓").OnClick(_ => statusMessage = "Changes committed"),
                        ])
                        .RightActions(ra =>
                        [
                            ra.Icon("⟳").OnClick(_ => statusMessage = "Pulling changes..."),
                            ra.Icon("…").OnClick(_ => statusMessage = "More SCM actions..."),
                        ]),
                    ])
                ],

                // RIGHT: Editor tabs
                right =>
                {
                    if (openTabs.Count == 0)
                    {
                        return
                        [
                            right.VStack(empty =>
                            [
                                empty.Text(""),
                                empty.Text("  No editors open"),
                                empty.Text(""),
                                empty.Text("  Open a file from the Explorer sidebar"),
                                empty.Text("  to begin editing."),
                                empty.Text(""),
                                empty.Text("  Keyboard shortcuts:"),
                                empty.Text("    Tab          Navigate between panels"),
                                empty.Text("    Enter/Space  Open file / toggle section"),
                                empty.Text("    ↑/↓          Navigate items"),
                                empty.Text("    Ctrl+C       Exit"),
                            ]).FillHeight()
                        ];
                    }

                    return
                    [
                        right.TabPanel(tc =>
                        {
                            var tabs = new List<TabItemWidget>();
                            for (var i = 0; i < openTabs.Count; i++)
                            {
                                var tabPath = openTabs[i];
                                var tabName = Path.GetFileName(tabPath);
                                var (_, textState, hexState) = openDocs[tabPath];
                                var mode = editorModes.GetValueOrDefault(tabPath, 0);
                                var capturedPath = tabPath;
                                var capturedIndex = i;

                                var tab = tc.Tab(tabName, content =>
                                {
                                    var widgets = new List<Hex1bWidget>();

                                    widgets.Add(content.HStack(h =>
                                    [
                                        h.ToggleSwitch(["Code", "Hex"], mode)
                                            .OnSelectionChanged(e => editorModes[capturedPath] = e.SelectedIndex),
                                    ]).ContentHeight());

                                    if (mode == 0)
                                    {
                                        widgets.Add(content.Editor(textState).FillWidth().FillHeight());
                                    }
                                    else
                                    {
                                        widgets.Add(content.Editor(hexState)
                                            .WithViewRenderer(new HexEditorViewRenderer())
                                            .FillWidth().FillHeight());
                                    }

                                    return widgets;
                                });

                                if (i == activeTab)
                                    tab = tab.Selected();

                                tabs.Add(tab);
                            }
                            return tabs;
                        })
                        .OnSelectionChanged(e => activeTab = e.SelectedIndex)
                        .FillWidth().FillHeight()
                    ];
                },
                leftWidth: 32).FillWidth().FillHeight(),

            // ── Status bar ──
            v.InfoBar(ib =>
            {
                var items = new List<IInfoBarChild>();
                items.Add(ib.Section("hex1b IDE"));
                items.Add(ib.Separator());

                if (activeTab >= 0 && activeTab < openTabs.Count)
                {
                    var path = openTabs[activeTab];
                    items.Add(ib.Section(path));

                    if (openDocs.TryGetValue(path, out var entry))
                    {
                        items.Add(ib.Spacer());
                        var cursorOffset = Math.Min(entry.TextState.Cursor.Position.Value, entry.Doc.Length);
                        var pos = entry.Doc.OffsetToPosition(new DocumentOffset(cursorOffset));
                        items.Add(ib.Section($"Ln {pos.Line + 1}, Col {pos.Column + 1}"));
                        items.Add(ib.Separator());
                        items.Add(ib.Section($"{entry.Doc.Length} chars"));
                        items.Add(ib.Separator());
                        items.Add(ib.Section("UTF-8"));
                    }
                }
                else
                {
                    items.Add(ib.Section(statusMessage));
                }

                return items;
            }).ContentHeight(),
        ]);
    })
    .Build();

await terminal.RunAsync();

// ── Cleanup ───────────────────────────────────────────────────
try { Directory.Delete(workspaceDir, true); } catch { }

// ── Helpers ───────────────────────────────────────────────────
static IEnumerable<TreeItemWidget> BuildTreeItems(TreeContext tc, List<FileEntry> entries)
{
    foreach (var entry in entries)
    {
        if (entry.IsDirectory)
        {
            yield return tc.Item(entry.Name, sub => BuildTreeItems(sub, entry.Children))
                .Icon("📁").Expanded();
        }
        else
        {
            var icon = entry.Name switch
            {
                _ when entry.Name.EndsWith(".cs") => "🔷",
                _ when entry.Name.EndsWith(".md") => "📝",
                _ when entry.Name.EndsWith(".json") => "⚙️",
                _ when entry.Name.EndsWith(".bin") => "🔢",
                _ => "📄",
            };
            yield return tc.Item(entry.Name).Icon(icon);
        }
    }
}

static string? FindRelativePath(FileEntry root, string label)
{
    foreach (var child in root.Children)
    {
        if (!child.IsDirectory && child.Name == label)
            return child.RelativePath;

        if (child.IsDirectory)
        {
            var found = FindRelativePath(child, label);
            if (found != null) return found;
        }
    }
    return null;
}

record FileEntry(string Name, string RelativePath, bool IsDirectory, List<FileEntry> Children)
{
    public static FileEntry ScanDirectory(string dir, string relativeTo)
    {
        var name = Path.GetFileName(dir);
        var relPath = Path.GetRelativePath(relativeTo, dir);
        var children = new List<FileEntry>();

        foreach (var subDir in Directory.GetDirectories(dir).OrderBy(d => d))
            children.Add(ScanDirectory(subDir, relativeTo));

        foreach (var file in Directory.GetFiles(dir).OrderBy(f => f))
        {
            var fileName = Path.GetFileName(file);
            var fileRelPath = Path.GetRelativePath(relativeTo, file);
            children.Add(new FileEntry(fileName, fileRelPath, false, []));
        }

        return new FileEntry(name, relPath, true, children);
    }
}
