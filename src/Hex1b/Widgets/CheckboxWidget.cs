using Hex1b.Events;

namespace Hex1b.Widgets;

/// <summary>
/// A checkbox widget that displays a toggleable checked/unchecked/indeterminate state.
/// </summary>
/// <param name="State">The current state of the checkbox.</param>
public sealed record CheckboxWidget(CheckboxState State = CheckboxState.Unchecked) : Hex1bWidget
{
    /// <summary>
    /// Optional label displayed after the checkbox.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Event handler for when the checkbox is toggled.
    /// </summary>
    internal Func<CheckboxToggledEventArgs, Task>? ToggledHandler { get; init; }

    #region Fluent API

    /// <summary>
    /// Sets the checkbox to checked state.
    /// </summary>
    public CheckboxWidget Checked() => this with { State = CheckboxState.Checked };

    /// <summary>
    /// Sets the checkbox to unchecked state.
    /// </summary>
    public CheckboxWidget Unchecked() => this with { State = CheckboxState.Unchecked };

    /// <summary>
    /// Sets the checkbox to indeterminate state.
    /// </summary>
    public CheckboxWidget Indeterminate() => this with { State = CheckboxState.Indeterminate };

    /// <summary>
    /// Sets the checkbox state.
    /// </summary>
    public CheckboxWidget WithState(CheckboxState state) => this with { State = state };

    /// <summary>
    /// Sets the label displayed after the checkbox.
    /// </summary>
    public CheckboxWidget WithLabel(string label) => this with { Label = label };

    /// <summary>
    /// Sets a synchronous handler called when the checkbox is toggled.
    /// </summary>
    public CheckboxWidget OnToggled(Action<CheckboxToggledEventArgs> handler)
        => this with { ToggledHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when the checkbox is toggled.
    /// </summary>
    public CheckboxWidget OnToggled(Func<CheckboxToggledEventArgs, Task> handler)
        => this with { ToggledHandler = handler };

    #endregion

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as CheckboxNode ?? new CheckboxNode();

        if (node.State != State || node.Label != Label)
        {
            node.MarkDirty();
        }

        node.State = State;
        node.Label = Label;
        node.SourceWidget = this;

        if (ToggledHandler != null)
        {
            node.ToggledCallback = async ctx =>
            {
                var args = new CheckboxToggledEventArgs(this, node, ctx);
                await ToggledHandler(args);
            };
        }
        else
        {
            node.ToggledCallback = null;
        }

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(CheckboxNode);
}
