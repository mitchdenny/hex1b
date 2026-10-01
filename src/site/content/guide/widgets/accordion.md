<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode    → src/Hex1b.Website/Examples/AccordionBasicExample.cs
  - actionsCode  → src/Hex1b.Website/Examples/AccordionActionsExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import sectionsSnippet from './snippets/accordion-sections.cs?raw'
import actionsSnippet from './snippets/accordion-actions.cs?raw'
import toggleSnippet from './snippets/accordion-toggle.cs?raw'

const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Accordion(a => [
        a.Section(s => [
            s.Text("  src/"),
            s.Text("    Program.cs"),
            s.Text("    Utils.cs"),
            s.Text("    Models/"),
        ]).Title("EXPLORER"),

        a.Section(s => [
            s.Text("  ▸ Properties"),
            s.Text("  ▸ Methods"),
            s.Text("  ▸ Fields"),
        ]).Title("OUTLINE"),

        a.Section(s => [
            s.Text("  ● Updated README.md"),
            s.Text("  ● Fixed build script"),
        ]).Title("TIMELINE"),
    ]))
    .Build();

await terminal.RunAsync();`

const actionsCode = `using Hex1b;

var statusMessage = "Ready";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Accordion(a => [
            a.Section(s => [
                s.Text("  src/"),
                s.Text("    Program.cs"),
                s.Text("    Utils.cs"),
            ]).Title("EXPLORER")
            .RightActions(ra => [
                ra.Icon("+").OnClick(_ => statusMessage = "New file..."),
                ra.Icon("⟳").OnClick(_ => statusMessage = "Refreshed"),
            ]),

            a.Section(s => [
                s.Text("  ▸ Properties"),
                s.Text("  ▸ Methods"),
            ]).Title("OUTLINE")
            .RightActions(ra => [
                ra.Icon("⟳").OnClick(_ => statusMessage = "Outline refreshed"),
            ]),

            a.Section(s => [
                s.Text("  main"),
                s.Text("  develop"),
            ]).Title("SOURCE CONTROL")
            .LeftActions(la => [
                la.Toggle("▶", "▼"),
                la.Icon("✓").OnClick(_ => statusMessage = "Committed"),
            ])
            .RightActions(ra => [
                ra.Icon("⟳").OnClick(_ => statusMessage = "Pulling..."),
            ]),
        ]),
        v.Text(""),
        v.Text($" Status: {statusMessage}"),
    ]))
    .Build();

await terminal.RunAsync();`

</script>

# Accordion

A collapsible section container inspired by IDE sidebars. Each section has a clickable header that expands or collapses its content area. By default, only one section can be expanded at a time.

## Basic Usage

Create an accordion with named sections using the builder pattern. The first section is expanded by default:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="accordion-basic" exampleTitle="Accordion - Basic Usage" />

## Sections

Each section has a title header and content area. Content is built using the same widget context as a VStack:

<StaticTerminalPreview svgPath="/svg/accordion-sections.svg" :code="sectionsSnippet" />

Key behavior:
- The first section is expanded by default
- Only one section can be expanded at a time (single-expand mode)
- Use `.Expanded()` to set the initial expanded state of a specific section
- Use `.MultipleExpanded()` on the accordion to allow multiple open sections

## Section Actions

Add interactive icons to section headers using `.LeftActions()` and `.RightActions()`. Actions receive an `AccordionSectionActionContext` that can control the section's expand state:

<CodeBlock lang="csharp" :code="actionsCode" command="dotnet run" example="accordion-actions" exampleTitle="Accordion - Action Icons" />

### Right Actions

Add action icons to the right side of a section header:

<StaticTerminalPreview svgPath="/svg/accordion-actions.svg" :code="actionsSnippet" />

### Custom Toggle Icons

By default, a chevron toggle (▾/▸) is prepended to the left side of every section header. Override it with `.LeftActions()` and the `Toggle()` factory method:

<StaticTerminalPreview svgPath="/svg/accordion-toggle.svg" :code="toggleSnippet" />

The `AccordionSectionActionBuilder` provides these factory methods:

| Method | Description |
|--------|-------------|
| `Toggle(collapsed?, expanded?)` | Expand/collapse toggle with state-dependent icon. Uses theme chevrons by default |
| `Icon(string)` | Simple icon with optional `.OnClick()` handler |
| `Collapse(icon?)` | Collapses the section on click (default icon: "−") |
| `Expand(icon?)` | Expands the section on click (default icon: "+") |

### Action Context

Click handlers receive an `AccordionSectionActionContext` with these methods:

| Member | Description |
|--------|-------------|
| `IsExpanded` | Whether this section is currently expanded |
| `Expand()` | Expands this section |
| `Collapse()` | Collapses this section |
| `Toggle()` | Toggles the expand/collapse state |

## Events

React to section expand/collapse changes:

```csharp
ctx.Accordion(a => [...])
    .OnSectionExpanded(e =>
    {
        Console.WriteLine($"{e.SectionTitle} is now {(e.IsExpanded ? "expanded" : "collapsed")}");
    })
```

## Theming

Customize the accordion appearance via `AccordionTheme`:

| Element | Default | Description |
|---------|---------|-------------|
| `HeaderForegroundColor` | Default | Header text color |
| `HeaderBackgroundColor` | Default | Header background |
| `FocusedHeaderForegroundColor` | Black | Focused header text |
| `FocusedHeaderBackgroundColor` | White | Focused header background |
| `ExpandedChevron` | ▾ | Character for expanded state |
| `CollapsedChevron` | ▸ | Character for collapsed state |

## Keyboard Navigation

| Key | Action |
|-----|--------|
| `↑` / `↓` | Navigate between section headers |
| `Enter` / `Space` | Toggle the focused section |
| `Tab` | Move focus to next focusable element |
| `Shift+Tab` | Move focus to previous focusable element |

## Layout

The accordion fills available vertical space by default (`HeightHint = SizeHint.Fill`). Expanded section content fills the remaining height after all headers are accounted for. When content widgets already fill vertical space (e.g., a `TreeWidget` with `.FillHeight()`), the accordion respects that without adding extra spacers.

## Related Widgets

- [TabPanel](/guide/widgets/tabpanel) - Tabbed interface for switching between content panels
- [Tree](/guide/widgets/tree) - Hierarchical data display (often used inside accordion sections)
- [Splitter](/guide/widgets/splitter) - Resizable split views (commonly pairs with accordion sidebar)
- [List](/guide/widgets/list) - Selectable item lists
