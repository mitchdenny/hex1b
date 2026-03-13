using Hex1b.Events;
using Hex1b.Markdown;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Internal widget that loads and renders an image via a user-provided
/// <see cref="MarkdownImageLoader"/> callback. Caches the loaded image data
/// across frames and displays a <see cref="KgpImageWidget"/> when the terminal
/// supports the Kitty Graphics Protocol, or a text fallback otherwise.
/// </summary>
internal sealed record MarkdownImageWidget(
    string Url,
    string AltText,
    MarkdownImageLoader Loader,
    bool FocusableLinks,
    Func<MarkdownLinkActivatedEventArgs, Task>? LinkActivatedHandler,
    MarkdownWidget? SourceWidget) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(
        Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as MarkdownImageNode ?? new MarkdownImageNode();

        // Only reload when URL changes
        if (node.CachedUrl != Url)
        {
            node.CachedUrl = Url;
            node.MarkDirty();

            try
            {
                var uri = new Uri(Url, UriKind.RelativeOrAbsolute);
                node.CachedImageData = await Loader(uri, AltText);
            }
            catch
            {
                node.CachedImageData = null;
            }
        }

        // Build the child widget based on whether image data is available
        Hex1bWidget childWidget;
        if (node.CachedImageData is { } data)
        {
            var fallback = BuildFallback();
            childWidget = new KgpImageWidget(data.ImageData, data.PixelWidth, data.PixelHeight, fallback);
        }
        else
        {
            childWidget = BuildFallback();
        }

        node.ContentChild = await context.ReconcileChildAsync(
            node.ContentChild, childWidget, node);

        return node;
    }

    private Hex1bWidget BuildFallback()
    {
        // Render as italic [alt text] link, same as inline image rendering
        var inlines = new List<MarkdownInline>
        {
            new ImageInline(AltText, Url, null)
        };

        return new MarkdownTextBlockWidget(inlines)
        {
            FocusableLinks = FocusableLinks,
            LinkActivatedHandler = LinkActivatedHandler,
            SourceWidget = SourceWidget
        };
    }

    internal override Type GetExpectedNodeType() => typeof(MarkdownImageNode);
}
