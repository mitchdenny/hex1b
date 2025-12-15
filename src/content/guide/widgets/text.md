# Text & TextBlock Widgets

Display static or styled text in your terminal UI.

## TextBlockWidget

The simplest widget—displays a string:

```csharp
new TextBlockWidget("Hello, World!")
```

## Styling

Apply text styles with extension methods:

```csharp
new TextBlockWidget("Important!")
    .Bold()
    .Foreground(Hex1bColor.Red)

new TextBlockWidget("Subtle note")
    .Dim()
    .Italic()
```

## Available Styles

| Method | Effect |
|--------|--------|
| `.Bold()` | Bold text |
| `.Italic()` | Italic text |
| `.Underline()` | Underlined text |
| `.Strikethrough()` | ~~Strikethrough~~ |
| `.Dim()` | Dimmed/faded text |
| `.Blink()` | Blinking text (use sparingly!) |
| `.Reverse()` | Swap foreground/background |

## Colors

```csharp
.Foreground(Hex1bColor.Cyan)
.Background(Hex1bColor.Black)
```

## Multi-line Text

TextBlock handles newlines:

```csharp
new TextBlockWidget("Line 1\nLine 2\nLine 3")
```

## Text Widget (Inline)

For inline text within stacks, `TextWidget` is also available for simpler cases:

```csharp
new HStackWidget([
    new TextWidget("Name: "),
    new TextBlockWidget(user.Name).Bold()
])
```
