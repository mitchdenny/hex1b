namespace Hex1b.Widgets;

/// <summary>
/// Provides a fluent API context for building Accordion structures.
/// This context exposes section-creation methods to guide developers toward the correct API usage.
/// </summary>
public readonly struct AccordionContext
{
    /// <summary>
    /// Creates a section with the specified content builder.
    /// Use <see cref="AccordionSectionWidget.Title"/> to set the section title.
    /// </summary>
    /// <param name="contentBuilder">A function that builds the section content widgets.</param>
    /// <returns>An AccordionSectionWidget configured with the specified content.</returns>
    /// <example>
    /// <code>
    /// a.Section(s => [s.Text("Content here")]).Title("My Section")
    /// </code>
    /// </example>
    public AccordionSectionWidget Section(
        Func<WidgetContext<VStackWidget>, IEnumerable<Hex1bWidget>> contentBuilder)
    {
        return new AccordionSectionWidget(contentBuilder);
    }

    /// <summary>
    /// Creates a section with the specified title and content builder.
    /// </summary>
    /// <param name="title">The title displayed in the section header.</param>
    /// <param name="contentBuilder">A function that builds the section content widgets.</param>
    /// <returns>An AccordionSectionWidget configured with the specified title and content.</returns>
    public AccordionSectionWidget Section(
        string title,
        Func<WidgetContext<VStackWidget>, IEnumerable<Hex1bWidget>> contentBuilder)
    {
        return new AccordionSectionWidget(contentBuilder) { SectionTitle = title };
    }
}
