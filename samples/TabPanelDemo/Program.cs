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

// Initialize fake file system with realistic project structure
// Core source files
editorState.Files.Add(new SourceFile("Program.cs", "📄", GenerateCSharpContent("Program")));
editorState.Files.Add(new SourceFile("Startup.cs", "📄", GenerateCSharpContent("Startup")));

// Controllers
editorState.Files.Add(new SourceFile("HomeController.cs", "📄", GenerateCSharpContent("HomeController")));
editorState.Files.Add(new SourceFile("UsersController.cs", "📄", GenerateCSharpContent("UsersController")));
editorState.Files.Add(new SourceFile("ProductsController.cs", "📄", GenerateCSharpContent("ProductsController")));
editorState.Files.Add(new SourceFile("OrdersController.cs", "📄", GenerateCSharpContent("OrdersController")));
editorState.Files.Add(new SourceFile("AuthController.cs", "📄", GenerateCSharpContent("AuthController")));

// Services
editorState.Files.Add(new SourceFile("UserService.cs", "📄", GenerateCSharpContent("UserService")));
editorState.Files.Add(new SourceFile("ProductService.cs", "📄", GenerateCSharpContent("ProductService")));
editorState.Files.Add(new SourceFile("OrderService.cs", "📄", GenerateCSharpContent("OrderService")));
editorState.Files.Add(new SourceFile("EmailService.cs", "📄", GenerateCSharpContent("EmailService")));
editorState.Files.Add(new SourceFile("CacheService.cs", "📄", GenerateCSharpContent("CacheService")));

// Models
editorState.Files.Add(new SourceFile("User.cs", "📄", GenerateCSharpContent("User")));
editorState.Files.Add(new SourceFile("Product.cs", "📄", GenerateCSharpContent("Product")));
editorState.Files.Add(new SourceFile("Order.cs", "📄", GenerateCSharpContent("Order")));
editorState.Files.Add(new SourceFile("OrderItem.cs", "📄", GenerateCSharpContent("OrderItem")));
editorState.Files.Add(new SourceFile("Address.cs", "📄", GenerateCSharpContent("Address")));

// Data access
editorState.Files.Add(new SourceFile("AppDbContext.cs", "📄", GenerateCSharpContent("AppDbContext")));
editorState.Files.Add(new SourceFile("UserRepository.cs", "📄", GenerateCSharpContent("UserRepository")));
editorState.Files.Add(new SourceFile("ProductRepository.cs", "📄", GenerateCSharpContent("ProductRepository")));
editorState.Files.Add(new SourceFile("OrderRepository.cs", "📄", GenerateCSharpContent("OrderRepository")));

// Interfaces
editorState.Files.Add(new SourceFile("IUserService.cs", "📄", GenerateCSharpContent("IUserService")));
editorState.Files.Add(new SourceFile("IProductService.cs", "📄", GenerateCSharpContent("IProductService")));
editorState.Files.Add(new SourceFile("IOrderService.cs", "📄", GenerateCSharpContent("IOrderService")));
editorState.Files.Add(new SourceFile("IRepository.cs", "📄", GenerateCSharpContent("IRepository")));

// Middleware & Extensions
editorState.Files.Add(new SourceFile("AuthMiddleware.cs", "📄", GenerateCSharpContent("AuthMiddleware")));
editorState.Files.Add(new SourceFile("LoggingMiddleware.cs", "📄", GenerateCSharpContent("LoggingMiddleware")));
editorState.Files.Add(new SourceFile("ServiceExtensions.cs", "📄", GenerateCSharpContent("ServiceExtensions")));

// DTOs
editorState.Files.Add(new SourceFile("UserDto.cs", "📄", GenerateCSharpContent("UserDto")));
editorState.Files.Add(new SourceFile("ProductDto.cs", "📄", GenerateCSharpContent("ProductDto")));
editorState.Files.Add(new SourceFile("OrderDto.cs", "📄", GenerateCSharpContent("OrderDto")));
editorState.Files.Add(new SourceFile("LoginRequest.cs", "📄", GenerateCSharpContent("LoginRequest")));
editorState.Files.Add(new SourceFile("LoginResponse.cs", "📄", GenerateCSharpContent("LoginResponse")));

