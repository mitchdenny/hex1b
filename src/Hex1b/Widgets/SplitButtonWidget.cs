using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A split button with a primary action and a dropdown for secondary actions.
/// The main button area triggers the primary action, while the dropdown arrow opens a menu.
/// </summary>
/// <remarks>
/// <para>
/// The split button renders as: <c>[ Primary ▼ ]</c>
/// </para>
/// <para>
/// Clicking the main area activates the primary action. Clicking the dropdown arrow (▼)
/// opens a popup menu with secondary actions.
/// </para>
/// </remarks>
public sealed record SplitButtonWidget : Hex1bWidget
{
    /// <summary>
    /// The label for the primary action button.
    /// </summary>
    public string PrimaryLabel { get; init; } = "";

    /// <summary>
    /// The handler for the primary action.
    /// </summary>
    internal Func<SplitButtonClickedEventArgs, Task>? PrimaryHandler { get; init; }

    /// <summary>
    /// The secondary actions shown in the dropdown menu.
    /// </summary>
    internal IReadOnlyList<SplitButtonAction> SecondaryActions { get; init; } = [];

    /// <summary>
    /// Creates a split button with the specified primary label.
    /// </summary>
    /// <param name="primaryLabel">The label for the primary action.</param>
    public SplitButtonWidget(string primaryLabel)
    {
        PrimaryLabel = primaryLabel;
    }

    /// <summary>
    /// Sets a synchronous handler for the primary action.
    /// </summary>
    public SplitButtonWidget OnPrimaryClick(Action<SplitButtonClickedEventArgs> handler)
        => this with { PrimaryHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler for the primary action.
    /// </summary>
    public SplitButtonWidget OnPrimaryClick(Func<SplitButtonClickedEventArgs, Task> handler)
        => this with { PrimaryHandler = handler };

    /// <summary>
    /// Adds a secondary action to the dropdown menu.
    /// </summary>
    /// <param name="label">The label for the action.</param>
    /// <param name="handler">The handler for the action.</param>
    public SplitButtonWidget WithSecondaryAction(string label, Action<SplitButtonClickedEventArgs> handler)
    {
        var actions = SecondaryActions.ToList();
        actions.Add(new SplitButtonAction(label, args => { handler(args); return Task.CompletedTask; }));
        return this with { SecondaryActions = actions };
    }

    /// <summary>
    /// Adds an async secondary action to the dropdown menu.
    /// </summary>
    /// <param name="label">The label for the action.</param>
    /// <param name="handler">The async handler for the action.</param>
    public SplitButtonWidget WithSecondaryAction(string label, Func<SplitButtonClickedEventArgs, Task> handler)
    {
        var actions = SecondaryActions.ToList();
        actions.Add(new SplitButtonAction(label, handler));
        return this with { SecondaryActions = actions };
    }

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as SplitButtonNode ?? new SplitButtonNode();

        if (node.PrimaryLabel != PrimaryLabel)
        {
            node.MarkDirty();
        }

        node.PrimaryLabel = PrimaryLabel;
        node.SourceWidget = this;
        node.SecondaryActions = SecondaryActions;

        if (PrimaryHandler != null)
        {
            node.PrimaryAction = async ctx =>
            {
                var args = new SplitButtonClickedEventArgs(this, node, ctx);
                await PrimaryHandler(args);
            };
        }
        else
        {
            node.PrimaryAction = null;
        }

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(SplitButtonNode);
}

/// <summary>
/// Represents a secondary action in a split button dropdown.
/// </summary>
/// <param name="Label">The action label.</param>
/// <param name="Handler">The action handler.</param>
public sealed record SplitButtonAction(
    string Label,
    Func<SplitButtonClickedEventArgs, Task> Handler);
