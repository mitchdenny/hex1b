using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A collapsible section container where each section has a title header and expandable content area.
/// Sections can be expanded and collapsed independently.
/// </summary>
/// <param name="Sections">The list of sections to display.</param>
/// <example>
/// <code>
/// ctx.Accordion(a => [
///     a.Section(s => [s.Text("File list")]).Title("Explorer"),
///     a.Section(s => [s.Text("Outline")]).Title("Outline"),
///     a.Section(s => [s.Text("Timeline")]).Title("Timeline")
/// ])
/// </code>
/// </example>
public sealed record AccordionWidget(IReadOnlyList<AccordionSectionWidget> Sections) : Hex1bWidget
{
    /// <summary>
    /// Handler called when a section is expanded or collapsed.
    /// </summary>
    internal Func<AccordionSectionExpandedEventArgs, Task>? SectionExpandedHandler { get; init; }

    /// <summary>
    /// Whether multiple sections can be expanded simultaneously. Defaults to true.
    /// </summary>
    public bool AllowMultipleExpanded { get; init; } = true;

    /// <summary>
    /// Sets a synchronous handler for section expand/collapse changes.
    /// </summary>
    public AccordionWidget OnSectionExpanded(Action<AccordionSectionExpandedEventArgs> handler)
        => this with { SectionExpandedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler for section expand/collapse changes.
    /// </summary>
    public AccordionWidget OnSectionExpanded(Func<AccordionSectionExpandedEventArgs, Task> handler)
        => this with { SectionExpandedHandler = handler };

    /// <summary>
    /// Sets whether multiple sections can be expanded simultaneously.
    /// When false, expanding one section collapses all others.
    /// </summary>
    public AccordionWidget MultipleExpanded(bool allow = true)
        => this with { AllowMultipleExpanded = allow };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as AccordionNode ?? new AccordionNode();

        node.AllowMultipleExpanded = AllowMultipleExpanded;
        node.SectionExpandedHandler = SectionExpandedHandler;

        // Determine expanded states
        var expandedStates = new bool[Sections.Count];
        var hasExplicitExpansion = Sections.Any(s => s.IsExpanded.HasValue);

        if (hasExplicitExpansion)
        {
            // Controlled: use explicit IsExpanded values
            for (int i = 0; i < Sections.Count; i++)
            {
                expandedStates[i] = Sections[i].IsExpanded ?? false;
            }
        }
        else if (node.SectionCount == 0)
        {
            // First render: expand only the first section
            for (int i = 0; i < Sections.Count; i++)
            {
                expandedStates[i] = i == 0;
            }
        }
        else
        {
            // Uncontrolled: preserve existing expanded states
            for (int i = 0; i < Sections.Count; i++)
            {
                expandedStates[i] = i < node.SectionCount && node.IsSectionExpanded(i);
            }
        }

        // Build section info list
        var sections = new List<AccordionNode.SectionInfo>(Sections.Count);
        for (int i = 0; i < Sections.Count; i++)
        {
            var section = Sections[i];
            sections.Add(new AccordionNode.SectionInfo(
                section.SectionTitle,
                expandedStates[i],
                section.LeftActionIcons,
                section.RightActionIcons));
        }

        node.SetSections(sections);

        // Reconcile content for expanded sections
        for (int i = 0; i < Sections.Count; i++)
        {
            if (expandedStates[i])
            {
                var contentWidget = Sections[i].BuildContent();
                var existingContent = node.GetSectionContent(i);
                var contentNode = await context.ReconcileChildAsync(existingContent, contentWidget, node);
                node.SetSectionContent(i, contentNode);
            }
            else
            {
                node.SetSectionContent(i, null);
            }
        }

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(AccordionNode);
}
