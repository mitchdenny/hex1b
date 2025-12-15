# Theming

Hex1b supports customizable themes for colors, borders, and widget styling.

## Using a Theme

Pass a theme provider to your app:

```csharp
var options = new Hex1bAppOptions
{
    ThemeProvider = () => new Hex1bTheme
    {
        PrimaryColor = Hex1bColor.Cyan,
        SecondaryColor = Hex1bColor.Magenta,
        BackgroundColor = Hex1bColor.Black,
        ForegroundColor = Hex1bColor.White,
        AccentColor = Hex1bColor.Yellow,
        ErrorColor = Hex1bColor.Red
    }
};

var app = new Hex1bApp<AppState>(initialState, buildWidget, options);
```

## Theme Properties

| Property | Default | Used For |
|----------|---------|----------|
| `PrimaryColor` | Cyan | Focused elements, buttons |
| `SecondaryColor` | White | Secondary text, borders |
| `BackgroundColor` | Black | Panel backgrounds |
| `ForegroundColor` | White | Default text |
| `AccentColor` | Yellow | Highlights, selections |
| `ErrorColor` | Red | Error messages |

## Color Values

Hex1b supports standard terminal colors and 256-color mode:

```csharp
// Standard 16 colors
Hex1bColor.Black, Hex1bColor.Red, Hex1bColor.Green, Hex1bColor.Yellow
Hex1bColor.Blue, Hex1bColor.Magenta, Hex1bColor.Cyan, Hex1bColor.White

// Bright variants
Hex1bColor.BrightBlack, Hex1bColor.BrightRed, // etc.

// 256 color palette
Hex1bColor.Palette(196)  // Bright red

// RGB (if terminal supports it)
Hex1bColor.Rgb(78, 205, 196)  // Teal
```

## Widget-Level Styling

Apply styles directly to widgets:

```csharp
new TextBlockWidget("Important!")
    .Foreground(Hex1bColor.Red)
    .Background(Hex1bColor.Yellow)
    .Bold()

new BorderWidget(content)
    .BorderColor(Hex1bColor.Cyan)
    .TitleColor(Hex1bColor.White)
```

## Text Styles

```csharp
new TextBlockWidget("Styled text")
    .Bold()
    .Italic()
    .Underline()
    .Strikethrough()
    .Dim()
    .Blink()        // Use sparingly!
    .Reverse()
```

## Border Styles

```csharp
// Different border characters
new BorderWidget(content, BorderStyle.Single)   // ┌─┐│└─┘
new BorderWidget(content, BorderStyle.Double)   // ╔═╗║╚═╝
new BorderWidget(content, BorderStyle.Rounded)  // ╭─╮│╰─╯
new BorderWidget(content, BorderStyle.Heavy)    // ┏━┓┃┗━┛
new BorderWidget(content, BorderStyle.Dashed)   // ┌╌┐╎└╌┘
```

## Dynamic Theming

Change themes at runtime:

```csharp
public record AppState(
    Hex1bTheme CurrentTheme,
    // ... other state
);

var app = new Hex1bApp<AppState>(
    initialState,
    buildWidget: (ctx, ct) => BuildUI(ctx),
    options: new Hex1bAppOptions
    {
        ThemeProvider = () => ctx.State.CurrentTheme
    }
);

// Toggle theme
void ToggleDarkMode(WidgetContext<AppState> ctx)
{
    var theme = ctx.State.CurrentTheme;
    ctx.SetState(ctx.State with {
        CurrentTheme = theme with {
            BackgroundColor = theme.BackgroundColor == Hex1bColor.Black
                ? Hex1bColor.White
                : Hex1bColor.Black
        }
    });
}
```

## Live Demo

<TerminalDemo exhibit="theming" title="Theming Demo" />

## Next Steps

- [Widgets Reference](/guide/widgets/text) - Complete widget documentation
- [Terminal Rendering](/deep-dives/terminal-rendering) - How colors work
