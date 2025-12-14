using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A node that displays the first child whose condition evaluates to true.
/// Conditions are evaluated during layout with the available size from parent constraints.
/// </summary>
public sealed class ResponsiveNode : Hex1bNode
{
    /// <summary>
    /// The list of conditional branches to evaluate.
    /// </summary>
    public IReadOnlyList<ConditionalWidget> Branches { get; set; } = [];

    /// <summary>
    /// The reconciled child nodes corresponding to each branch.
    /// </summary>
    public List<Hex1bNode?> ChildNodes { get; set; } = new();

    /// <summary>
    /// The index of the currently active branch (-1 if none match).
    /// </summary>
    public int ActiveBranchIndex { get; private set; } = -1;

    /// <summary>
    /// The available width from the last layout pass.
    /// </summary>
    private int _availableWidth;

    /// <summary>
    /// The available height from the last layout pass.
    /// </summary>
    private int _availableHeight;

    /// <summary>
    /// Gets the currently active child node (the first one whose condition is true).
    /// </summary>
    public Hex1bNode? ActiveChild => ActiveBranchIndex >= 0 && ActiveBranchIndex < ChildNodes.Count
        ? ChildNodes[ActiveBranchIndex]
        : null;

    private void EvaluateConditions(int availableWidth, int availableHeight)
    {
        _availableWidth = availableWidth;
        _availableHeight = availableHeight;
        ActiveBranchIndex = -1;
        for (int i = 0; i < Branches.Count; i++)
        {
            if (Branches[i].Condition(availableWidth, availableHeight))
            {
                ActiveBranchIndex = i;
                break;
            }
        }
    }

    public override Size Measure(Constraints constraints)
    {
        // Use max constraints as the available space for condition evaluation
        EvaluateConditions(constraints.MaxWidth, constraints.MaxHeight);

        // Measure only the active child
        if (ActiveChild != null)
        {
            return ActiveChild.Measure(constraints);
        }

        return constraints.Constrain(Size.Zero);
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        // Re-evaluate with actual bounds in case they differ from constraints
        if (bounds.Width != _availableWidth || bounds.Height != _availableHeight)
        {
            EvaluateConditions(bounds.Width, bounds.Height);
        }

        // Arrange only the active child
        ActiveChild?.Arrange(bounds);
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // Only the active child's focusable nodes are available
        if (ActiveChild != null)
        {
            foreach (var focusable in ActiveChild.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Render only the active child (conditions already evaluated in Measure/Arrange)
        ActiveChild?.Render(context);
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (ActiveChild != null) yield return ActiveChild;
    }
}
