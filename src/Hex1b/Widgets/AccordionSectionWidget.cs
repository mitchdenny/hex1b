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
    /// Actions displayed on the left side of the section header.
    /// A default toggle chevron is prepended if no toggle action is included.
    /// </summary>
    internal IReadOnlyList<AccordionSectionAction> LeftSectionActions { get; init; } = [];

    /// <summary>
    /// Actions displayed on the right side of the section header.
    /// </summary>
    internal IReadOnlyList<AccordionSectionAction> RightSectionActions { get; init; } = [];

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
    /// Adds actions to the left side of the section header.
    /// A default toggle chevron is prepended unless a toggle action is included.
    /// </summary>
    /// <param name="builder">A function that returns the actions to add.</param>
    /// <example>
    /// <code>
    /// a.Section(s => [...])
    ///   .Title("Explorer")
    ///   .LeftActions(la => [la.Toggle("▶", "▼"), la.Icon("📌").OnClick(ctx => ctx.Collapse())])
    /// </code>
    /// </example>
    public AccordionSectionWidget LeftActions(Func<AccordionSectionActionBuilder, IEnumerable<AccordionSectionAction>> builder)
    {
        var ctx = new AccordionSectionActionBuilder();
        var actions = builder(ctx).ToList();
        return this with { LeftSectionActions = actions };
    }

    /// <summary>
    /// Adds actions to the right side of the section header.
    /// </summary>
    /// <param name="builder">A function that returns the actions to add.</param>
    /// <example>
    /// <code>
    /// a.Section(s => [...])
    ///   .Title("Explorer")
    ///   .RightActions(ra => [ra.Icon("+").OnClick(ctx => ...), ra.Collapse()])
    /// </code>
    /// </example>
    public AccordionSectionWidget RightActions(Func<AccordionSectionActionBuilder, IEnumerable<AccordionSectionAction>> builder)
    {
        var ctx = new AccordionSectionActionBuilder();
        var actions = builder(ctx).ToList();
        return this with { RightSectionActions = actions };
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
            children.Add(new AccordionSectionSpacerWidget { HeightHint = Layout.SizeHint.Fill });
        }
        return new VStackWidget(children);
    }

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        throw new InvalidOperationException(
            "AccordionSectionWidget should not be reconciled directly. Use Accordion instead.");
    }

    internal override Type GetExpectedNodeType() => typeof(AccordionNode);
}
