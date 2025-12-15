# Container Widgets (Border/Panel)

Wrap content with visual decoration and padding.

## BorderWidget

Draw a border around content:

```csharp
new BorderWidget(
    new TextBlockWidget("Content here")
)
```

### With Title

```csharp
new BorderWidget(content).Title("Settings")
```

### Border Styles

```csharp
new BorderWidget(content, BorderStyle.Single)   // ┌─┐│└─┘
new BorderWidget(content, BorderStyle.Double)   // ╔═╗║╚═╝
new BorderWidget(content, BorderStyle.Rounded)  // ╭─╮│╰─╯
new BorderWidget(content, BorderStyle.Heavy)    // ┏━┓┃┗━┛
```

## PanelWidget

A border with built-in padding:

```csharp
new PanelWidget(content)   // 1 cell padding
new PanelWidget(content, padding: 2)
```

## Styling

```csharp
new BorderWidget(content)
    .BorderColor(Hex1bColor.Cyan)
    .TitleColor(Hex1bColor.White)
    .Background(Hex1bColor.Black)
```

## Live Demo

<TerminalDemo exhibit="layout" title="Container Demo" />
