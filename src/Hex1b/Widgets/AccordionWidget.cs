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
        var isFirstRender = node.SectionCount == 0;

        node.AllowMultipleExpanded = AllowMultipleExpanded;
        node.SectionExpandedHandler = SectionExpandedHandler;

        // Build section info list (display data only, no expanded state)
        var sections = new List<AccordionNode.SectionInfo>(Sections.Count);
        for (int i = 0; i < Sections.Count; i++)
        {
            var section = Sections[i];
            sections.Add(new AccordionNode.SectionInfo(
                section.SectionTitle,
                section.LeftActionIcons,
                section.RightActionIcons));
        }

        node.SetSections(sections);

        // Apply expanded states
        if (isFirstRender)
        {
            // First render: use explicit IsExpanded or default (first section expanded)
            for (int i = 0; i < Sections.Count; i++)
            {
                var expanded = Sections[i].IsExpanded ?? (i == 0);
                node.SetExpandedState(i, expanded);
            }
        }
        else
        {
            // Subsequent renders: only override explicitly controlled sections
            for (int i = 0; i < Sections.Count; i++)
            {
                if (Sections[i].IsExpanded.HasValue)
                {
                    node.SetExpandedState(i, Sections[i].IsExpanded!.Value);
                }
                // Otherwise preserve the node's current state (user toggles preserved)
            }
        }

        // Reconcile content for expanded sections
        // Only the first expanded section gets the spacer to fill remaining vertical space
        var firstExpandedFound = false;
        for (int i = 0; i < Sections.Count; i++)
        {
            if (node.IsSectionExpanded(i))
            {
                var includeSpacer = !firstExpandedFound;
                firstExpandedFound = true;
                var contentWidget = Sections[i].BuildContent(includeSpacer);
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
