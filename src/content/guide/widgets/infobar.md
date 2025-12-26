<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode     → src/Hex1b.Website/Examples/InfoBarBasicExample.cs
  - sectionsCode  → src/Hex1b.Website/Examples/InfoBarSectionsExample.cs
  - colorsCode    → src/Hex1b.Website/Examples/InfoBarColorsExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import simpleSnippet from './snippets/infobar-simple.cs?raw'
import shortcutsSnippet from './snippets/infobar-shortcuts.cs?raw'
import colorsSnippet from './snippets/infobar-colors.cs?raw'

const basicCode = `using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Border(b => [
            b.Text("Main application content area")
        ], title: "My App").Fill(),
        v.InfoBar("Ready")
    ])
));

await app.RunAsync();`

const sectionsCode = `using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Border(b => [
            v.Text("Use Tab to navigate between fields"),
            v.Text("Use Enter to submit"),
            v.Text("Use Esc to cancel")
        ], title: "Instructions").Fill(),
        v.InfoBar([
            "Tab", "Navigate",
            "Enter", "Submit",
            "Esc", "Cancel"
        ])
    ])
));

await app.RunAsync();`

const colorsCode = `using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Border(b => [
            b.Text("Application content here...")
        ], title: "Editor").Fill(),
        v.InfoBar([
            new InfoBarSection("Mode: Normal"),
            new InfoBarSection(" | "),
            new InfoBarSection("ERROR", Hex1bColor.Red, Hex1bColor.Yellow),
            new InfoBarSection(" | "),
            new InfoBarSection("Ln 42, Col 7")
        ])
    ])
));

await app.RunAsync();`

</script>

# InfoBar

Display status information, keyboard shortcuts, or contextual help at the bottom of your terminal application.

## Basic Usage

InfoBar is typically placed at the bottom of the screen in a VStack to create a status bar:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="infobar-basic" exampleTitle="InfoBar Widget - Basic Usage" />

The InfoBar widget:
- Always measures to exactly **one line in height**
- **Fills the available width** (like a full-width status bar)
- Uses **inverted colors** by default for visual distinction
- Is **not focusable** and does not handle input

<StaticTerminalPreview svgPath="/svg/infobar-simple.svg" :code="simpleSnippet" />

## Multiple Sections

Create an InfoBar with multiple sections by passing an array of strings. This is ideal for displaying keyboard shortcuts with alternating keys and descriptions:

<CodeBlock lang="csharp" :code="sectionsCode" command="dotnet run" example="infobar-sections" exampleTitle="InfoBar Widget - Multiple Sections" />

Each string in the array becomes a separate section. Common patterns include:
- **Keyboard shortcuts**: Alternate between key names and descriptions (`"F1", "Help", "Ctrl+S", "Save"`)
- **Status fields**: Use separators like `" | "` to visually divide sections
- **Label-value pairs**: Display dynamic information (`"Mode: Insert"`, `"Ln 42, Col 7"`)

<StaticTerminalPreview svgPath="/svg/infobar-shortcuts.svg" :code="shortcutsSnippet" />

## Custom Colored Sections

Use `InfoBarSection` to apply custom foreground and background colors to individual sections. This is useful for highlighting warnings, errors, or important status indicators:

<CodeBlock lang="csharp" :code="colorsCode" command="dotnet run" example="infobar-colors" exampleTitle="InfoBar Widget - Custom Colors" />

When you provide custom colors:
- They override the InfoBar's theme colors for **that specific section only**
- Other sections continue using the InfoBar's default (or inverted) theme colors
- Common use cases include error indicators (red/yellow), success messages (green), or warnings

<StaticTerminalPreview svgPath="/svg/infobar-colors.svg" :code="colorsSnippet" />

## Color Inversion

By default, InfoBar renders with **inverted colors** (`InvertColors = true`), which swaps the theme's foreground and background colors. This creates a visually distinct bar that stands out from the main content area.

To use normal (non-inverted) theme colors:

```csharp
ctx.InfoBar("Status", invertColors: false)
```

Or when using sections:

```csharp
ctx.InfoBar([
    new InfoBarSection("Ready")
], invertColors: false)
```

## Common Patterns

### Editor Status Bar

Display editing mode, cursor position, and file encoding:

```csharp
ctx.InfoBar([
    new InfoBarSection("Mode: Normal"),
    new InfoBarSection(" | "),
    new InfoBarSection("UTF-8"),
    new InfoBarSection(" | "),
    new InfoBarSection($"Ln {line}, Col {column}")
])
```

### Application Help Bar

Show keyboard shortcuts for common actions:

```csharp
ctx.InfoBar([
    "F1", "Help",
    "Ctrl+N", "New",
    "Ctrl+O", "Open",
    "Ctrl+S", "Save",
    "Ctrl+Q", "Quit"
])
```

### Contextual Status

Display dynamic status with error highlighting:

```csharp
ctx.InfoBar([
    new InfoBarSection($"Items: {count}"),
    new InfoBarSection(" | "),
    hasError 
        ? new InfoBarSection("ERROR", Hex1bColor.Red, Hex1bColor.Yellow)
        : new InfoBarSection("Ready"),
    new InfoBarSection(" | "),
    new InfoBarSection($"Last update: {timestamp}")
])
```

## Theming

InfoBar supports theme customization through `InfoBarTheme`:

```csharp
var theme = Hex1bThemes.Default.Clone()
    .Set(InfoBarTheme.ForegroundColor, Hex1bColor.Cyan)
    .Set(InfoBarTheme.BackgroundColor, Hex1bColor.DarkGray);

var app = new Hex1bApp(
    ctx => ctx.InfoBar("Themed Status Bar"),
    new Hex1bAppOptions { Theme = theme }
);
```

When `InvertColors = true` (the default), the InfoBar will swap these theme colors to create visual contrast.

## Related Widgets

- [Text](/guide/widgets/text) - For displaying read-only text content
- [VStack](/guide/widgets/stacks) - For arranging InfoBar at the bottom of the screen
- [Border](/guide/widgets/containers) - For framing the main content above the InfoBar
