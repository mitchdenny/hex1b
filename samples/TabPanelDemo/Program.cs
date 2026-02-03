using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;

// =============================================================================
// TabPanelDemo - Mini IDE-like demo showcasing TabPanel with Tree and MenuBar
// =============================================================================

// Content generator local functions (must be before usage in top-level statements)
static string GenerateCSharpContent() => """
using System;
using Hex1b;
using Hex1b.Widgets;

namespace TabPanelDemo;

/// <summary>
/// Main entry point for the TabPanel demonstration application.
/// This showcases the composable nature of Hex1b widgets.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => BuildUI(app))
            .Build();

        await terminal.RunAsync();
    }

    private static Hex1bWidget BuildUI(Hex1bApp app)
    {
        // Build the main user interface
        return new VStackWidget([
            new TextBlockWidget("Hello, Hex1b!"),
            new ButtonWidget("Click Me")
        ]);
    }
}
""";

static string GenerateHex1bAppContent() => """
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hex1b;

/// <summary>
/// The main application class that manages the widget tree,
/// reconciliation, and render loop for a Hex1b terminal UI.
/// </summary>
public class Hex1bApp : IDisposable
{
    private readonly Func<WidgetContext, Task<Hex1bWidget>> _builder;
    private readonly Hex1bAppOptions _options;
    private Hex1bNode? _rootNode;

    public Hex1bApp(
        Func<WidgetContext, Task<Hex1bWidget>> builder,
        Hex1bAppOptions options)
    {
        _builder = builder;
        _options = options;
    }

    /// <summary>
    /// Runs the application render loop until cancelled.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            await RenderFrameAsync(ct);
            await WaitForInputAsync(ct);
        }
    }

    public void Dispose()
    {
        // Cleanup resources
    }
}
""";

static string GenerateReadmeContent() => """
# TabPanel Demo

A demonstration of the Hex1b TabPanel widget showcasing a mini IDE-like experience.

## Features

- **Menu Bar**: Standard File, Edit, View, Help menus
- **File Explorer**: Tree view with categorized files
- **Tab Panel**: Open multiple files in tabs
- **Status Bar**: Shows current status and tab info

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Alt+Right | Next tab |
| Alt+Left | Previous tab |
| Tab | Navigate forward |
| Shift+Tab | Navigate backward |
| Escape | Close menu |

## Getting Started

1. Click on a file in the Explorer panel
2. The file opens in a new tab
3. Use Alt+Right/Left to switch between tabs
4. Use File > Close Tab to close the current tab

## Architecture

The demo uses several Hex1b widgets:
- `MenuBar` for the application menu
- `HSplitter` to divide the workspace
- `Tree` for the file explorer
- `TabPanel` for the editor tabs
- `InfoBar` for the status bar
""";

static string GenerateJsonContent() => """
{
  "Application": {
    "Name": "TabPanel Demo",
    "Version": "1.0.0",
    "Description": "A mini IDE-like demo for Hex1b"
  },
  "Editor": {
    "WordWrap": true,
    "TabSize": 4,
    "ShowLineNumbers": true,
    "Theme": "default"
  },
  "FileExplorer": {
    "ShowHiddenFiles": false,
    "SortBy": "name",
    "GroupByType": true
  },
  "Tabs": {
    "MaxTabs": 10,
    "CloseOnMiddleClick": true,
    "ShowCloseButton": true
  }
}
""";

static string GeneratePropsContent() => """
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <PropertyGroup>
    <Authors>Hex1b Team</Authors>
    <Company>Hex1b</Company>
    <Product>TabPanel Demo</Product>
    <Copyright>© 2024 Hex1b</Copyright>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Hex1b" Version="1.0.0" />
  </ItemGroup>
</Project>
""";

static string GenerateLicenseContent() => """
MIT License

Copyright (c) 2024 Hex1b

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
""";

// Editor state
var editorState = new EditorState();

// Initialize fake file system
editorState.Files.Add(new SourceFile("Program.cs", "📄", GenerateCSharpContent()));
editorState.Files.Add(new SourceFile("Hex1bApp.cs", "📄", GenerateHex1bAppContent()));
editorState.Files.Add(new SourceFile("README.md", "📝", GenerateReadmeContent()));
editorState.Files.Add(new SourceFile("appsettings.json", "⚙️", GenerateJsonContent()));
editorState.Files.Add(new SourceFile("Directory.Build.props", "📦", GeneratePropsContent()));
editorState.Files.Add(new SourceFile("LICENSE", "📜", GenerateLicenseContent()));