// Tests
editorState.Files.Add(new SourceFile("UserServiceTests.cs", "📄", GenerateCSharpContent("UserServiceTests")));
editorState.Files.Add(new SourceFile("ProductServiceTests.cs", "📄", GenerateCSharpContent("ProductServiceTests")));
editorState.Files.Add(new SourceFile("OrderServiceTests.cs", "📄", GenerateCSharpContent("OrderServiceTests")));
editorState.Files.Add(new SourceFile("IntegrationTests.cs", "📄", GenerateCSharpContent("IntegrationTests")));

// Markdown docs
editorState.Files.Add(new SourceFile("README.md", "📝", GenerateReadmeContent()));
editorState.Files.Add(new SourceFile("CONTRIBUTING.md", "📝", GenerateMarkdownContent("Contributing Guide")));
editorState.Files.Add(new SourceFile("CHANGELOG.md", "📝", GenerateMarkdownContent("Changelog")));
editorState.Files.Add(new SourceFile("API.md", "📝", GenerateMarkdownContent("API Documentation")));
editorState.Files.Add(new SourceFile("ARCHITECTURE.md", "📝", GenerateMarkdownContent("Architecture")));
editorState.Files.Add(new SourceFile("DEPLOYMENT.md", "📝", GenerateMarkdownContent("Deployment Guide")));

// Config files
editorState.Files.Add(new SourceFile("appsettings.json", "⚙️", GenerateJsonContent()));
editorState.Files.Add(new SourceFile("appsettings.Development.json", "⚙️", GenerateJsonContent()));
editorState.Files.Add(new SourceFile("appsettings.Production.json", "⚙️", GenerateJsonContent()));
editorState.Files.Add(new SourceFile("launchSettings.json", "⚙️", GenerateJsonContent()));
editorState.Files.Add(new SourceFile("package.json", "⚙️", GenerateJsonContent()));
editorState.Files.Add(new SourceFile("tsconfig.json", "⚙️", GenerateJsonContent()));
editorState.Files.Add(new SourceFile("Directory.Build.props", "📦", GeneratePropsContent()));
editorState.Files.Add(new SourceFile("Directory.Packages.props", "📦", GeneratePropsContent()));

// Other files
editorState.Files.Add(new SourceFile("LICENSE", "📜", GenerateLicenseContent()));
editorState.Files.Add(new SourceFile(".gitignore", "📄", "bin/\nobj/\n*.user\n.vs/\nnode_modules/"));
editorState.Files.Add(new SourceFile(".editorconfig", "📄", "root = true\n\n[*]\nindent_style = space\nindent_size = 4"));
editorState.Files.Add(new SourceFile("Dockerfile", "🐳", "FROM mcr.microsoft.com/dotnet/aspnet:8.0\nWORKDIR /app\nCOPY . .\nENTRYPOINT [\"dotnet\", \"MyApp.dll\"]"));
editorState.Files.Add(new SourceFile("docker-compose.yml", "🐳", "version: '3.8'\nservices:\n  app:\n    build: .\n    ports:\n      - '8080:80'"));

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
                                .Selected(idx == editorState.SelectedTabIndex)
                                .WithRightActions(i => [
                                    i.Icon("×").OnClick(e => {
                                        editorState.CloseDocument(idx);
                                        statusMessage = $"Closed: {doc.File.Name}";
                                    })
                                ])
                            )
                        ])
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
                                .Selected(idx == editorState.SelectedTabIndex)
                                .WithRightActions(i => [
                                    i.Icon("×").OnClick(e => {
                                        editorState.CloseDocument(idx);
                                        statusMessage = $"Closed: {doc.File.Name}";
                                    })
                                ])
                            )
                        ])
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
