<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode      → src/Hex1b.Website/Examples/TabPanelBasicExample.cs
  - selectionCode  → src/Hex1b.Website/Examples/TabPanelSelectionExample.cs
  - dynamicCode    → src/Hex1b.Website/Examples/TabPanelDynamicExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import compactSnippet from './snippets/tabpanel-compact.cs?raw'

const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.TabPanel(tp => [
        tp.Tab("Overview", t => [
            t.Text("Welcome to Hex1b!"),
            t.Text(""),
            t.Text("This is the Overview tab content.")
        ]).Selected(),
        tp.Tab("Settings", t => [
            t.Text("Application Settings"),
            t.Text(""),
            t.Text("Configure your preferences here.")
        ]),
        tp.Tab("Help", t => [
            t.Text("Documentation and Support"),
            t.Text(""),
            t.Text("Visit hex1b.dev for more information.")
        ])
    ]).Selector().Fill())
    .Build();

await terminal.RunAsync();`

const selectionCode = `using Hex1b;

var state = new TabState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text($"Current tab: {state.SelectedTab}"),
        v.Text(""),
        v.TabPanel(tp => [
            tp.Tab("Documents", t => [
                t.Text("Your documents appear here")
            ]).Selected(state.SelectedTab == "Documents"),
            tp.Tab("Downloads", t => [
                t.Text("Your downloads appear here")
            ]).Selected(state.SelectedTab == "Downloads"),
            tp.Tab("Pictures", t => [
                t.Text("Your pictures appear here")
            ]).Selected(state.SelectedTab == "Pictures")
        ])
        .OnSelectionChanged(e => state.SelectedTab = e.SelectedTitle)
        .Selector()
        .Fill()
    ]))
    .Build();

await terminal.RunAsync();

class TabState
{
    public string SelectedTab { get; set; } = "Documents";
}`

const dynamicCode = `using Hex1b;

var state = new EditorState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.HStack(h => [
            h.Button("New Tab").OnClick(_ => state.AddTab()),
            h.Text($"  {state.Tabs.Count} tab(s) open")
        ]),
        v.Text(""),
        state.Tabs.Count == 0
            ? v.Text("No tabs open. Click 'New Tab' to add one.")
            : v.TabPanel(tp => state.Tabs.Select((tab, idx) =>
                tp.Tab(tab.Name, t => [
                    t.Text($"Content of {tab.Name}"),
                    t.Text(""),
                    t.Text($"Created at: {tab.CreatedAt:HH:mm:ss}")
                ])
                .Selected(idx == state.SelectedIndex)
                .WithRightActions(i => [
                    i.Icon("×").OnClick(_ => state.CloseTab(idx))
                ])
            ).ToArray())
            .OnSelectionChanged(e => state.SelectedIndex = e.SelectedIndex)
            .Selector()
            .Fill()
    ]))
    .Build();

await terminal.RunAsync();

class EditorState
{
    public List<TabInfo> Tabs { get; } = [];
    public int SelectedIndex { get; set; }
    private int _counter = 1;

    public void AddTab()
    {
        Tabs.Add(new TabInfo($"Tab {_counter++}", DateTime.Now));
        SelectedIndex = Tabs.Count - 1;
    }

    public void CloseTab(int index)
    {
        if (index >= 0 && index < Tabs.Count)
        {
            Tabs.RemoveAt(index);
            if (SelectedIndex >= Tabs.Count)
                SelectedIndex = Math.Max(0, Tabs.Count - 1);
        }
    }
}

