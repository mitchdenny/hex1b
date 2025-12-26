<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode    → src/Hex1b.Website/Examples/TextBasicExample.cs
  - overflowCode → src/Hex1b.Website/Examples/TextOverflowExample.cs
  - completeCode → src/Hex1b.Website/Examples/TextCompleteExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
const basicCode = `using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("Welcome to Hex1b"),
        v.Text("Build beautiful terminal UIs")
    ])
));

await app.RunAsync();`

const overflowCode = `using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("═══ Text Overflow Modes ═══"),
        v.Text(""),
        v.Text("Wrap Mode:"),
        v.Text(
            "This is a long description that demonstrates text wrapping behavior in Hex1b. " +
            "When the text content exceeds the available width of the container, it automatically " +
            "breaks at word boundaries to fit within the allocated space. This ensures that all " +
            "content remains visible to the user without requiring horizontal scrolling. The widget's " +
            "measured height increases dynamically based on the number of wrapped lines.",
            TextOverflow.Wrap
        ),
        v.Text(""),
        v.Text("Ellipsis Mode:"),
        v.Text("This is a much longer piece of text that will definitely be truncated with an ellipsis character sequence when it exceeds the available fixed width of forty columns", TextOverflow.Ellipsis)
            .FixedWidth(40),
        v.Text(""),
        v.Text("Default (Overflow) Mode:"),
        v.Text("This text extends beyond its allocated bounds and will be clipped by the parent container if clipping is enabled")
    ])
));

await app.RunAsync();`

const completeCode = `using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("═══ Application Title ═══"),
        v.Text(""),
        v.Text(
            "This is a long description that demonstrates text wrapping. " +
            "When the text exceeds the available width, it automatically " +
            "breaks at word boundaries to fit within the container.",
            TextOverflow.Wrap
        ),
        v.Text(""),
        v.Text("Status: Loading...").FillWidth(),
        v.Text("Item name that might be too long", TextOverflow.Ellipsis)
            .FixedWidth(25)
    ])
));

await app.RunAsync();`
</script>

# TextBlockWidget

Display static or dynamic text content in your terminal UI.

## Basic Usage

Create a simple text display using the fluent API:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="text-basic" exampleTitle="Text Widget - Basic Usage" />

## Text Overflow Behavior

TextBlockWidget provides three modes for handling text that exceeds the available width:

<CodeBlock lang="csharp" :code="overflowCode" command="dotnet run" example="text-overflow" exampleTitle="Text Overflow Modes" />

### Overflow (Default)

Text extends beyond its bounds. Clipping is handled by parent containers:

```csharp
v.Text("This text may extend beyond its container")
```

### Wrap

Text wraps to multiple lines at word boundaries:

```csharp
v.Text("This long text will wrap to fit within the available width", TextOverflow.Wrap)
```

When wrapping:
- Words break at spaces when possible
- Very long words are broken mid-word if necessary
- The widget's measured height increases with the number of lines

### Ellipsis

Text is truncated with "..." when it exceeds the width:

```csharp
v.Text("Long title that gets truncated...", TextOverflow.Ellipsis)
```

## Size Hints

Control how text widgets size within layouts using size hints:

```csharp
// Fill available width
v.Text("Status: OK").FillWidth()

// Fixed width
v.Text("ID").FixedWidth(10)

// Size to content (default behavior)
v.Text("Label").ContentWidth()
```

## Unicode Support

TextBlockWidget correctly handles Unicode text including:

- **Wide characters** (CJK): 日本語, 中文, 한국어
- **Emoji**: 🎉 🚀 ✨
- **Combining characters**: é, ñ
- **Box-drawing characters**: ┌─┐│└─┘

```csharp
v.Text("日本語テスト 🎉")
```

## Complete Example

Here's a practical example showing various text features:

<CodeBlock lang="csharp" :code="completeCode" command="dotnet run" example="text-complete" exampleTitle="Text Widget - Complete Example" />

## API Reference

### Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `text` | `string` | The text content to display |
| `overflow` | `TextOverflow` | How to handle text that exceeds available width (optional) |

### TextOverflow Enum

| Value | Description |
|-------|-------------|
| `Overflow` | Text extends beyond bounds (default) |
| `Wrap` | Text wraps to multiple lines at word boundaries |
| `Ellipsis` | Text is truncated with "..." |

### Extension Methods

| Method | Description |
|--------|-------------|
| `ctx.Text(string)` | Creates a TextBlockWidget |
| `ctx.Text(string, TextOverflow)` | Creates a TextBlockWidget with overflow behavior |

### Size Hint Methods

All size hint methods are available on TextBlockWidget:

| Method | Description |
|--------|-------------|
| `.FillWidth()` | Expand to fill available width |
| `.FillHeight()` | Expand to fill available height |
| `.Fill()` | Fill in both dimensions |
| `.FixedWidth(n)` | Set a fixed width of n columns |
| `.FixedHeight(n)` | Set a fixed height of n lines |
| `.ContentWidth()` | Size to fit content width |
| `.ContentHeight()` | Size to fit content height |

## Related Widgets

- [TextBoxWidget](/guide/widgets/textbox) - For editable text input
- [Layout & Stacks](/guide/widgets/stacks) - For arranging text with other widgets
