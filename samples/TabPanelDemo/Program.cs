using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;

// =============================================================================
// TabPanelDemo - Mini IDE-like demo showcasing TabPanel with Tree and MenuBar
// =============================================================================

// Content generator local functions (must be before usage in top-level statements)
static string GenerateCSharpContent(string className = "Program") => $$"""
using System;
using Hex1b;
using Hex1b.Widgets;

namespace TabPanelDemo;

/// <summary>
/// {{className}} - Auto-generated class for demonstration purposes.
/// This showcases the composable nature of Hex1b widgets.
/// </summary>
public class {{className}}
{
    private readonly string _name = "{{className}}";
    private int _counter;

    public {{className}}()
    {
        _counter = 0;
    }

    public void DoSomething()
    {
        _counter++;
        Console.WriteLine($"{_name} counter: {_counter}");
    }

    public static async Task Main(string[] args)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp((app, options) => BuildUI(app))
            .Build();

        await terminal.RunAsync();
    }

    private static Hex1bWidget BuildUI(Hex1bApp app)
    {
        return new VStackWidget([
            new TextBlockWidget("Hello from {{className}}!"),
            new ButtonWidget("Click Me")
        ]);
    }
}
""";

static string GenerateMarkdownContent(string title) => $"""
# {title}

This is auto-generated documentation for **{title}**.

## Overview

Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod 
tempor incididunt ut labore et dolore magna aliqua.

## Features

- Feature one with detailed description
- Feature two that does something useful  
- Feature three for advanced users

## Usage

```csharp
var example = new {title.Replace(" ", "")}();
example.DoSomething();
```

## See Also

- Related documentation
- API reference
- Examples
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

// Initialize fake file system with many files
// Source files (.cs)
for (int i = 1; i <= 50; i++)
{
    editorState.Files.Add(new SourceFile($"Class{i:D3}.cs", "📄", GenerateCSharpContent($"Class{i:D3}")));
}
editorState.Files.Add(new SourceFile("Program.cs", "📄", GenerateCSharpContent("Program")));
editorState.Files.Add(new SourceFile("Hex1bApp.cs", "📄", GenerateCSharpContent("Hex1bApp")));

// Markdown docs (.md)
for (int i = 1; i <= 30; i++)
{
    editorState.Files.Add(new SourceFile($"Doc{i:D2}.md", "📝", GenerateMarkdownContent($"Document {i}")));
}
editorState.Files.Add(new SourceFile("README.md", "📝", GenerateReadmeContent()));
editorState.Files.Add(new SourceFile("CONTRIBUTING.md", "📝", GenerateMarkdownContent("Contributing Guide")));
editorState.Files.Add(new SourceFile("CHANGELOG.md", "📝", GenerateMarkdownContent("Changelog")));

// Config files (.json, .props)
for (int i = 1; i <= 20; i++)
{
    editorState.Files.Add(new SourceFile($"config{i:D2}.json", "⚙️", GenerateJsonContent()));
}
editorState.Files.Add(new SourceFile("appsettings.json", "⚙️", GenerateJsonContent()));
editorState.Files.Add(new SourceFile("appsettings.Development.json", "⚙️", GenerateJsonContent()));
editorState.Files.Add(new SourceFile("Directory.Build.props", "📦", GeneratePropsContent()));
editorState.Files.Add(new SourceFile("Directory.Packages.props", "📦", GeneratePropsContent()));

// Other files
editorState.Files.Add(new SourceFile("LICENSE", "📜", GenerateLicenseContent()));
editorState.Files.Add(new SourceFile(".gitignore", "📄", "bin/\nobj/\n*.user\n.vs/"));
editorState.Files.Add(new SourceFile(".editorconfig", "📄", "root = true\n\n[*]\nindent_style = space"));

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
                        statusMessage = $"Saved: {current.File.Name}";
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
                left.VScroll(scroll => [
                    scroll.Tree(t => [
                        t.Item("src", items => [
                            ..editorState.Files
                                .Where(f => f.Name.EndsWith(".cs"))
                                .Select(f => t.Item(f.Name)
                                    .Icon(f.Icon)
                                    .OnClicked(e => {
                                        editorState.OpenDocument(f, keepOpen: false);
                                        statusMessage = $"Preview: {f.Name}";
                                    })
                                    .OnActivated(e => {
                                        editorState.OpenDocument(f, keepOpen: true);
                                        statusMessage = $"Opened: {f.Name}";
                                    }))
                        ]).Icon("📁").Expanded(),
                        t.Item("docs", items => [
                            ..editorState.Files
                                .Where(f => f.Name.EndsWith(".md"))
                                .Select(f => t.Item(f.Name)
                                    .Icon(f.Icon)
                                    .OnClicked(e => {
                                        editorState.OpenDocument(f, keepOpen: false);
                                        statusMessage = $"Preview: {f.Name}";
                                    })
                                    .OnActivated(e => {
                                        editorState.OpenDocument(f, keepOpen: true);
                                        statusMessage = $"Opened: {f.Name}";
                                    }))
                        ]).Icon("📁"),
                        t.Item("config", items => [
                            ..editorState.Files
                                .Where(f => f.Name.EndsWith(".json") || f.Name.EndsWith(".props"))
                                .Select(f => t.Item(f.Name)
                                    .Icon(f.Icon)
                                    .OnClicked(e => {
                                        editorState.OpenDocument(f, keepOpen: false);
                                        statusMessage = $"Preview: {f.Name}";
                                    })
                                    .OnActivated(e => {
                                        editorState.OpenDocument(f, keepOpen: true);
                                        statusMessage = $"Opened: {f.Name}";
                                    }))
                        ]).Icon("📁"),
                        ..editorState.Files
                            .Where(f => !f.Name.EndsWith(".cs") && !f.Name.EndsWith(".md") && 
                                       !f.Name.EndsWith(".json") && !f.Name.EndsWith(".props"))
                            .Select(f => t.Item(f.Name)
                                .Icon(f.Icon)
                                .OnClicked(e => {
                                    editorState.OpenDocument(f, keepOpen: false);
                                    statusMessage = $"Preview: {f.Name}";
                                })
                                .OnActivated(e => {
                                    editorState.OpenDocument(f, keepOpen: true);
                                    statusMessage = $"Opened: {f.Name}";
                                }))
                    ])
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
                            ..editorState.OpenDocuments.Select((doc, idx) =>
                                tp.Tab(doc.KeepOpen ? doc.File.Name : $"[{doc.File.Name}]", t => [
                                    t.VScroll(s => [
                                        s.Text(doc.File.Content).Wrap()
                                    ]).Fill()
                                ])
                                .WithRightIcons(i => [
                                    i.Icon("×").OnClick(e => {
                                        editorState.CloseDocument(idx);
                                        statusMessage = $"Closed: {doc.File.Name}";
                                    })
                                ])
                            )
                        ])
                        .WithSelectedIndex(editorState.SelectedTabIndex)
                        .OnSelectionChanged(e => {
                            editorState.SelectedTabIndex = e.SelectedIndex;
                            statusMessage = $"Viewing: {e.SelectedTitle}";
                        })
                        .Full()
                        .Selector()
                        .Fill()),
                        // Compact mode when height < 15
                        r.Otherwise(r => r.TabPanel(tp => [
                            ..editorState.OpenDocuments.Select((doc, idx) =>
                                tp.Tab(doc.KeepOpen ? doc.File.Name : $"[{doc.File.Name}]", t => [
                                    t.VScroll(s => [
                                        s.Text(doc.File.Content).Wrap()
                                    ]).Fill()
                                ])
                                .WithRightIcons(i => [
                                    i.Icon("×").OnClick(e => {
                                        editorState.CloseDocument(idx);
                                        statusMessage = $"Closed: {doc.File.Name}";
                                    })
                                ])
                            )
                        ])
                        .WithSelectedIndex(editorState.SelectedTabIndex)
                        .OnSelectionChanged(e => {
                            editorState.SelectedTabIndex = e.SelectedIndex;
                            statusMessage = $"Viewing: {e.SelectedTitle}";
                        })
                        .Compact()
                        .Selector()
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
    public List<OpenDocument> OpenDocuments { get; } = new();
    public int SelectedTabIndex { get; set; }

    public void OpenDocument(SourceFile file, bool keepOpen = false)
    {
        // Check if already open
        var existingIndex = OpenDocuments.FindIndex(d => d.File.Name == file.Name);
        if (existingIndex >= 0)
        {
            // If opening with keepOpen, upgrade the existing document
            if (keepOpen && !OpenDocuments[existingIndex].KeepOpen)
            {
                OpenDocuments[existingIndex] = OpenDocuments[existingIndex] with { KeepOpen = true };
            }
            SelectedTabIndex = existingIndex;
            return;
        }

        // If not keepOpen, replace existing preview tab (if any)
        if (!keepOpen)
        {
            var previewIndex = OpenDocuments.FindIndex(d => !d.KeepOpen);
            if (previewIndex >= 0)
            {
                OpenDocuments[previewIndex] = new OpenDocument(file, false);
                SelectedTabIndex = previewIndex;
                return;
            }
        }

        // Add new document
        OpenDocuments.Add(new OpenDocument(file, keepOpen));
        SelectedTabIndex = OpenDocuments.Count - 1;
    }

    public void KeepCurrentOpen()
    {
        if (SelectedTabIndex >= 0 && SelectedTabIndex < OpenDocuments.Count)
        {
            OpenDocuments[SelectedTabIndex] = OpenDocuments[SelectedTabIndex] with { KeepOpen = true };
        }
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
record OpenDocument(SourceFile File, bool KeepOpen);
