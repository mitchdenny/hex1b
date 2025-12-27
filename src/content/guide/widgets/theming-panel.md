<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode       → src/Hex1b.Website/Examples/PanelBasicExample.cs
  - interactiveCode → src/Hex1b.Website/Examples/PanelInteractiveExample.cs
  - panel-button-styling → src/Hex1b.Website/Examples/PanelButtonStylingExample.cs
  - panel-border-styling → src/Hex1b.Website/Examples/PanelBorderStylingExample.cs
  - panel-list-styling → src/Hex1b.Website/Examples/PanelListStylingExample.cs
  - panel-background → src/Hex1b.Website/Examples/PanelBackgroundExample.cs
  - panel-nested → src/Hex1b.Website/Examples/PanelNestedExample.cs
  - panel-semantic → src/Hex1b.Website/Examples/PanelSemanticExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import basicSnippet from './snippets/panel-basic.cs?raw'
import buttonStylingSnippet from './snippets/panel-button-styling.cs?raw'
import borderStylingSnippet from './snippets/panel-border-styling.cs?raw'
import listStylingSnippet from './snippets/panel-list-styling.cs?raw'
import backgroundSnippet from './snippets/panel-background.cs?raw'
import nestedSnippet from './snippets/panel-nested.cs?raw'
import semanticSnippet from './snippets/panel-semantic.cs?raw'

const basicCode = `using Hex1b;
using Hex1b.Widgets;
using Hex1b.Theming;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.ThemingPanel(
        theme => theme
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Cyan)
            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black),
        ctx.VStack(v => [
            v.Text("Buttons in this panel have cyan focus:"),
            v.Button("Styled Button"),
            v.Button("Another Button")
        ])
    )
));

await app.RunAsync();`

const interactiveCode = `using Hex1b;
using Hex1b.Widgets;
using Hex1b.Theming;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.HStack(h => [
        // Normal theme section
        h.Border(b => [
            b.VStack(v => [
                v.Text("Default Theme"),
                v.Button("Normal"),
                v.Button("Buttons")
            ])
        ], title: "Standard"),
        
        // Custom themed section
        h.ThemingPanel(
            theme => theme
                .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Green)
                .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black)
                .Set(BorderTheme.BorderColor, Hex1bColor.Green),
            h.Border(b => [
                b.VStack(v => [
                    v.Text("Success Theme"),
                    v.Button("Green"),
                    v.Button("Buttons")
                ])
            ], title: "Styled")
        ),
        
        // Another custom themed section
        h.ThemingPanel(
            theme => theme
                .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red)
                .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.White)
                .Set(BorderTheme.BorderColor, Hex1bColor.Red),
            h.Border(b => [
                b.VStack(v => [
                    v.Text("Danger Theme"),
                    v.Button("Red"),
                    v.Button("Buttons")
                ])
            ], title: "Warning")
        )
    ])
));

await app.RunAsync();`
</script>

# ThemingPanelWidget

Apply differential styles to child widgets.

ThemingPanelWidget creates a **scoped theme boundary** that lets you override any theme element for all widgets within its subtree. This enables localized styling—changing button colors, border styles, list indicators, and more—without affecting the rest of your application.

## The Power of Scoped Theming

Unlike setting colors on individual widgets, ThemingPanel modifies the **theme itself** for all children. This means:

- **Buttons** can have different focus colors in different sections
- **Borders** can use different line characters or colors
- **Lists** can have custom selection indicators
- Any widget that reads from the theme inherits your overrides

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="panel-basic" exampleTitle="ThemingPanel - Scoped Button Styles" />

::: tip Theme Callback
The first parameter is always a theme callback: `theme => theme.Set(...)`. The callback receives a **cloned** theme, so your changes don't affect the parent scope or siblings.
:::

## Side-by-Side Theme Comparison

ThemingPanels really shine when you need different visual treatments in the same UI:

<CodeBlock lang="csharp" :code="interactiveCode" command="dotnet run" example="panel-interactive" exampleTitle="ThemingPanel - Multiple Theme Scopes" />

## Available Theme Elements

ThemingPanel can modify **any** theme element. Here are the most commonly overridden:

### Button Styling

<StaticTerminalPreview svgPath="/svg/panel-button-styling.svg" :code="buttonStylingSnippet" />

### Border Styling

<StaticTerminalPreview svgPath="/svg/panel-border-styling.svg" :code="borderStylingSnippet" />

### List Styling

<StaticTerminalPreview svgPath="/svg/panel-list-styling.svg" :code="listStylingSnippet" />

### Panel Background

ThemingPanel also supports its own background color:

<StaticTerminalPreview svgPath="/svg/panel-background.svg" :code="backgroundSnippet" />

## Nested Theme Scopes

ThemingPanels can be nested, with inner panels overriding outer ones:

<StaticTerminalPreview svgPath="/svg/panel-nested.svg" :code="nestedSnippet" />

## Common Patterns

### Semantic Sections

Create distinct visual zones for different UI purposes:

<StaticTerminalPreview svgPath="/svg/panel-semantic.svg" :code="semanticSnippet" />

### Highlighting Active Panes

In splitter layouts, highlight the active pane:

```csharp
ctx.HSplitter(
    ctx.ThemingPanel(
        theme => isLeftActive
            ? theme.Set(BorderTheme.BorderColor, Hex1bColor.Cyan)
            : theme,
        ctx.Border(b => [ /* left content */ ], title: "Left")
    ),
    ctx.ThemingPanel(
        theme => !isLeftActive
            ? theme.Set(BorderTheme.BorderColor, Hex1bColor.Cyan)
            : theme,
        ctx.Border(b => [ /* right content */ ], title: "Right")
    )
)
```

### Theme Variations

Create reusable theme builders:

```csharp
Func<Hex1bTheme, Hex1bTheme> successTheme = theme => theme
    .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Green)
    .Set(BorderTheme.BorderColor, Hex1bColor.Green);

Func<Hex1bTheme, Hex1bTheme> dangerTheme = theme => theme
    .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red)
    .Set(BorderTheme.BorderColor, Hex1bColor.Red);

ctx.VStack(v => [
    v.ThemingPanel(successTheme, v.Button("Success")),
    v.ThemingPanel(dangerTheme, v.Button("Danger"))
])
```

## Layout Behavior

ThemingPanelWidget is transparent to layout:

- **No size overhead**: Child takes all available space
- **Focus passthrough**: Focus navigates directly to child widgets
- **Only affects theming**: Layout and input work exactly as without the panel

## Theme Elements Reference

| Theme Class | Key Elements |
|-------------|-------------|
| `ButtonTheme` | `FocusedBackgroundColor`, `FocusedForegroundColor`, `LeftBracket`, `RightBracket` |
| `BorderTheme` | `BorderColor`, `TitleColor`, corner and line characters |
| `ListTheme` | `SelectedBackgroundColor`, `SelectedForegroundColor`, `SelectedIndicator` |
| `TextBoxTheme` | `ForegroundColor`, `BackgroundColor`, `CursorColor` |
| `ProgressTheme` | `BarColor`, `BackgroundColor` |
| `ThemingPanelTheme` | `BackgroundColor`, `ForegroundColor` |

## Related Widgets

- [BorderWidget](/guide/widgets/border) - Decorative borders (themable via `BorderTheme`)
- [Button](/guide/widgets/button) - Interactive buttons (themable via `ButtonTheme`)
- [List](/guide/widgets/list) - Selection lists (themable via `ListTheme`)
