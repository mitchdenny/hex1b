using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for building Accordion widgets.
/// </summary>
public static class AccordionExtensions
{
    /// <summary>
    /// Creates an Accordion widget using a builder pattern.
    /// The accordion fills available vertical space by default.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="builder">A function that builds the sections using an AccordionContext.</param>
    /// <returns>An AccordionWidget.</returns>
    /// <example>
    /// <code>
    /// ctx.Accordion(a => [
    ///     a.Section(s => [s.Text("File list")]).Title("Explorer"),
    ///     a.Section(s => [s.Text("Outline")]).Title("Outline"),
    ///     a.Section(s => [s.Text("Timeline")]).Title("Timeline")
    /// ])
    /// </code>
    /// </example>
    public static AccordionWidget Accordion<TParent>(
        this WidgetContext<TParent> ctx,
        Func<AccordionContext, IEnumerable<AccordionSectionWidget>> builder)
        where TParent : Hex1bWidget
    {
        var accordionContext = new AccordionContext();
        var sections = builder(accordionContext).ToList();
        return new AccordionWidget(sections) { HeightHint = SizeHint.Fill };
    }

    /// <summary>
    /// Creates an Accordion widget with pre-built sections.
    /// The accordion fills available vertical space by default.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="sections">The sections to display.</param>
    /// <returns>An AccordionWidget.</returns>
    public static AccordionWidget Accordion<TParent>(
        this WidgetContext<TParent> ctx,
        IEnumerable<AccordionSectionWidget> sections)
        where TParent : Hex1bWidget
    {
        return new AccordionWidget(sections.ToList()) { HeightHint = SizeHint.Fill };
    }
}
