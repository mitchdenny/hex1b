using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="FormWidget"/> instances.
/// </summary>
public static class FormExtensions
{
    /// <summary>
    /// Creates a form container. The builder receives a <see cref="FormContext"/>
    /// which provides form-specific extensions (<c>form.TextField(...)</c>) alongside
    /// standard widget methods (<c>form.Text(...)</c>, <c>form.Separator()</c>).
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.Form(form =>
    /// {
    ///     var name = form.TextField("Name");
    ///     return [name, form.SubmitButton("Save", e => { })];
    /// })
    /// </code>
    /// </example>
    public static FormWidget Form<TParent>(
        this WidgetContext<TParent> ctx,
        Func<FormContext, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var formCtx = new FormContext();
        var children = builder(formCtx);
        return new FormWidget(children) { FieldRegistry = formCtx.FieldRegistry };
    }
}
