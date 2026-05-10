using Hex1b.Events;
using Hex1b.Input;

namespace Hex1b.Widgets;

/// <summary>
/// A checkbox widget that displays a toggleable checked / unchecked / indeterminate state.
/// </summary>
/// <param name="Value">The initial value of the checkbox when the framework manages
/// state. Ignored when an external state instance is supplied via
/// <see cref="State(CheckboxState)"/>.</param>
public sealed record CheckboxWidget(CheckboxValue Value = CheckboxValue.Unchecked) : Hex1bWidget,
    IStatefulWidget<CheckboxWidget, CheckboxState>
{
    /// <summary>Action ID for toggling the checkbox state.</summary>
    public static readonly ActionId ToggleActionId = new($"{nameof(CheckboxWidget)}.{nameof(ToggleActionId)}");

    /// <summary>
    /// Optional label displayed after the checkbox.
    /// </summary>
    internal string? LabelText { get; init; }

    /// <summary>
    /// Externally-supplied state instance. When set, the checkbox becomes a pure
    /// view of <see cref="InjectedState"/> — the framework routes this exact
    /// instance into the underlying node on every reconcile and toggle gestures
    /// mutate <see cref="CheckboxState.Value"/> in place. Pair with
    /// <see cref="Composition.CompositionContext.UseState{T}(System.Func{T})"/>
    /// inside a composite parent.
    /// </summary>
    internal CheckboxState? InjectedState { get; init; }

    /// <summary>
    /// Event handler for when the checkbox is toggled.
    /// </summary>
    internal Func<CheckboxToggledEventArgs, Task>? ToggledHandler { get; init; }

    #region Fluent API

    /// <summary>
    /// Sets the checkbox to checked state.
    /// </summary>
    public CheckboxWidget Checked() => this with { Value = CheckboxValue.Checked };

    /// <summary>
    /// Sets the checkbox to unchecked state.
    /// </summary>
    public CheckboxWidget Unchecked() => this with { Value = CheckboxValue.Unchecked };

    /// <summary>
    /// Sets the checkbox to indeterminate state.
    /// </summary>
    public CheckboxWidget Indeterminate() => this with { Value = CheckboxValue.Indeterminate };

    /// <summary>
    /// Sets the label displayed after the checkbox.
    /// </summary>
    public CheckboxWidget Label(string label) => this with { LabelText = label };

    /// <summary>
    /// Returns a copy of the checkbox bound to the supplied <paramref name="state"/>
    /// instance. The widget becomes a pure view of <paramref name="state"/>:
    /// every reconcile assigns this exact instance to the underlying node, and
    /// toggle gestures mutate <see cref="CheckboxState.Value"/> in place so the
    /// parent observes changes without wiring an
    /// <see cref="OnToggled(System.Action{CheckboxToggledEventArgs})"/> handler.
    /// </summary>
    /// <remarks>
    /// When <c>State</c> is supplied, the constructor <see cref="Value"/> argument
    /// is ignored — the parent's state is the single source of truth.
    /// </remarks>
    public CheckboxWidget State(CheckboxState state) => this with { InjectedState = state };

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

        if (InjectedState is not null)
        {
            // Hoisted-state path: parent owns the state, the widget is a view.
            // Mark dirty if the instance reference or the value changed since
            // last frame.
            if (!ReferenceEquals(node.State, InjectedState) || node.Label != LabelText)
            {
                node.MarkDirty();
            }
            node.State = InjectedState;
            node.LastWidgetValue = null;
        }
        else
        {
            // Framework-managed path: drift-detect the widget's Value parameter
            // against the value we last sourced from it. This preserves user
            // toggles between renders while still honoring re-renders that
            // change Value (the typical "parent tracks bool state and rebuilds
            // the widget" pattern).
            if (context.IsNew)
            {
                if (node.State.Value != Value || node.Label != LabelText)
                {
                    node.MarkDirty();
                }
                node.State.Value = Value;
                node.LastWidgetValue = Value;
            }
            else if (Value != node.LastWidgetValue)
            {
                node.State.Value = Value;
                node.LastWidgetValue = Value;
                node.MarkDirty();
            }
            else if (node.Label != LabelText)
            {
                node.MarkDirty();
            }
        }

        node.Label = LabelText;
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
