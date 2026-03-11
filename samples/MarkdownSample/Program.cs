using Hex1b;
using Hex1b.Documents;
using Hex1b.Widgets;

var sampleMarkdown = """
    # Markdown Preview Demo

    This is a **live preview** of markdown content. Edit the text on the left
    and watch the rendered output update on the right.

    ## Features

    - Headings (h1 through h6)
    - Paragraphs with word wrap
    - Fenced code blocks with language tags
    - Block quotes
    - Ordered and unordered lists
    - Thematic breaks
    - [Clickable links](https://github.com) with Tab navigation

    ## Code Example

    ```csharp
    var app = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) => ctx =>
            ctx.Markdown("# Hello World"))
        .Build();
    ```

    > This is a block quote.
    > It can span multiple lines.

    ## Links

    - External: [GitHub](https://github.com)
    - External: [.NET Documentation](https://learn.microsoft.com/dotnet)
    - Intra-document: [Back to top](#markdown-preview-demo)

    Use **Tab** / **Shift+Tab** to cycle through links.
    Press **Enter** to activate the focused link.

    ## Lists

    1. First item
    2. Second item
    3. Third item

    - Bullet one
    - Bullet two
    - Bullet three

    ---

    *Edit the markdown on the left to see changes here.*
    """;

var document = new Hex1bDocument(sampleMarkdown);
var editorState = new EditorState(document);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) =>
    {
        Func<RootContext, Hex1bWidget> builder = ctx =>
            ctx.HSplitter(
                // Left pane: Editor
                ctx.Editor(editorState).LineNumbers(),
                // Right pane: Markdown preview in scroll panel with focusable links
                ctx.VScrollPanel(
                    ctx.Markdown(document.GetText())
                        .Focusable(children: true)
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
