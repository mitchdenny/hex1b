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
    /// Whether multiple sections can be expanded simultaneously. Defaults to false.
    /// </summary>
    public bool AllowMultipleExpanded { get; init; } = false;

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
            var sectionIndex = i;

            // Build left actions; prepend a default toggle if user didn't provide one
            var leftActions = new List<AccordionSectionAction>();
            var hasToggle = section.LeftSectionActions.Any(a => a.IsToggle);
            if (!hasToggle)
            {
                leftActions.Add(AccordionSectionActionBuilder.DefaultToggle());
            }
            foreach (var a in section.LeftSectionActions)
            {
                leftActions.Add(WireToggleHandler(a, node, sectionIndex));
            }

            // Build right actions
            var rightActions = new List<AccordionSectionAction>();
            foreach (var a in section.RightSectionActions)
            {
                rightActions.Add(WireToggleHandler(a, node, sectionIndex));
            }

            sections.Add(new AccordionNode.SectionInfo(
                section.SectionTitle,
                leftActions,
                rightActions));
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

    /// <summary>
    /// Wires up toggle actions with the node and section index so they can call ToggleSection.
    /// Non-toggle actions are returned as-is.
    /// </summary>
    private static AccordionSectionAction WireToggleHandler(AccordionSectionAction action, AccordionNode node, int sectionIndex)
    {
        if (!action.IsToggle)
            return action;

        // Ensure toggle actions have a click handler that toggles the section
        return action with
        {
            ClickHandler = action.ClickHandler ?? (ctx => ctx.Toggle())
        };
    }

    internal override Type GetExpectedNodeType() => typeof(AccordionNode);
}
