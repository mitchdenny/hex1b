<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode    → src/Hex1b.Website/Examples/TextBasicExample.cs
  - overflowCode → src/Hex1b.Website/Examples/TextOverflowExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import truncateSnippet from './snippets/text-truncate.cs?raw'
import wrapSnippet from './snippets/text-wrap.cs?raw'
import ellipsisSnippet from './snippets/text-ellipsis.cs?raw'
import unicodeSnippet from './snippets/text-unicode.cs?raw'

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
            "measured height increases dynamically based on the number of wrapped lines."
        ).Wrap(),
        v.Text(""),
        v.Text("Ellipsis Mode:"),
        v.Text(
            "This is a much longer piece of text that will definitely " +
            "be truncated with an ellipsis character sequence when it " +
            "exceeds the available fixed width of forty columns"
        ).Ellipsis().FixedWidth(40),
        v.Text(""),
        v.Text("Default (Truncate) Mode:"),
        v.Text(
            "This text extends beyond its allocated bounds and " +
            "will be clipped by the parent container if clipping is enabled"
        )
    ])
));

await app.RunAsync();`

</script>

# TextWidget

Display static or dynamic text content in your terminal UI.

## Basic Usage

Create a simple text display using the fluent API:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="text-basic" exampleTitle="Text Widget - Basic Usage" />

## Text Overflow Behavior

TextWidget provides three modes for handling text that exceeds the available width:

<CodeBlock lang="csharp" :code="overflowCode" command="dotnet run" example="text-overflow" exampleTitle="Text Overflow Modes" />

### Truncate (Default)

Text is clipped when it extends beyond its bounds. No visual indicator is shown:

<StaticTerminalPreview svgPath="/svg/text-overflow-truncate.svg" :code="truncateSnippet" />

### Wrap

Text wraps to multiple lines at word boundaries:

<StaticTerminalPreview svgPath="/svg/text-overflow-wrap.svg" :code="wrapSnippet" />

When wrapping:
- Words break at spaces when possible
- Very long words are broken mid-word if necessary
- The widget's measured height increases with the number of lines

### Ellipsis

Text is truncated with "..." when it exceeds the width:

<StaticTerminalPreview svgPath="/svg/text-overflow-ellipsis.svg" :code="ellipsisSnippet" />

## Unicode Support

TextWidget correctly handles Unicode text including:

- **Wide characters** (CJK): 日本語, 中文, 한국어
- **Emoji**: 🎉 🚀 ✨
- **Combining characters**: é, ñ
- **Box-drawing characters**: ┌─┐│└─┘

<StaticTerminalPreview svgPath="/svg/text-unicode.svg" :code="unicodeSnippet" />

## Related Widgets

- [TextBoxWidget](/guide/widgets/textbox) - For editable text input
- [Layout & Stacks](/guide/widgets/stacks) - For arranging text with other widgets
