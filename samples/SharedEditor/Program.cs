using Hex1b;
using Hex1b.Documents;
using Hex1b.Widgets;

// ── Create a fake workspace with sample files ─────────────────
var workspaceDir = Path.Combine(Path.GetTempPath(), "hex1b-workspace-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(workspaceDir);
Directory.CreateDirectory(Path.Combine(workspaceDir, "src"));
Directory.CreateDirectory(Path.Combine(workspaceDir, "tests"));
Directory.CreateDirectory(Path.Combine(workspaceDir, "docs"));

var sampleFiles = new Dictionary<string, string>
{
    ["README.md"] = "# My Project\n\nA sample project for the Hex1b editor demo.\n\n## Getting Started\n\nRun `dotnet run` to start.",
    ["src/Program.cs"] = "using System;\n\nnamespace MyProject;\n\nclass Program\n{\n    static void Main(string[] args)\n    {\n        Console.WriteLine(\"Hello, World!\");\n    }\n}",
    ["src/Config.json"] = "{\n  \"name\": \"my-project\",\n  \"version\": \"1.0.0\",\n  \"settings\": {\n    \"theme\": \"dark\",\n    \"fontSize\": 14,\n    \"tabSize\": 4\n  }\n}",
    ["src/Utils.cs"] = "namespace MyProject;\n\npublic static class Utils\n{\n    public static string Greet(string name)\n        => $\"Hello, {name}!\";\n\n    public static int Add(int a, int b) => a + b;\n}",
    ["tests/ProgramTests.cs"] = "using Xunit;\nusing MyProject;\n\npublic class ProgramTests\n{\n    [Fact]\n    public void Greet_ReturnsExpected()\n    {\n        Assert.Equal(\"Hello, Alice!\", Utils.Greet(\"Alice\"));\n    }\n\n    [Fact]\n    public void Add_ReturnsSum()\n    {\n        Assert.Equal(5, Utils.Add(2, 3));\n    }\n}",
    ["docs/ARCHITECTURE.md"] = "# Architecture\n\nThis project follows a simple layered architecture:\n\n1. **Program** — entry point\n2. **Utils** — shared helpers\n3. **Tests** — xUnit test suite\n\n## Data Flow\n\nInput → Program → Utils → Output",
    [".gitignore"] = "bin/\nobj/\n*.user\n.vs/\n*.swp",
};

foreach (var (path, content) in sampleFiles)
{
    var fullPath = Path.Combine(workspaceDir, path);
    File.WriteAllText(fullPath, content);
}

// ── State ─────────────────────────────────────────────────────
// Track open documents and tabs
var openDocs = new Dictionary<string, (Hex1bDocument doc, EditorState textState, EditorState hexState)>();
var openTabs = new List<string>(); // ordered list of open file paths (relative)
var activeTab = 0;

(Hex1bDocument doc, EditorState textState, EditorState hexState) OpenFile(string relativePath)
{
    if (!openDocs.ContainsKey(relativePath))
    {
        var fullPath = Path.Combine(workspaceDir, relativePath);
        var content = File.Exists(fullPath) ? File.ReadAllText(fullPath) : "";
        var doc = new Hex1bDocument(content);
        var textState = new EditorState(doc);
        var hexState = new EditorState(doc) { IsReadOnly = true };
        openDocs[relativePath] = (doc, textState, hexState);
    }

    if (!openTabs.Contains(relativePath))
    {
        openTabs.Add(relativePath);
    }

    activeTab = openTabs.IndexOf(relativePath);
    return openDocs[relativePath];
}

// Open README by default
OpenFile("README.md");

// ── Build file tree structure ─────────────────────────────────
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
                    m2.MenuItem("New File"),
                    m2.MenuItem("Open..."),
                    m2.Separator(),
                    m2.MenuItem("Save").OnActivated(_ =>
                    {
                        if (activeTab >= 0 && activeTab < openTabs.Count)
                        {
                            var path = openTabs[activeTab];
                            if (openDocs.TryGetValue(path, out var entry))
                            {
                                File.WriteAllText(
                                    Path.Combine(workspaceDir, path),
                                    entry.doc.GetText());
                            }
                        }
                    }),
                    m2.MenuItem("Save All"),
                    m2.Separator(),
                    m2.MenuItem("Exit").OnActivated(_ => app.RequestStop()),
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
                    m2.MenuItem("Select All"),
                ]),
                m.Menu("View", m2 =>
                [
                    m2.MenuItem("Explorer"),
                    m2.MenuItem("Terminal"),
                    m2.Separator(),
                    m2.MenuItem("Zoom In"),
                    m2.MenuItem("Zoom Out"),
                ]),
                m.Menu("Help", m2 =>
                [
                    m2.MenuItem("About"),
                ]),
            ]).ContentHeight(),

            // ── Main content: tree + editor tabs ──
            v.HStack(h =>
            [
                // Left panel: file tree
                h.VStack(left =>
                [
                    left.Text(" EXPLORER").ContentHeight(),
                    left.Tree(tc => BuildTreeItems(tc, rootEntry.Children))
                        .OnItemActivated(e =>
                        {
                            var relPath = FindRelativePath(rootEntry, e.Item.Label);
                            if (relPath != null && !relPath.EndsWith(Path.DirectorySeparatorChar))
                            {
                                OpenFile(relPath);
                            }
                        })
                        .FillHeight(),
                ]).FixedWidth(28),

                // Right panel: tab panel with editors
                h.VStack(right =>
                {
                    if (openTabs.Count == 0)
                    {
                        return
                        [
                            right.Text("  Open a file from the explorer to begin editing.")
                                .FillWidth().FillHeight()
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

                                tabs.Add(tc.Tab(tabName, content =>
                                [
                                    content.HStack(editors =>
                                    [
                                        editors.Editor(textState).FillWidth().FillHeight(),
                                        editors.Editor(hexState)
                                            .WithViewRenderer(HexEditorViewRenderer.Instance)
                                            .FillWidth().FillHeight(),
                                    ]).FillWidth().FillHeight()
                                ]));
                            }
                            return tabs;
                        })
                        .OnSelectionChanged(e => { activeTab = e.SelectedIndex; })
                        .FillWidth().FillHeight()
                    ];
                }).FillWidth().FillHeight(),
            ]).FillWidth().FillHeight(),

            // ── Info bar (status bar) ──
            v.InfoBar(ib =>
            {
                var items = new List<IInfoBarChild>();
                items.Add(ib.Section("hex1b editor"));
                items.Add(ib.Separator());

                if (activeTab >= 0 && activeTab < openTabs.Count)
                {
                    var path = openTabs[activeTab];
                    items.Add(ib.Section(path));

                    if (openDocs.TryGetValue(path, out var entry))
                    {
                        items.Add(ib.Spacer());
                        var pos = entry.doc.OffsetToPosition(entry.textState.Cursor.Position);
                        items.Add(ib.Section($"Ln {pos.Line}, Col {pos.Column}"));
                        items.Add(ib.Separator());
                        items.Add(ib.Section($"{entry.doc.Length} chars"));
                        items.Add(ib.Separator());
                        items.Add(ib.Section("UTF-8"));
                    }
                }
                else
                {
                    items.Add(ib.Section("No file open"));
                }

                return items;
            }).ContentHeight(),
        ]);
    })
    .Build();

await terminal.RunAsync();

// ── Cleanup ───────────────────────────────────────────────────
try { Directory.Delete(workspaceDir, true); } catch { }

// ── Helper functions ──────────────────────────────────────────
static IEnumerable<TreeItemWidget> BuildTreeItems(TreeContext tc, List<FileEntry> entries)
{
    foreach (var entry in entries)
    {
        if (entry.IsDirectory)
        {
            yield return tc.Item(entry.Name, sub => BuildTreeItems(sub, entry.Children))
                .Icon("📁");
        }
        else
        {
            var icon = entry.Name switch
            {
                _ when entry.Name.EndsWith(".cs") => "🔷",
                _ when entry.Name.EndsWith(".md") => "📝",
                _ when entry.Name.EndsWith(".json") => "⚙️",
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

// ── Types ─────────────────────────────────────────────────────
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
