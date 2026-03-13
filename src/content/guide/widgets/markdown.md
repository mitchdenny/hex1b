<!--
  ⚠️ MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode  → src/Hex1b.Website/Examples/MarkdownBasicExample.cs
  - linksCode  → src/Hex1b.Website/Examples/MarkdownLinksExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import headingsSnippet from './snippets/markdown-headings.cs?raw'
import formattingSnippet from './snippets/markdown-formatting.cs?raw'
import listsSnippet from './snippets/markdown-lists.cs?raw'
import codeBlockSnippet from './snippets/markdown-code-block.cs?raw'
import tableSnippet from './snippets/markdown-table.cs?raw'
import blockquoteSnippet from './snippets/markdown-blockquote.cs?raw'
import documentSnippet from './snippets/markdown-document.cs?raw'

const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VScrollPanel(
        ctx.Markdown("""
            # Welcome to Hex1b

            Render **rich markdown** content in your terminal UI with full support
            for headings, *emphasis*, \`inline code\`, and more.

            ## Features

            - **Bold** and *italic* text formatting
            - Fenced code blocks with line numbers
            - Tables, lists, and block quotes
            - Interactive links with Tab navigation
            - Embedded images via Kitty Graphics Protocol

            ## Code Example

            \\\`\\\`\\\`csharp
            var app = new Hex1bApp(ctx =>
                ctx.Markdown("# Hello, World!")
            );
            await app.RunAsync();
            \\\`\\\`\\\`

            > The MarkdownWidget parses CommonMark-compatible
            > markdown and renders it as a composed widget tree.

            ---

            | Feature        | Status  |
            |:---------------|:-------:|
            | Headings       | ✅ Done |
            | Inline styles  | ✅ Done |
            | Code blocks    | ✅ Done |
            | Tables         | ✅ Done |
            | Links          | ✅ Done |
            """)
    ))
    .Build();

await terminal.RunAsync();`

const linksCode = `using Hex1b;

var lastActivated = "";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.VScrollPanel(
            v.Markdown("""
                # Focusable Links Demo

                Use **Tab** and **Shift+Tab** to navigate between links.
                Press **Enter** to activate a focused link.

                ## Navigation

                - [Hex1b on GitHub](https://github.com/mitchdenny/hex1b)
                - [Getting Started](/guide/getting-started)
                - [Widget Documentation](/guide/widgets/)

                ## Intra-Document Links

                Jump to the [Navigation](#navigation) section above,
                or go to [Resources](#resources) below.

                ## Resources

                Check out the [API Reference](/reference/) for details.
                """)
                .Focusable(children: true)
                .OnLinkActivated(args =>
                {
                    lastActivated = $"{args.Kind}: {args.Url}";
                    args.Handled = true;
                })
        ),
        v.Text(string.IsNullOrEmpty(lastActivated)
            ? "Press Tab to focus a link, Enter to activate"
            : $"Activated → {lastActivated}")
    ]))
    .Build();

await terminal.RunAsync();`
</script>

# MarkdownWidget

Renders markdown source text as a composed terminal widget tree. Supports CommonMark headings, inline formatting, fenced code blocks, GFM tables, lists, block quotes, images, and interactive links.

## Basic Usage

Pass a markdown string to `ctx.Markdown()` inside any layout:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="markdown-basic" exampleTitle="Markdown Widget - Basic Usage" />

## Supported Syntax

### Headings

Four heading levels are supported, rendered with decreasing visual weight:

<StaticTerminalPreview svgPath="/svg/markdown-headings.svg" :code="headingsSnippet" />

### Text Formatting

Inline styles can be combined freely within paragraphs:

<StaticTerminalPreview svgPath="/svg/markdown-formatting.svg" :code="formattingSnippet" />

Supported styles:
- `**bold**` — Bold text
- `*italic*` — Italic (rendered as dim)
- `***bold italic***` — Combined bold and italic
- `` `code` `` — Inline code with distinct background
- `~~strikethrough~~` — Strikethrough text

### Code Blocks

Fenced code blocks are rendered as read-only editors with line numbers. The language tag is displayed but syntax highlighting is not yet supported:

<StaticTerminalPreview svgPath="/svg/markdown-code-block.svg" :code="codeBlockSnippet" />

### Lists

Unordered, ordered, and task lists are all supported, including nesting:

<StaticTerminalPreview svgPath="/svg/markdown-lists.svg" :code="listsSnippet" />

### Tables

GFM-style tables with column alignment (`:---`, `:---:`, `---:`):

<StaticTerminalPreview svgPath="/svg/markdown-table.svg" :code="tableSnippet" />

### Block Quotes

Block quotes support inline formatting and can span multiple lines:

<StaticTerminalPreview svgPath="/svg/markdown-blockquote.svg" :code="blockquoteSnippet" />

### Thematic Breaks

Horizontal rules (`---`, `***`, `___`) render as full-width dividers between content sections.

## Focusable Links

Enable Tab navigation across links with `.Focusable(children: true)`. Links become keyboard-focusable and visually highlight when focused:

<CodeBlock lang="csharp" :code="linksCode" command="dotnet run" example="markdown-links" exampleTitle="Markdown Widget - Focusable Links" />

## Link Activation

Handle link clicks with `.OnLinkActivated()`. The event args include the URL, display text, and a `Kind` property that classifies the link:

| Kind | Description | Example |
|------|-------------|---------|
| `External` | HTTP/HTTPS URLs | `https://example.com` |
| `IntraDocument` | Heading anchors | `#my-heading` |
| `Custom` | Everything else | `mailto:`, `command:` |

Set `args.Handled = true` to suppress default behavior (opening browser, scrolling to heading).

## Image Loading

Load and display embedded images with `.OnImageLoad()`. The callback receives a `Uri` and alt text, and returns pixel data:

```csharp
ctx.Markdown(source)
    .OnImageLoad(async (uri, altText) =>
    {
        var bytes = await File.ReadAllBytesAsync(uri.LocalPath);
        // Decode image to RGBA32 pixel data
        return new MarkdownImageData(rgbaData, width, height);
    })
```

Images are rendered using the Kitty Graphics Protocol. When the loader returns `null`, the image alt text is shown as a text fallback.

## Document Source

For live-updating markdown (e.g., an editor preview), pass an `IHex1bDocument` instead of a string. The widget uses `IHex1bDocument.Version` for efficient change detection—re-parsing only occurs when the version advances:

<StaticTerminalPreview svgPath="/svg/markdown-headings.svg" :code="documentSnippet" />

## Custom Block Rendering

Override how specific block types are rendered with `.OnBlock<TBlock>()`. Handlers form a middleware chain—call `ctx.Default(block)` to fall through to the next handler:

```csharp
ctx.Markdown(source)
    .OnBlock<MarkdownDocument.BlockQuote>((ctx, block) =>
        ctx.RootContext.Border(b =>
            [ctx.Default(block)],
            title: "Note"
        ))
```

Available block types: `Heading`, `Paragraph`, `FencedCode`, `BlockQuote`, `UnorderedList`, `OrderedList`, `Table`, `ThematicBreak`, `TaskList`.

## Related Widgets

- [Text](/guide/widgets/text) — Simple text display
- [Hyperlink](/guide/widgets/hyperlink) — Single clickable hyperlinks (OSC 8)
- [Scroll](/guide/widgets/scroll) — Scrollable containers for long markdown content
- [KgpImage](/guide/widgets/kgpimage) — Direct pixel image rendering
