# Stack Widgets (HStack/VStack)

Arrange children horizontally or vertically.

## VStack (Vertical)

Stack children top to bottom:

```csharp
new VStackWidget([
    new TextBlockWidget("Header"),
    new ButtonWidget("Button 1", () => {}),
    new ButtonWidget("Button 2", () => {})
])
```

## HStack (Horizontal)

Stack children left to right:

```csharp
new HStackWidget([
    new TextBlockWidget("Label:"),
    new TextBoxWidget(value, onChange).Fill(),
    new ButtonWidget("OK", () => {})
])
```

## Sizing Children

### Fill

```csharp
new VStackWidget([
    new TextBlockWidget("Header"),        // Content height
    new ListWidget(items, onSelect).Fill(), // Fill remaining
    new TextBlockWidget("Footer")          // Content height
])
```

### Fixed Size

```csharp
new HStackWidget([
    new PanelWidget(sidebar).Width(30),
    new PanelWidget(main).Fill()
])
```

## Spacing

```csharp
new VStackWidget([...]).Spacing(1)  // 1 row gap between children
new HStackWidget([...]).Spacing(2)  // 2 column gap between children
```

## Live Demo

<TerminalDemo exhibit="layout" title="Stack Layout Demo" />
