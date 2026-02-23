using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Represents a single section within an Accordion.
/// Contains the section title, content builder, and action icons.
/// </summary>
/// <param name="ContentBuilder">A function that builds the section content widgets.</param>
public sealed record AccordionSectionWidget(
    Func<WidgetContext<VStackWidget>, IEnumerable<Hex1bWidget>> ContentBuilder) : Hex1bWidget
{
    /// <summary>
    /// The title displayed in the section header.
    /// </summary>
    public string SectionTitle { get; init; } = "";

    /// <summary>
    /// Whether this section is expanded. When not explicitly set, the accordion
    /// will expand the first section by default.
    /// </summary>
    public bool? IsExpanded { get; init; }

    /// <summary>
    /// Action icons displayed on the left side of the section header.
    /// </summary>
    internal IReadOnlyList<IconWidget> LeftActionIcons { get; init; } = [];

    /// <summary>
    /// Action icons displayed on the right side of the section header.
    /// </summary>
    internal IReadOnlyList<IconWidget> RightActionIcons { get; init; } = [];

    /// <summary>
    /// Sets the title for this section.
    /// </summary>
    /// <param name="title">The title displayed in the section header.</param>
    public AccordionSectionWidget Title(string title)
        => this with { SectionTitle = title };

    /// <summary>
    /// Marks this section as expanded.
    /// </summary>
    /// <param name="expanded">True to expand, false to collapse.</param>
    public AccordionSectionWidget Expanded(bool expanded = true)
        => this with { IsExpanded = expanded };

    /// <summary>
    /// Marks this section as collapsed.
    /// </summary>
    public AccordionSectionWidget Collapsed()
        => this with { IsExpanded = false };

    /// <summary>
    /// Adds action icons to the left side of the section header.
    /// </summary>
    /// <param name="builder">A function that returns the icons to add.</param>
    /// <example>
    /// <code>
    /// a.Section(s => [...])
    ///   .Title("Explorer")
    ///   .LeftActions(la => [la.Icon("📌")])
    /// </code>
    /// </example>
    public AccordionSectionWidget LeftActions(Func<WidgetContext<AccordionSectionWidget>, IEnumerable<IconWidget>> builder)
    {
        var ctx = new WidgetContext<AccordionSectionWidget>();
        var icons = builder(ctx).ToList();
        return this with { LeftActionIcons = icons };
    }

    /// <summary>
    /// Adds action icons to the right side of the section header.
    /// </summary>
    /// <param name="builder">A function that returns the icons to add.</param>
    /// <example>
    /// <code>
    /// a.Section(s => [...])
    ///   .Title("Explorer")
    ///   .RightActions(ra => [ra.Icon("×").OnClick(e => e.Section.Collapse())])
    /// </code>
    /// </example>
    public AccordionSectionWidget RightActions(Func<WidgetContext<AccordionSectionWidget>, IEnumerable<IconWidget>> builder)
    {
        var ctx = new WidgetContext<AccordionSectionWidget>();
        var icons = builder(ctx).ToList();
        return this with { RightActionIcons = icons };
    }

    /// <summary>
    /// Builds the content widget tree for this section.
    /// </summary>
    internal Hex1bWidget? BuildContent(bool includeSpacer)
    {
        var ctx = new WidgetContext<VStackWidget>();
        var children = ContentBuilder(ctx).ToList();
        if (includeSpacer)
        {
            // Inject spacer to fill remaining vertical space within the section
            children.Add(new AccordionSectionSpacerWidget { HeightHint = Layout.SizeHint.Fill });
        }
        return new VStackWidget(children);
    }

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        // AccordionSectionWidget is not directly reconciled - it's used by AccordionNode
        // to build section headers and content. This method should not be called directly.
        throw new InvalidOperationException(
            "AccordionSectionWidget should not be reconciled directly. Use Accordion instead.");
    }

    internal override Type GetExpectedNodeType() => typeof(AccordionNode);
}