var statusMessage = "Ready";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx =>
    ctx.VStack(main => [
        // ─────────────────────────────────────────────────────────────────
        // MENU BAR
        // ─────────────────────────────────────────────────────────────────
        main.MenuBar(m => [
            m.Menu("File", m => [
                m.MenuItem("New File").OnActivated(e => {
                    statusMessage = "New file created";
                }),
                m.MenuItem("Open...").OnActivated(e => {
                    statusMessage = "Open dialog would appear";
                }),
                m.Separator(),
                m.MenuItem("Save").OnActivated(e => {
                    if (editorState.OpenDocuments.Count > 0)
                    {
                        var current = editorState.OpenDocuments[editorState.SelectedTabIndex];
                        statusMessage = $"Saved: {current.Name}";
                    }
                }),
                m.MenuItem("Save All").OnActivated(e => {
                    statusMessage = $"Saved {editorState.OpenDocuments.Count} files";
                }),
                m.Separator(),
                m.MenuItem("Close Tab").OnActivated(e => {
                    if (editorState.OpenDocuments.Count > 0)
                    {
                        editorState.CloseDocument(editorState.SelectedTabIndex);
                        statusMessage = "Tab closed";
                    }
                }),
                m.MenuItem("Close All Tabs").OnActivated(e => {
                    editorState.OpenDocuments.Clear();
                    editorState.SelectedTabIndex = 0;
                    statusMessage = "All tabs closed";
                }),
                m.Separator(),
                m.MenuItem("Exit").OnActivated(e => e.Context.RequestStop())
            ]),
            m.Menu("Edit", m => [
                m.MenuItem("Undo").Disabled(),
                m.MenuItem("Redo").Disabled(),
                m.Separator(),
                m.MenuItem("Cut").OnActivated(e => statusMessage = "Cut"),
                m.MenuItem("Copy").OnActivated(e => statusMessage = "Copy"),
                m.MenuItem("Paste").OnActivated(e => statusMessage = "Paste"),
                m.Separator(),
                m.MenuItem("Find...").OnActivated(e => statusMessage = "Find dialog")
            ]),
            m.Menu("View", m => [
                m.MenuItem("Explorer").OnActivated(e => statusMessage = "Showing Explorer"),
                m.MenuItem("Problems").OnActivated(e => statusMessage = "Showing Problems"),
                m.MenuItem("Output").OnActivated(e => statusMessage = "Showing Output"),
                m.Separator(),
                m.MenuItem("Word Wrap").OnActivated(e => statusMessage = "Toggle word wrap")
            ]),
            m.Menu("Help", m => [
                m.MenuItem("Documentation").OnActivated(e => statusMessage = "Opening docs..."),
                m.MenuItem("Keyboard Shortcuts").OnActivated(e => statusMessage = "Alt+Right/Left: Switch tabs"),
                m.Separator(),
                m.MenuItem("About").OnActivated(e => statusMessage = "TabPanel Demo v1.0")
            ])
        ]),

        // ─────────────────────────────────────────────────────────────────
        // MAIN CONTENT - HSplitter with Tree on left, Editor on right
        // ─────────────────────────────────────────────────────────────────
        main.HSplitter(
            // LEFT PANE - File Explorer Tree
            left => [
                left.Text(" EXPLORER"),
                left.Separator(),
                left.Tree(t => [
                    t.Item("src", items => [
                        ..editorState.Files
                            .Where(f => f.Name.EndsWith(".cs"))
                            .Select(f => t.Item(f.Name)
                                .Icon(f.Icon)
                                .OnActivated(e => {
                                    editorState.OpenDocument(f);
                                    statusMessage = $"Opened: {f.Name}";
                                }))
                    ]).Icon("📁").Expanded(),
                    t.Item("docs", items => [
                        ..editorState.Files
                            .Where(f => f.Name.EndsWith(".md"))
                            .Select(f => t.Item(f.Name)
                                .Icon(f.Icon)
                                .OnActivated(e => {
                                    editorState.OpenDocument(f);
                                    statusMessage = $"Opened: {f.Name}";
                                }))
                    ]).Icon("📁"),
                    t.Item("config", items => [
                        ..editorState.Files
                            .Where(f => f.Name.EndsWith(".json") || f.Name.EndsWith(".props"))
                            .Select(f => t.Item(f.Name)
                                .Icon(f.Icon)
                                .OnActivated(e => {
                                    editorState.OpenDocument(f);
                                    statusMessage = $"Opened: {f.Name}";
                                }))
                    ]).Icon("📁"),
                    ..editorState.Files
                        .Where(f => !f.Name.EndsWith(".cs") && !f.Name.EndsWith(".md") && 
                                   !f.Name.EndsWith(".json") && !f.Name.EndsWith(".props"))
                        .Select(f => t.Item(f.Name)
                            .Icon(f.Icon)
                            .OnActivated(e => {
                                editorState.OpenDocument(f);
                                statusMessage = $"Opened: {f.Name}";
                            }))
                ]).Fill()
            ],
            // RIGHT PANE - Tab Panel with Editor Content
            right => editorState.OpenDocuments.Count == 0
                ? [
                    // Empty state when no documents open
                    right.Center(c => c.VStack(empty => [
                        empty.Text(""),
                        empty.Text("No files open"),
                        empty.Text(""),
                        empty.Text("Select a file from the Explorer"),
                        empty.Text("to open it in the editor."),
                        empty.Text(""),
                        empty.Text("Keyboard shortcuts:"),
                        empty.Text("  Alt+Right/Left - Switch tabs"),
                        empty.Text("  Escape - Close menu")
                    ])).Fill()
                ]
                : [
                    // TabPanel with open documents - responsive Full/Compact mode
                    right.Responsive(r => [
                        // Full mode when height >= 15
                        r.When((w, h) => h >= 15, r => r.TabPanel(tp => [
                            ..editorState.OpenDocuments.Select(doc =>
                                tp.Tab(doc.Name, t => [
                                    t.VScroll(s => [
                                        s.Text(doc.Content).Wrap()
                                    ]).Fill()
                                ])
                            )
                        ])
                        .WithSelectedIndex(editorState.SelectedTabIndex)
                        .OnSelectionChanged(e => {
                            editorState.SelectedTabIndex = e.SelectedIndex;
                            statusMessage = $"Viewing: {e.SelectedTitle}";
                        })
                        .Full()
                        .Fill()),
                        // Compact mode when height < 15
                        r.Otherwise(r => r.TabPanel(tp => [
                            ..editorState.OpenDocuments.Select(doc =>
                                tp.Tab(doc.Name, t => [
                                    t.VScroll(s => [
                                        s.Text(doc.Content).Wrap()
                                    ]).Fill()
                                ])
                            )
                        ])
                        .WithSelectedIndex(editorState.SelectedTabIndex)
                        .OnSelectionChanged(e => {
                            editorState.SelectedTabIndex = e.SelectedIndex;
                            statusMessage = $"Viewing: {e.SelectedTitle}";
                        })
                        .Compact()
                        .Fill())
                    ]).Fill()
                ],
            leftWidth: 25
        ).Fill(),

        // ─────────────────────────────────────────────────────────────────
        // STATUS BAR
        // ─────────────────────────────────────────────────────────────────
        main.InfoBar(s => [
            s.Section(statusMessage),
            s.Spacer(),
            s.Section(editorState.OpenDocuments.Count > 0 
                ? $"Tab {editorState.SelectedTabIndex + 1}/{editorState.OpenDocuments.Count}" 
                : ""),
            s.Separator(" | "),
            s.Section("Alt+←/→: Switch tabs")
        ])
    ]))
    .Build();

await terminal.RunAsync();

// =============================================================================
// State and Data Classes
// =============================================================================

class EditorState
{
    public List<SourceFile> Files { get; } = new();
    public List<SourceFile> OpenDocuments { get; } = new();
    public int SelectedTabIndex { get; set; }

    public void OpenDocument(SourceFile file)
    {
        // Check if already open
        var existingIndex = OpenDocuments.FindIndex(d => d.Name == file.Name);
        if (existingIndex >= 0)
        {
            SelectedTabIndex = existingIndex;
            return;
        }

        // Add and select
        OpenDocuments.Add(file);
        SelectedTabIndex = OpenDocuments.Count - 1;
    }

    public void CloseDocument(int index)
    {
        if (index >= 0 && index < OpenDocuments.Count)
        {
            OpenDocuments.RemoveAt(index);
            if (SelectedTabIndex >= OpenDocuments.Count)
            {
                SelectedTabIndex = Math.Max(0, OpenDocuments.Count - 1);
            }
        }
    }
}

record SourceFile(string Name, string Icon, string Content);
