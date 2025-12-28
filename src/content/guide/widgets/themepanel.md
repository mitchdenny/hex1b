<script setup>
import basicSnippet from './snippets/themepanel-basic.cs?raw'
import buttonSnippet from './snippets/themepanel-button.cs?raw'
import nestedSnippet from './snippets/themepanel-nested.cs?raw'

const basicCode = `using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.ThemePanel(
        theme => theme.Clone()
            .Set(GlobalTheme.ForegroundColor, Hex1bColor.Yellow)
            .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(0, 0, 139)),
        ctx.VStack(v => [
            v.Text("Themed content"),
            v.Text("Yellow on dark blue")
        ])
    )
));

await app.RunAsync();`
</script>

# ThemePanelWidget

Apply scoped theme mutations to a subtree of widgets.

ThemePanelWidget allows you to override specific theme values for a child widget and all its descendants. The theme mutations only affect the subtree—once rendering exits the ThemePanel, the original theme is restored.

## Basic Usage

Create a ThemePanel with a theme mutator function using the fluent API:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" />

::: tip Functional API
ThemePanelWidget uses a `Func<Hex1bTheme, Hex1bTheme>` pattern. Your function receives the current theme and returns a modified copy. Use `.Clone()` to create a new theme instance before applying modifications.
:::

## Basic ThemePanel

A simple theme panel that changes text colors:

<StaticTerminalPreview svgPath="/svg/themepanel-basic.svg" :code="basicSnippet" />

The ThemePanel applies the theme mutations before rendering its child, then restores the original theme afterward.

## How Theme Mutations Work

ThemePanelWidget uses a functional approach to theme modification:

1. **Receives current theme**: The mutator function receives the active theme at render time
2. **Returns modified theme**: You return a new theme with your modifications applied
3. **Scoped application**: The modified theme applies only to the child subtree
4. **Automatic restoration**: After rendering the child, the original theme is restored

```csharp
// The mutator function signature
Func<Hex1bTheme, Hex1bTheme> mutator = theme => theme.Clone()
    .Set(GlobalTheme.ForegroundColor, Hex1bColor.Green);
```

## Theming Buttons

ThemePanels work with interactive widgets like buttons:

<StaticTerminalPreview svgPath="/svg/themepanel-button.svg" :code="buttonSnippet" />

You can customize any theme element within the ThemePanel scope:

```csharp
ctx.ThemePanel(
    theme => theme.Clone()
        .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(0, 100, 0))
        .Set(ButtonTheme.ForegroundColor, Hex1bColor.White)
        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Green),
    ctx.Button("Themed Button", () => { /* action */ })
)
```

## Nesting ThemePanels

ThemePanels can be nested to create layered theme overrides:

<StaticTerminalPreview svgPath="/svg/themepanel-nested.svg" :code="nestedSnippet" />

Each nested ThemePanel starts with the theme from its parent context and applies additional mutations:

```csharp
ctx.ThemePanel(
    outer => outer.Clone()
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
    ctx.VStack(v => [
        v.Text("Outer theme applies here"),
        v.ThemePanel(
            inner => inner.Clone()
                .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(0, 0, 139)),
            // Inner has Cyan foreground (inherited) + dark blue background
            v.Text("Both themes combined")
        ),
        v.Text("Only outer theme here")
    ])
)
```

## VStack Builder Overload

For convenience, ThemePanel provides a VStack builder overload:

```csharp
// These are equivalent:
ctx.ThemePanel(theme => theme.Clone().Set(...), ctx.VStack(v => [...]))

ctx.ThemePanel(theme => theme.Clone().Set(...), v => [
    v.Text("Line 1"),
    v.Text("Line 2")
])
```

## Layout Behavior

ThemePanelWidget has no visual presence—it only affects theming:

- **Measuring**: The child is measured with the full constraints
- **Arranging**: The child gets the full bounds of the ThemePanel
- **Rendering**: Theme is swapped, child is rendered, theme is restored
- **No size overhead**: ThemePanel adds zero pixels to the layout

## Focus Behavior

ThemePanelWidget is not focusable:

- Focus passes through to focusable children
- The ThemePanel itself cannot receive focus
- Tab navigation works normally within themed content

## Common Patterns

### Danger Zones

Create visually distinct danger areas:

```csharp
ctx.ThemePanel(
    theme => theme.Clone()
        .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(139, 0, 0))
        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red)
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Red),
    ctx.VStack(v => [
        v.Text("⚠ Danger Zone"),
        v.Button("Delete Everything", async () => { /* ... */ })
    ])
)
```

### Information Sections

Highlight informational content:

```csharp
ctx.ThemePanel(
    theme => theme.Clone()
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan)
        .Set(BorderTheme.BorderColor, Hex1bColor.Cyan),
    ctx.Border(b => [
        b.Text("ℹ Info: This is helpful information.")
    ], title: "Info")
)
```

### Success Feedback

Style success states:

```csharp
ctx.ThemePanel(
    theme => theme.Clone()
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Green),
    ctx.VStack(v => [
        v.Text("✓ Operation completed successfully"),
        v.Text("All files have been saved.")
    ])
)
```

### Combined with Panel

ThemePanel pairs well with PanelWidget for background colors:

```csharp
ctx.ThemePanel(
    theme => theme.Clone()
        .Set(PanelTheme.BackgroundColor, Hex1bColor.FromRgb(0, 0, 139))
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.White),
    ctx.Panel(
        ctx.VStack(v => [
            v.Text("Content with themed background")
        ])
    )
)
```

## Comparison with PanelWidget

| Feature | ThemePanelWidget | PanelWidget |
|---------|------------------|-------------|
| Purpose | Theme scoping | Visual background |
| Draws background | No | Yes |
| Modifies theme | Yes (any element) | Yes (colors only) |
| Size overhead | None | None |
| Nesting effect | Cumulative mutations | Independent backgrounds |

## Related Widgets

- [PanelWidget](/guide/widgets/panel) - For visual backgrounds
- [BorderWidget](/guide/widgets/border) - For decorative borders
- [Theming Guide](/guide/theming) - Comprehensive theming documentation
