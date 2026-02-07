namespace Hex1b;

/// <summary>
/// Provides context for window result callbacks when a dialog closes with a typed result.
/// </summary>
/// <typeparam name="T">The type of the result value.</typeparam>
/// <remarks>
/// <para>
/// WindowResultContext is passed to <c>OnResult</c> callbacks when a window closes,
/// providing access to the result value and cancellation state.
/// </para>
/// <para>
/// A result is considered cancelled when the window is closed without calling
/// <see cref="WindowHandle.CloseWithResult{T}(T)"/> - for example, via the close button
/// or Escape key.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// e.Windows.Window(w =&gt; w.VStack(v =&gt; [
///     v.Text("Delete this item?"),
///     v.HStack(h =&gt; [
///         h.Button("Yes").OnClick(_ =&gt; w.Window.CloseWithResult(true)),
///         h.Button("No").OnClick(_ =&gt; w.Window.CloseWithResult(false))
///     ])
/// ]))
/// .Title("Confirm Delete")
/// .Modal()
/// .OnResult&lt;bool&gt;(result =&gt; {
///     if (!result.IsCancelled &amp;&amp; result.Value)
///     {
///         DeleteItem();
///     }
/// })
/// .Open(e.Windows);
/// </code>
/// </example>
public sealed class WindowResultContext<T>
{
    internal WindowResultContext(WindowEntry window, T? value, bool isCancelled)
    {
        Window = window;
        Value = value;
        IsCancelled = isCancelled;
    }

    /// <summary>
    /// The window entry that was closed.
    /// </summary>
    public WindowEntry Window { get; }

    /// <summary>
    /// The result value provided via <see cref="WindowHandle.CloseWithResult{T}(T)"/>.
    /// This is the default value of <typeparamref name="T"/> when <see cref="IsCancelled"/> is true.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Whether the dialog was cancelled (closed without providing a result).
    /// When true, <see cref="Value"/> should not be used.
    /// </summary>
    public bool IsCancelled { get; }

    /// <summary>
    /// Convenience accessor for the window manager.
    /// Use this to open additional windows or perform other window operations.
    /// </summary>
    public WindowManager Windows => Window.Manager;
}
