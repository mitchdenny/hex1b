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
/// The split button renders as: <c>[ Primary ▼ ]</c> where "Primary" is the label.
/// </para>
/// <para>
/// <strong>Interaction:</strong>
/// <list type="bullet">
///   <item><description>Clicking the main label area (or pressing Enter when focused) triggers the primary action.</description></item>
///   <item><description>Clicking the dropdown arrow (▼) opens a popup menu with secondary actions.</description></item>
///   <item><description>Pressing Down arrow when focused also opens the dropdown menu.</description></item>
/// </list>
/// </para>
/// <para>
/// Split buttons are useful when you have a default action that users commonly want,
/// but also need to expose related alternative actions without cluttering the UI.
/// </para>
/// </remarks>
/// <example>
/// <para>A save button with alternative save options:</para>
/// <code>
/// ctx.SplitButton("Save")
///    .OnPrimaryClick(e =&gt; SaveFile())
///    .WithSecondaryAction("Save As...", e =&gt; SaveAs())
///    .WithSecondaryAction("Save All", e =&gt; SaveAll())
///    .WithSecondaryAction("Save Copy", e =&gt; SaveCopy())
/// </code>
/// </example>
/// <seealso cref="ButtonWidget"/>
/// <seealso cref="SplitButtonAction"/>
/// <seealso cref="SplitButtonClickedEventArgs"/>
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
    /// Callback invoked when the dropdown menu is opened.
    /// </summary>
    internal Action? DropdownOpenedCallback { get; init; }

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
    /// <param name="handler">The handler invoked when the primary button is clicked.</param>
    /// <returns>A new widget instance with the handler configured.</returns>
    public SplitButtonWidget OnPrimaryClick(Action<SplitButtonClickedEventArgs> handler)
        => this with { PrimaryHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler for the primary action.
    /// </summary>
    /// <param name="handler">The async handler invoked when the primary button is clicked.</param>
    /// <returns>A new widget instance with the handler configured.</returns>
    public SplitButtonWidget OnPrimaryClick(Func<SplitButtonClickedEventArgs, Task> handler)
        => this with { PrimaryHandler = handler };

    /// <summary>
    /// Sets a callback invoked when the dropdown menu is opened.
    /// </summary>
    /// <param name="callback">The callback invoked when the dropdown opens.</param>
    /// <returns>A new widget instance with the callback configured.</returns>
    /// <remarks>
    /// This is useful for analytics, lazy-loading menu items, or canceling timeouts
    /// when the user is interacting with the button.
    /// </remarks>
    public SplitButtonWidget OnDropdownOpened(Action callback)
        => this with { DropdownOpenedCallback = callback };

    /// <summary>
    /// Adds a secondary action to the dropdown menu.
    /// </summary>
    /// <param name="label">The label for the action.</param>
    /// <param name="handler">The handler invoked when this action is selected.</param>
    /// <returns>A new widget instance with the action added.</returns>
    /// <remarks>
    /// Secondary actions appear in the dropdown menu when the user clicks the arrow (▼)
    /// or presses the Down arrow key. Actions are displayed in the order they were added.
    /// </remarks>
    public SplitButtonWidget WithSecondaryAction(string label, Action<SplitButtonClickedEventArgs> handler)
    {
        var actions = SecondaryActions.ToList();
        actions.Add(new SplitButtonAction(label, args => { handler(args); return Task.CompletedTask; }));
        return this with { SecondaryActions = actions };
    }

    /// <summary>
    /// Adds an asynchronous secondary action to the dropdown menu.
    /// </summary>
    /// <param name="label">The label for the action.</param>
    /// <param name="handler">The async handler invoked when this action is selected.</param>
    /// <returns>A new widget instance with the action added.</returns>
    /// <remarks>
    /// Secondary actions appear in the dropdown menu when the user clicks the arrow (▼)
    /// or presses the Down arrow key. Actions are displayed in the order they were added.
    /// </remarks>
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
        node.DropdownOpenedCallback = DropdownOpenedCallback;

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
/// Represents a secondary action in a split button dropdown menu.
/// </summary>
/// <param name="Label">The action label displayed in the dropdown menu.</param>
/// <param name="Handler">The async handler invoked when the action is selected.</param>
/// <seealso cref="SplitButtonWidget"/>
public sealed record SplitButtonAction(
    string Label,
    Func<SplitButtonClickedEventArgs, Task> Handler);
