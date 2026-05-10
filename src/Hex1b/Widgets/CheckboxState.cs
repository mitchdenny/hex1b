namespace Hex1b.Widgets;

/// <summary>
/// The three discrete values a checkbox can hold. This is the value layer of the
/// <see cref="CheckboxState"/> model.
/// </summary>
public enum CheckboxValue
{
    /// <summary>The checkbox is unchecked.</summary>
    Unchecked,

    /// <summary>The checkbox is checked.</summary>
    Checked,

    /// <summary>
    /// The checkbox is in an indeterminate state (partially checked).
    /// Used when a parent represents a group with mixed selection.
    /// </summary>
    Indeterminate,
}

/// <summary>
/// Mutable state object for a <see cref="CheckboxWidget"/>. Wraps a single
/// <see cref="Value"/> field so the state can be lifted into a composite parent
/// via <see cref="Composition.CompositionContext.UseState{T}(System.Func{T})"/>
/// and routed into the widget with
/// <see cref="CheckboxWidget.State(CheckboxState)"/>.
/// </summary>
/// <remarks>
/// <para>
/// When the parent owns the state, toggle gestures (Enter, Spacebar, mouse click)
/// mutate <see cref="Value"/> in place — the parent observes the change immediately
/// without an <see cref="CheckboxWidget.OnToggled(System.Action{Events.CheckboxToggledEventArgs})"/>
/// shadow-sync.
/// </para>
/// <para>
/// The static <see cref="Checked"/>, <see cref="Unchecked"/>, and
/// <see cref="Indeterminate"/> properties are convenience factories that allocate
/// a fresh instance with the corresponding value. They are NOT shared singletons —
/// each access returns a new instance, so callers cannot accidentally mutate
/// one another's state.
/// </para>
/// </remarks>
public sealed class CheckboxState
{
    /// <summary>
    /// The current value of the checkbox.
    /// </summary>
    public CheckboxValue Value { get; set; }

    /// <summary>
    /// Creates a new state instance with the supplied <paramref name="value"/>
    /// (defaults to <see cref="CheckboxValue.Unchecked"/>).
    /// </summary>
    public CheckboxState(CheckboxValue value = CheckboxValue.Unchecked)
    {
        Value = value;
    }

    /// <summary>True if the checkbox value is <see cref="CheckboxValue.Checked"/>.</summary>
    public bool IsChecked => Value == CheckboxValue.Checked;

    /// <summary>True if the checkbox value is <see cref="CheckboxValue.Indeterminate"/>.</summary>
    public bool IsIndeterminate => Value == CheckboxValue.Indeterminate;

    /// <summary>
    /// Convenience factory that returns a fresh <see cref="CheckboxState"/> with
    /// <see cref="Value"/> set to <see cref="CheckboxValue.Checked"/>.
    /// </summary>
    public static CheckboxState Checked => new(CheckboxValue.Checked);

    /// <summary>
    /// Convenience factory that returns a fresh <see cref="CheckboxState"/> with
    /// <see cref="Value"/> set to <see cref="CheckboxValue.Unchecked"/>.
    /// </summary>
    public static CheckboxState Unchecked => new(CheckboxValue.Unchecked);

    /// <summary>
    /// Convenience factory that returns a fresh <see cref="CheckboxState"/> with
    /// <see cref="Value"/> set to <see cref="CheckboxValue.Indeterminate"/>.
    /// </summary>
    public static CheckboxState Indeterminate => new(CheckboxValue.Indeterminate);
}
