using Hex1b;
using Hex1b.Documents;
using Hex1b.Markdown;
using Hex1b.Widgets;

var sampleMarkdown = """
    # Markdown Preview Demo

    This is a **live preview** of markdown content. Edit the text on the left
    and watch the rendered output update on the right.

    ## Inline Styles

    You can use **bold**, *italic*, ***bold italic***, `inline code`,
    and ~~strikethrough~~ text. Combine them: ~~**bold strikethrough**~~
    and ~~*italic strikethrough*~~ work too.

    ## Code Blocks

    Fenced code blocks render as read-only editors with line numbers:

    ```csharp
    var app = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) => ctx =>
            ctx.Markdown("# Hello World"))
        .Build();
    await app.RunAsync();
    ```

    Indented code blocks (4 spaces) also work:

        $ dotnet add package Hex1b
        $ dotnet run

    ## Block Quotes

    > This is a block quote that demonstrates word wrapping.
    > When the content is long enough it will wrap to multiple
    > lines with the bar character on each line.

    ## Task Lists

    - [x] Implement parser
    - [x] Inline styling (bold, italic, code, strikethrough)
    - [x] Focusable links with Tab navigation
    - [x] GFM tables with column alignment
    - [x] Reference-style links
    - [x] Image embedding via KGP
    - [x] Live document editing
    - [ ] Syntax highlighting in code blocks

    ## Links

    - External: [GitHub](https://github.com)
    - Reference: [Hex1b repo][hex1b] uses reference-style links
    - Collapsed: [hex1b][] also works
    - Intra-document: [Back to top](#markdown-preview-demo)
    - Intra-document: [Jump to lists](#lists)

    [hex1b]: https://github.com/hex1b/hex1b
    [docs]: https://hex1b.dev "Hex1b Documentation"

    Use **Tab** / **Shift+Tab** to cycle through links.
    Press **Enter** to activate the focused link.

    ## Lists

    1. First ordered item
    2. Second ordered item
    3. Third ordered item

    - Top level bullet
      - Nested bullet (◦)
        - Deeply nested (▪)
      - Another nested item
    - Back to top level

    ## Tables

    | Feature | Status | Priority |
    |:--------|:------:|----------:|
    | Parser | ✅ Done | High |
    | Inline styles | ✅ Done | High |
    | Links | ✅ Done | Medium |
    | Tables | ✅ Done | Medium |
    | Images | ✅ Done | Medium |
    | Live editing | ✅ Done | Medium |
    | Syntax highlighting | 🔄 Planned | Low |

    ## Images

    ![Gradient](gradient.png)

    ### Heading Levels

    # Heading 1
    ## Heading 2
    ### Heading 3
    #### Heading 4
    ##### Heading 5
    ###### Heading 6

    ---

    *Edit the markdown on the left to see changes here.*
    """;

var document = new Hex1bDocument(sampleMarkdown);
var editorState = new EditorState(document);

// Generate a simple gradient image for the demo (no external files needed)
static MarkdownImageData CreateGradientImage(int width, int height)
{
    var rgba = new byte[width * height * 4];
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            int i = (y * width + x) * 4;
            rgba[i] = (byte)(x * 255 / width);      // R: left-to-right
            rgba[i + 1] = (byte)(y * 255 / height);  // G: top-to-bottom
            rgba[i + 2] = 128;                        // B: constant
            rgba[i + 3] = 255;                        // A: opaque
        }
    }
    return new MarkdownImageData(rgba, width, height);
}

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) =>
    {
        Func<RootContext, Hex1bWidget> builder = ctx =>
            ctx.HSplitter(
                // Left pane: Editor
                ctx.Editor(editorState).LineNumbers(),
                // Right pane: Markdown preview in scroll panel with focusable links
                ctx.VScrollPanel(
                    ctx.Markdown(document)
                        .Focusable(children: true)
                        .OnImageLoad((uri, alt) =>
                            Task.FromResult<MarkdownImageData?>(CreateGradientImage(120, 60)))
                        .OnLinkActivated(args =>
                        {
                            // Log link activation to the status bar
                            // For external links, the default handler opens the browser
                            // Set args.Handled = true to suppress default behavior
                        })
                ),
                leftWidth: 45
            );
        return builder;
    })
    .WithMouse()
    .Build();

await terminal.RunAsync();
