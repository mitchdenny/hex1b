<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode        → src/Hex1b.Website/Examples/InfoBarBasicExample.cs
  - spacerCode       → src/Hex1b.Website/Examples/InfoBarSpacerExample.cs
  - spinnerCode      → src/Hex1b.Website/Examples/InfoBarSpinnerExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import separatorSnippet from './snippets/infobar-separator.cs?raw'
import widthSnippet from './snippets/infobar-width.cs?raw'
import themingSnippet from './snippets/infobar-theming.cs?raw'

const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Border(b => [
            b.Text("Main content area"),
            b.Text(""),
            b.Text("The status bar sits at the bottom of the window")
        ], title: "Application"),
        v.InfoBar(s => [
            s.Section("NORMAL"),
            s.Section("main.cs"),
            s.Section("Ln 42, Col 8")
        ]).WithDefaultSeparator(" │ ")
    ]))
    .Build();

await terminal.RunAsync();`

const spacerCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Border(b => [
            b.Text("Content with a flexible status bar")
        ], title: "Spacer Demo"),
        v.InfoBar(s => [
            s.Section("Mode: INSERT"),
            s.Spacer(),
            s.Section("100%"),
            s.Separator(" │ "),
            s.Section("UTF-8")
        ])
    ]))
    .Build();

await terminal.RunAsync();`

const spinnerCode = `using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Border(b => [
            b.Text("Background operation in progress...")
        ], title: "Activity Indicator"),
        v.InfoBar(s => [
            s.Section(x => x.HStack(h => [
                h.Spinner(SpinnerStyle.Dots),
                h.Text(" Saving...")
            ])),
            s.Spacer(),
            s.Section("Ready")
        ])
    ]))
    .Build();

await terminal.RunAsync();`

</script>

# InfoBar

A horizontal status bar widget for displaying contextual information at the edge of your application. InfoBar is commonly used to show mode indicators, file status, cursor position, and other metadata.

## Basic Usage

Create an InfoBar with sections separated by a default separator:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="infobar-basic" exampleTitle="InfoBar - Basic Usage" />

## Spacer

Use `Spacer()` to push sections apart. The spacer expands to fill available space:

<CodeBlock lang="csharp" :code="spacerCode" command="dotnet run" example="infobar-spacer" exampleTitle="InfoBar - Spacer" />

## Separators

Control how sections are visually separated:

### Default Separator

Use `.WithDefaultSeparator()` to automatically insert separators between consecutive sections:

<StaticTerminalPreview svgPath="/svg/infobar-separator.svg" :code="separatorSnippet" />

### Explicit Separators

Add separators manually for fine-grained control:

```csharp
ctx.InfoBar(s => [
    s.Section("Mode"),
    s.Separator(" │ "),  // Explicit separator
    s.Section("File"),
    // No separator here
    s.Spacer(),
    s.Section("Ready")
])
```

## Widget Content

Sections can contain any widget, not just text. This enables rich status displays:

<CodeBlock lang="csharp" :code="spinnerCode" command="dotnet run" example="infobar-spinner" exampleTitle="InfoBar - Widget Content" />

## Width Control

Control how sections size themselves:

<StaticTerminalPreview svgPath="/svg/infobar-width.svg" :code="widthSnippet" />

### Width Options

| Method | Description |
|--------|-------------|
| `.ContentWidth()` | Size to fit content (default) |
| `.FixedWidth(n)` | Fixed width in columns |
| `.FillWidth()` | Expand to fill available space |
| `.FillWidth(weight)` | Proportional fill with weight |

### Alignment

Within fixed-width sections, control text alignment:

```csharp
s.Section("Left").FixedWidth(20).AlignLeft()    // Default
s.Section("Center").FixedWidth(20).AlignCenter()
s.Section("Right").FixedWidth(20).AlignRight()
```

## Per-Section Theming

Apply custom colors to individual sections using `.Theme()`:

<StaticTerminalPreview svgPath="/svg/infobar-theming.svg" :code="themingSnippet" />

## Color Inversion

By default, InfoBar inverts foreground and background colors to create visual distinction. Disable this with `.InvertColors(false)`:

```csharp
ctx.InfoBar(s => [
    s.Section("Normal colors")
]).InvertColors(false)
```

## Related Widgets

- [Text](/guide/widgets/text) - For simple text display
- [Spinner](/guide/widgets/spinner) - Animated activity indicators for InfoBar sections
- [ThemePanel](/guide/widgets/themepanel) - For broader theme customization
