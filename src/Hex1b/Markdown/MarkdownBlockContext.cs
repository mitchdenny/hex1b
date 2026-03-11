using Hex1b.Widgets;

namespace Hex1b.Markdown;

/// <summary>
/// Context passed to block handler callbacks during markdown rendering.
/// Provides access to the default/next handler in the middleware chain
/// and widget building utilities.
/// </summary>
public sealed class MarkdownBlockContext
{
    private readonly Func<MarkdownBlock, Hex1bWidget> _defaultHandler;
    private readonly WidgetContext<MarkdownWidget> _widgetContext;

    internal MarkdownBlockContext(
        Func<MarkdownBlock, Hex1bWidget> defaultHandler,
        WidgetContext<MarkdownWidget> widgetContext)
    {
        _defaultHandler = defaultHandler;
        _widgetContext = widgetContext;
    }

    /// <summary>
    /// Invoke the next handler in the chain (or the built-in default renderer)
    /// for the given block. Use this to wrap or augment default rendering.
    /// </summary>
    /// <param name="block">The block to render with the next handler.</param>
    /// <returns>The widget produced by the next handler.</returns>
    public Hex1bWidget Default(MarkdownBlock block) => _defaultHandler(block);

    /// <summary>
    /// Access the widget context for building child widgets.
    /// </summary>
    public WidgetContext<MarkdownWidget> Ctx => _widgetContext;
}
