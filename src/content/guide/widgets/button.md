# Button Widget

An interactive button that responds to Enter or Space.

## Basic Usage

```csharp
new ButtonWidget("Click me!", () => Console.WriteLine("Clicked!"))
```

## With State

```csharp
new ButtonWidget("Increment", () => ctx.SetState(ctx.State + 1))
```

## Keyboard Shortcuts

Add custom key bindings:

```csharp
new ButtonWidget("Save", () => Save())
    .OnKey(Hex1bKey.S, Hex1bModifiers.Control, () => Save())
```

## Styling

```csharp
new ButtonWidget("Danger", () => DeleteAll())
    .Foreground(Hex1bColor.White)
    .Background(Hex1bColor.Red)
```

## Focus Appearance

Buttons show focus state:
- Unfocused: `[ Label ]`
- Focused: `[▶ Label ]`

The exact appearance depends on your theme.

## Live Demo

<TerminalDemo exhibit="hello-world" title="Button Demo" />
