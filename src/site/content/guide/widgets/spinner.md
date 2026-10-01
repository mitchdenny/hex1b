<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode  → src/Hex1b.Website/Examples/SpinnerBasicExample.cs
  - stylesCode → src/Hex1b.Website/Examples/SpinnerStylesExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import basicSnippet from './snippets/spinner-basic.cs?raw'
import stylesSnippet from './snippets/spinner-styles.cs?raw'
import themingSnippet from './snippets/spinner-theming.cs?raw'

const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.HStack(h => [
        h.Spinner(),
        h.Text(" Loading...")
    ]))
    .Build();

await terminal.RunAsync();`

const stylesCode = `using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Built-in Spinner Styles"),
        v.Text(""),
        v.HStack(h => [
            h.Spinner(SpinnerStyle.Dots), h.Text(" Dots  "),
            h.Spinner(SpinnerStyle.Line), h.Text(" Line  "),
            h.Spinner(SpinnerStyle.Arrow), h.Text(" Arrow")
        ]),
        v.HStack(h => [
            h.Spinner(SpinnerStyle.Circle), h.Text(" Circle  "),
            h.Spinner(SpinnerStyle.Square), h.Text(" Square  "),
            h.Spinner(SpinnerStyle.Bounce), h.Text(" Bounce")
        ]),
        v.Text(""),
        v.Text("Multi-Character Styles"),
        v.HStack(h => [
            h.Spinner(SpinnerStyle.BouncingBall), h.Text(" BouncingBall  "),
            h.Spinner(SpinnerStyle.LoadingBar), h.Text(" LoadingBar")
        ])
    ]))
    .Build();

await terminal.RunAsync();`

</script>

# Spinner

Display animated spinners to indicate ongoing activity.

## Basic Usage

Create a self-animating spinner using the fluent API:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="spinner-basic" exampleTitle="Spinner Widget - Basic Usage" />

Spinners are **self-animating**—no external timer or frame counter is needed. The animation is time-based internally, ensuring consistent animation speed regardless of screen refresh rate.

## Built-in Styles

Hex1b provides 12 built-in spinner styles. Each style has its own animation interval and behavior:

<CodeBlock lang="csharp" :code="stylesCode" command="dotnet run" example="spinner-styles" exampleTitle="Spinner Widget - Styles" />

### Single-Character Styles

| Style | Frames | Interval | Description |
|-------|--------|----------|-------------|
| `Dots` | ⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏ | 80ms | Braille dot pattern (default) |
| `DotsScrolling` | ⠿⠾⠽⠻⠟⠯ | 80ms | Scrolling braille dots |
| `Line` | \|/-\\ | 100ms | Classic ASCII spinner |
| `Arrow` | ←↖↑↗→↘↓↙ | 100ms | Rotating arrow |
| `Circle` | ◐◓◑◒ | 120ms | Quarter circle rotation |
| `Square` | ◰◳◲◱ | 120ms | Quarter square rotation |
| `Bounce` | ⠁⠂⠄⠂ | 80ms | Bouncing dot (ping-pong) |
| `GrowHorizontal` | ▏▎▍▌▋▊▉█ | 80ms | Horizontal growth (ping-pong) |
| `GrowVertical` | ▁▂▃▄▅▆▇█ | 80ms | Vertical growth (ping-pong) |

### Multi-Character Styles

| Style | Width | Interval | Description |
|-------|-------|----------|-------------|
| `BouncingBall` | 5 chars | 100ms | Ball bouncing between bars |
| `LoadingBar` | 6 chars | 120ms | Bar with animated fill |
| `Segments` | 3 chars | 100ms | Three-segment loader |

## Custom Styles

Create custom spinners by defining your own frames:

```csharp
// Simple custom spinner
var custom = new SpinnerStyle("🌑", "🌒", "🌓", "🌔", "🌕", "🌖", "🌗", "🌘");

// Custom spinner with specific interval
var fast = new SpinnerStyle(
    frames: ["⣾", "⣽", "⣻", "⢿", "⡿", "⣟", "⣯", "⣷"],
    interval: TimeSpan.FromMilliseconds(60)
);

// Custom spinner with ping-pong animation
var pingPong = new SpinnerStyle(
    frames: ["▁", "▂", "▃", "▄", "▅", "▆", "▇", "█"],
    interval: TimeSpan.FromMilliseconds(80),
    autoReverse: true  // Plays 0→7→0 instead of 0→7→0→7...
);
```

## Manual Frame Control

For special cases where you need explicit control over which frame is displayed:

```csharp
// Display a specific frame (no auto-animation)
v.Spinner(frameIndex: 3)

// Specific style with manual frame
v.Spinner(SpinnerStyle.Arrow, frameIndex: myCounter)
```

When using manual frame control:
- The spinner displays exactly the specified frame
- No automatic redraws are scheduled
- You must update the frame index and trigger redraws yourself

## Theming

Customize spinner appearance using the theme system:

<StaticTerminalPreview svgPath="/svg/spinner-theming.svg" :code="themingSnippet" />

Available theme elements:

| Element | Default | Description |
|---------|---------|-------------|
| `Style` | `SpinnerStyle.Dots` | Default spinner style |
| `ForegroundColor` | `Default` | Spinner color |
| `BackgroundColor` | `Default` | Background color |

```csharp
ctx.ThemePanel(
    theme =>
    {
        theme.Set(SpinnerTheme.Style, SpinnerStyle.Arrow);
        theme.Set(SpinnerTheme.ForegroundColor, Hex1bColor.Cyan);
        return theme;
    },
    t => [
        t.HStack(h => [
            h.Spinner(),  // Uses theme's Arrow style and Cyan color
            h.Text(" Processing...")
        ])
    ]
)
```

## Layout Behavior

Spinners measure their width based on the current frame's display width. Most single-character spinners are 1 column wide, while multi-character spinners vary:

```csharp
// Single character spinner - 1 column wide
h.Spinner(SpinnerStyle.Dots)

// Multi-character spinner - 5 columns wide
h.Spinner(SpinnerStyle.BouncingBall)
```

The spinner height is always 1 row.

## Related Widgets

- [Progress](/guide/widgets/progress) - For showing completion percentage
- [Text](/guide/widgets/text) - For status messages alongside spinners
- [Layout & Stacks](/guide/widgets/stacks) - For arranging spinners with labels