record TabInfo(string Name, DateTime CreatedAt);`

</script>

# TabPanel

A tabbed interface for organizing content into switchable panels. TabPanel provides a complete solution with a tab bar, content area, and optional navigation controls.

## Basic Usage

Create a tabbed interface using the fluent API. Mark the initially selected tab with `.Selected()`:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="tabpanel-basic" exampleTitle="TabPanel - Basic Usage" />

## Selection with State

Track tab selection changes to update your application state. Use `.Selected(condition)` to declaratively set which tab is active:

<CodeBlock lang="csharp" :code="selectionCode" command="dotnet run" example="tabpanel-selection" exampleTitle="TabPanel - Selection Tracking" />

Key points:
- `.Selected()` marks a tab as initially selected
- `.Selected(condition)` enables dynamic selection based on state
- When multiple tabs have `.Selected(true)`, the first one wins
- Use `OnSelectionChanged` to respond to user tab switches

## Dynamic Tabs

Build tab-based interfaces where tabs are added and removed at runtime. This pattern is common for document editors, browser-like UIs, and multi-pane dashboards:

<CodeBlock lang="csharp" :code="dynamicCode" command="dotnet run" example="tabpanel-dynamic" exampleTitle="TabPanel - Dynamic Tabs" />

Features demonstrated:
- Adding tabs dynamically with state
- Close button using `.WithRightActions()`
- Selection tracking with `OnSelectionChanged`
- Conditional rendering when no tabs exist

## Render Modes

TabPanel supports two visual styles:

### Full Mode (Default)

Full mode displays visual separators above and below the tabs:

```csharp
tp.TabPanel(...).Full()
```

### Compact Mode

Compact mode shows only the tab row without separators, saving vertical space:

<StaticTerminalPreview svgPath="/svg/tabpanel-compact.svg" :code="compactSnippet" />

## Tab Bar Features

### Paging Arrows

When tabs overflow the available width, paging arrows (◀ ▶) appear automatically. Disable with:

```csharp
tp.TabPanel(...).Paging(false)
```

### Dropdown Selector

Enable a dropdown menu (▼) for quick navigation to any tab:

```csharp
tp.TabPanel(...).Selector()
```

### Mouse Wheel Scrolling

When the mouse is over the tab bar, use the scroll wheel to page through tabs without changing the selection.

## Tab Position

Tabs can appear at the top or bottom of the panel:

```csharp
// Explicit positioning
tp.TabPanel(...).TabsOnTop()
tp.TabPanel(...).TabsOnBottom()
```

By default, TabPanel auto-detects position based on its location in a VStack:
- First child → tabs on top
- Last child → tabs on bottom
- Otherwise → tabs on top

## Tab Icons

Add icons to tabs for visual identification:

```csharp
tp.Tab("Settings", t => [...]).WithIcon("⚙️")
```

## Tab Actions

Add interactive action icons to tabs using `.WithLeftActions()` and `.WithRightActions()`:

### Close Button (Right Action)

```csharp
tp.Tab("Document.cs", t => [...])
  .WithRightActions(a => [
      a.Icon("×").OnClick(_ => CloseDocument())
  ])
```

### Pin Button (Left Action)

```csharp
tp.Tab("Important.cs", t => [...])
  .WithLeftActions(a => [
      a.Icon("📌").OnClick(_ => TogglePin())
  ])
```

### Multiple Actions

```csharp
tp.Tab("Document.cs", t => [...])
  .WithLeftActions(a => [
      a.Icon("📌").OnClick(_ => TogglePin())
  ])
  .WithRightActions(a => [
      a.Icon("💾").OnClick(_ => Save()),
      a.Icon("×").OnClick(_ => Close())
  ])
```

## Keyboard Navigation

| Key | Action |
|-----|--------|
| `Alt+Right` | Switch to next tab |
| `Alt+Left` | Switch to previous tab |
| `Tab` | Move focus to next focusable element |
| `Shift+Tab` | Move focus to previous focusable element |

## Related Widgets

- [Navigator](/guide/widgets/navigator) - Stack-based page navigation
- [Splitter](/guide/widgets/splitter) - Resizable split views (often used with TabPanel)
- [Scroll](/guide/widgets/scroll) - Scrollable content within tabs
- [List](/guide/widgets/list) - Selectable item lists
