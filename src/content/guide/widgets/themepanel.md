<script setup>
import basicSnippet from './snippets/themepanel-basic.cs?raw'
import buttonSnippet from './snippets/themepanel-button.cs?raw'
import nestedSnippet from './snippets/themepanel-nested.cs?raw'
import buttonThemedSnippet from './snippets/themepanel-button-themed.cs?raw'
import nestingSnippet from './snippets/themepanel-nesting.cs?raw'
import dangerSnippet from './snippets/themepanel-danger.cs?raw'
import infoSnippet from './snippets/themepanel-info.cs?raw'
import successSnippet from './snippets/themepanel-success.cs?raw'
import borderSnippet from './snippets/themepanel-border.cs?raw'

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

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="themepanel-basic" exampleTitle="ThemePanel Widget - Basic Usage" />

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

<StaticTerminalPreview svgPath="/svg/themepanel-button-themed.svg" :code="buttonThemedSnippet" />

## Nesting ThemePanels

ThemePanels can be nested to create layered theme overrides:

<StaticTerminalPreview svgPath="/svg/themepanel-nested.svg" :code="nestedSnippet" />

Each nested ThemePanel starts with the theme from its parent context and applies additional mutations:

<StaticTerminalPreview svgPath="/svg/themepanel-nesting.svg" :code="nestingSnippet" />

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

<StaticTerminalPreview svgPath="/svg/themepanel-danger.svg" :code="dangerSnippet" />

### Information Sections

Highlight informational content:

<StaticTerminalPreview svgPath="/svg/themepanel-info.svg" :code="infoSnippet" />

### Success Feedback

Style success states:

<StaticTerminalPreview svgPath="/svg/themepanel-success.svg" :code="successSnippet" />

### Combined with Border

ThemePanel pairs well with BorderWidget for styled containers:

<StaticTerminalPreview svgPath="/svg/themepanel-border.svg" :code="borderSnippet" />

## Related Widgets

- [BorderWidget](/guide/widgets/border) - For decorative borders
- [Theming Guide](/guide/theming) - Comprehensive theming documentation
