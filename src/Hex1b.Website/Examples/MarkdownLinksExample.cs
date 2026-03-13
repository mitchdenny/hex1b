using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Markdown Widget Documentation: Focusable Links
/// Demonstrates Tab-navigable links within markdown content.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the linksCode sample in:
/// src/content/guide/widgets/markdown.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class MarkdownLinksExample(ILogger<MarkdownLinksExample> logger) : Hex1bExample
{
    private readonly ILogger<MarkdownLinksExample> _logger = logger;

    public override string Id => "markdown-links";
    public override string Title => "Markdown Widget - Focusable Links";
    public override string Description => "Demonstrates focusable link navigation in markdown";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating markdown links example widget builder");

        var lastActivated = "";

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
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
            ]);
        };
    }
}
