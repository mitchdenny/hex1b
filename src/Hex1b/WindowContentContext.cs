using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A widget context for building window content that provides access to the window being built.
/// </summary>
/// <typeparam name="TParentWidget">The parent widget type - constrains valid children.</typeparam>
/// <remarks>
/// <para>
/// WindowContentContext extends <see cref="WidgetContext{TParentWidget}"/> with access to
/// the <see cref="Window"/> property, which allows content to reference the window it belongs to.
/// This is particularly useful for closing the window from within its content.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// e.Windows.Window(w =&gt; w.VStack(v =&gt; [
///     v.Text("Hello!"),
///     v.Button("Close").OnClick(ev =&gt; ev.Windows.Close(w.Window))
/// ]));
/// </code>
/// </para>
/// </remarks>
public sealed class WindowContentContext<TParentWidget> : WidgetContext<TParentWidget>
    where TParentWidget : Hex1bWidget
{
    internal WindowContentContext(WindowHandle window)
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
    }

    /// <summary>
    /// The window handle for the window being built.
    /// Use this to close the window or perform other window operations from within the content.
    /// </summary>
    /// <example>
    /// <code>
    /// e.Windows.Window(w =&gt; w.Button("Close").OnClick(ev =&gt; ev.Windows.Close(w.Window)));
    /// </code>
    /// </example>
    public WindowHandle Window { get; }
}
