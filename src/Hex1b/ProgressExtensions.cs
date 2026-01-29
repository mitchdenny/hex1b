namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating Progress widgets.
/// </summary>
/// <remarks>
/// <para>
/// Progress widgets come in two variants:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>Determinate</strong>: Use <see cref="Progress{TParent}(WidgetContext{TParent}, double, double, double)"/>
/// when you know the completion amount.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Indeterminate</strong>: Use <see cref="ProgressIndeterminate{TParent}(WidgetContext{TParent})"/>
/// when completion is unknown. Animation is automatic and time-based.
/// </description>
/// </item>
/// </list>
/// </remarks>
/// <example>
/// <para>Determinate progress (0-100):</para>
/// <code>
/// ctx.VStack(v => [
///     v.Text("Downloading..."),
///     v.Progress(percentComplete)
/// ])
/// </code>
/// <para>Custom range:</para>
/// <code>
/// ctx.Progress(current: bytesDownloaded, min: 0, max: totalBytes)
/// </code>
/// <para>Indeterminate (self-animating):</para>
/// <code>
/// ctx.ProgressIndeterminate()
/// </code>
/// </example>
public static class ProgressExtensions
{
    /// <summary>
    /// Creates a determinate progress bar with a value from 0 to 100.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="current">The current progress value (0-100 by default).</param>
    /// <returns>A new ProgressWidget configured for determinate mode.</returns>
    /// <example>
    /// <code>
    /// v.Progress(75) // 75% complete
    /// </code>
    /// </example>
    public static ProgressWidget Progress<TParent>(
        this WidgetContext<TParent> ctx,
        double current)
        where TParent : Hex1bWidget
        => new() { Value = current, Minimum = 0, Maximum = 100 };

    /// <summary>
    /// Creates a determinate progress bar with a custom range.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="current">The current progress value.</param>
    /// <param name="min">The minimum value of the range.</param>
    /// <param name="max">The maximum value of the range.</param>
    /// <returns>A new ProgressWidget configured for determinate mode with a custom range.</returns>
    /// <remarks>
    /// The range can include negative values. For example, a temperature progress bar
    /// might use min: -40 and max: 120.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Bytes downloaded out of total
    /// v.Progress(current: 1500, min: 0, max: 5000)
    /// 
    /// // Temperature scale
    /// v.Progress(current: 72, min: -40, max: 120)
    /// </code>
    /// </example>
    public static ProgressWidget Progress<TParent>(
        this WidgetContext<TParent> ctx,
        double current,
        double min,
        double max)
        where TParent : Hex1bWidget
        => new() { Value = current, Minimum = min, Maximum = max };

    /// <summary>
    /// Creates an indeterminate progress bar for operations with unknown completion.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <returns>A new ProgressWidget configured for indeterminate mode with automatic animation.</returns>
    /// <remarks>
    /// <para>
    /// The progress bar animates automatically using time-based animation.
    /// No external timer or manual position updates are required.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.ProgressIndeterminate()
    /// </code>
    /// </example>
    public static ProgressWidget ProgressIndeterminate<TParent>(
        this WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
        => new() { IsIndeterminate = true };
}
