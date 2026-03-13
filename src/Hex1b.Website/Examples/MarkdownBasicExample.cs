using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Markdown Widget Documentation: Basic Usage
/// Demonstrates rendering markdown content with common syntax.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/markdown.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class MarkdownBasicExample(ILogger<MarkdownBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<MarkdownBasicExample> _logger = logger;

    public override string Id => "markdown-basic";
    public override string Title => "Markdown Widget - Basic Usage";
    public override string Description => "Demonstrates rendering markdown content";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating markdown basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VScrollPanel(
                ctx.Markdown("""
                    # Welcome to Hex1b

                    Render **rich markdown** content in your terminal UI with full support
                    for headings, *emphasis*, `inline code`, and more.

                    ## Features

                    - **Bold** and *italic* text formatting
                    - Fenced code blocks with line numbers
                    - Tables, lists, and block quotes
                    - Interactive links with Tab navigation
                    - Embedded images via Kitty Graphics Protocol

                    ## Code Example

                    ```csharp
                    var app = new Hex1bApp(ctx =>
                        ctx.Markdown("# Hello, World!")
                    );
                    await app.RunAsync();
                    ```

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
            );
        };
    }
}
