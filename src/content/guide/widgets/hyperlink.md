<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode    → src/Hex1b.Website/Examples/HyperlinkBasicExample.cs
  - overflowCode → src/Hex1b.Website/Examples/HyperlinkOverflowExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import basicSnippet from './snippets/hyperlink-basic.cs?raw'
import wrapSnippet from './snippets/hyperlink-wrap.cs?raw'
import ellipsisSnippet from './snippets/hyperlink-ellipsis.cs?raw'
import onclickSnippet from './snippets/hyperlink-onclick.cs?raw'
import withIdSnippet from './snippets/hyperlink-with-id.cs?raw'
import paramsSnippet from './snippets/hyperlink-params.cs?raw'

const basicCode = `using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("Hyperlink Examples"),
        v.Text(""),
        v.Hyperlink("Visit Hex1b Docs", "https://hex1b.dev"),
        v.Hyperlink("GitHub Repository", "https://github.com/mitchdenny/hex1b"),
        v.Text(""),
        v.Text("Press Tab to navigate, Enter to activate")
    ])
));

await app.RunAsync();`

const overflowCode = `using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("═══ Hyperlink Overflow Modes ═══"),
        v.Text(""),
        v.Text("Default (Truncate):"),
        v.Hyperlink(
            "This is a very long hyperlink text that will be truncated when it exceeds the width",
            "https://example.com"
        ),
        v.Text(""),
        v.Text("Wrap Mode:"),
        v.Hyperlink(
            "This hyperlink has wrapping enabled so the text will break across " +
            "multiple lines at word boundaries when needed",
            "https://example.com"
        ).Wrap(),
        v.Text(""),
        v.Text("Ellipsis Mode:"),
        v.Hyperlink(
            "This hyperlink shows ellipsis when text is too long to fit in the available space",
            "https://example.com"
        ).Ellipsis().FixedWidth(50)
    ])
));

await app.RunAsync();`

const clickHandlerCode = `using Hex1b;
using Hex1b.Widgets;

var clickCount = 0;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text($"Link clicked {clickCount} times"),
        v.Text(""),
        v.Hyperlink("Click me!", "https://example.com")
            .OnClick(e => {
                clickCount++;
                Console.WriteLine($"Navigating to: {e.Uri}");
            })
    ])
));

await app.RunAsync();`
</script>

# HyperlinkWidget

Display clickable hyperlinks in your terminal UI using [OSC 8](https://gist.github.com/egmontkob/eb114294efbcd5adb1944c9f3cb5feda) escape sequences.

In terminals that support OSC 8 (most modern terminals like iTerm2, Windows Terminal, GNOME Terminal, and Konsole), the text renders as a clickable link. In unsupported terminals, the text displays normally without link functionality.

## Basic Usage

Create hyperlinks using the fluent API:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="hyperlink-basic" exampleTitle="Hyperlink Widget - Basic Usage" />

::: info Web Terminal Note
In the live demo above, use **Shift+Click** to open links. Native terminals typically use Ctrl+Click (or Cmd+Click on macOS), but xterm.js reserves Shift+Click for bypassing mouse tracking mode.
:::

Hyperlinks are focusable widgets. Users can navigate between links using Tab and activate them with Enter.

## Click Handlers

Handle link activation in your application:

<CodeBlock lang="csharp" :code="clickHandlerCode" command="dotnet run" example="hyperlink-click" exampleTitle="Hyperlink Click Handler" />

::: info Web Terminal Note
In the live demo above, use **Shift+Click** to open links. Native terminals typically use Ctrl+Click (or Cmd+Click on macOS), but xterm.js reserves Shift+Click for bypassing mouse tracking mode.
:::

The `OnClick` handler receives `HyperlinkClickedEventArgs` with access to:
- `Uri` - The link's target URL
- `Text` - The visible link text
- `Context` - Access to the application context for state updates

::: tip Terminal-Native Links
Even without an `OnClick` handler, OSC 8 links remain clickable in supported terminals. The terminal handles navigation natively when users Ctrl+Click or Cmd+Click the link.
:::

## Text Overflow Behavior

HyperlinkWidget supports the same overflow modes as TextWidget:

<CodeBlock lang="csharp" :code="overflowCode" command="dotnet run" example="hyperlink-overflow" exampleTitle="Hyperlink Overflow Modes" />

::: info Web Terminal Note
In the live demo above, use **Shift+Click** to open links. Native terminals typically use Ctrl+Click (or Cmd+Click on macOS), but xterm.js reserves Shift+Click for bypassing mouse tracking mode.
:::

### Truncate (Default)

Text is clipped when it extends beyond its bounds:

<StaticTerminalPreview svgPath="/svg/hyperlink-truncate.svg" :code="basicSnippet" />

### Wrap

Text wraps to multiple lines at word boundaries:

<StaticTerminalPreview svgPath="/svg/hyperlink-wrap.svg" :code="wrapSnippet" />

### Ellipsis

Text is truncated with "..." when it exceeds the width:

<StaticTerminalPreview svgPath="/svg/hyperlink-ellipsis.svg" :code="ellipsisSnippet" />

## Link Parameters

OSC 8 supports optional parameters for advanced link behavior:

### Grouping Links with IDs

Use `WithId` to group multiple hyperlink segments as a single logical link. This is useful when a link spans multiple lines or elements:

<StaticTerminalPreview svgPath="/svg/hyperlink-with-id.svg" :code="withIdSnippet" />

When grouped, terminals highlight all segments together on hover.

### Custom Parameters

Use `WithParameters` for arbitrary OSC 8 parameters:

<StaticTerminalPreview svgPath="/svg/hyperlink-truncate.svg" :code="paramsSnippet" />

::: tip
In practice, only the `id` parameter is routinely used. For link grouping, prefer the `WithId` method shown above—it's more readable and type-safe.
:::

## Default Bindings

HyperlinkWidget registers these input bindings when a click handler is set:

| Input | Action |
|-------|--------|
| Enter | Open link |
| Left mouse click | Open link |

::: tip Custom Bindings
You can add additional keyboard shortcuts or custom bindings to any widget. See the [Input Handling](/guide/input) guide for details on working with input bindings.
:::

## Related Widgets

- [TextWidget](/guide/widgets/text) - For non-interactive text display
- [ButtonWidget](/guide/widgets/button) - For button-style interactions
