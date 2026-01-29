namespace Hex1b.Widgets;

/// <summary>
/// Displays a progress bar that can show either determinate (known completion percentage)
/// or indeterminate (unknown completion) progress.
/// </summary>
/// <remarks>
/// <para>
/// The progress widget has two modes:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>Determinate</strong>: Shows progress as a filled bar from <see cref="Minimum"/> to <see cref="Maximum"/>.
/// The current value is set via <see cref="Value"/>. Values can be any range, not just 0-100.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Indeterminate</strong>: Shows an animated indicator when the completion amount is unknown.
/// Set <see cref="IsIndeterminate"/> to true for this mode. Animation is time-based and automatic.
/// </description>
/// </item>
/// </list>
/// <para>
/// By default, the progress bar fills the available horizontal space. Use layout extensions
/// like <c>FixedWidth()</c> to constrain its size.
/// </para>
/// </remarks>
/// <example>
/// <para>Determinate progress (0-100%):</para>
/// <code>
/// ctx.Progress(current: 75)
/// </code>
/// <para>Custom range (e.g., bytes downloaded):</para>
/// <code>
/// ctx.Progress(current: 1500, min: 0, max: 5000)
/// </code>
/// <para>Indeterminate progress (self-animating):</para>
/// <code>
/// ctx.ProgressIndeterminate()
/// </code>
/// </example>
/// <seealso cref="ProgressExtensions"/>
public sealed record ProgressWidget : Hex1bWidget
{
    /// <summary>
    /// Default animation interval for indeterminate progress bars.
    /// </summary>
    public static readonly TimeSpan DefaultAnimationInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Gets the current value of the progress bar.
    /// </summary>
    /// <remarks>
    /// This value is clamped between <see cref="Minimum"/> and <see cref="Maximum"/> during rendering.
    /// </remarks>
    public double Value { get; init; }
    
    /// <summary>
    /// Gets the minimum value of the progress range.
    /// </summary>
    /// <remarks>
    /// Can be negative. The minimum should be less than or equal to <see cref="Maximum"/>.
    /// </remarks>
    public double Minimum { get; init; }
    
    /// <summary>
    /// Gets the maximum value of the progress range.
    /// </summary>
    /// <remarks>
    /// Can be any value greater than or equal to <see cref="Minimum"/>.
    /// </remarks>
    public double Maximum { get; init; } = 100.0;
    
    /// <summary>
    /// Gets a value indicating whether the progress bar is in indeterminate mode.
    /// </summary>
    /// <remarks>
    /// When true, the progress bar shows an animated indicator instead of a fill level.
    /// The <see cref="Value"/>, <see cref="Minimum"/>, and <see cref="Maximum"/> properties are ignored.
    /// Animation is time-based and redraws are scheduled automatically.
    /// </remarks>
    public bool IsIndeterminate { get; init; }

    /// <inheritdoc/>
    internal override TimeSpan? GetEffectiveRedrawDelay()
    {
        // If explicitly set, use that value
        if (RedrawDelay.HasValue)
        {
            return RedrawDelay;
        }

        // Only auto-schedule for indeterminate mode
        return IsIndeterminate ? DefaultAnimationInterval : null;
    }

    /// <summary>
    /// Creates a new determinate progress widget.
    /// </summary>
    public ProgressWidget()
    {
    }

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ProgressNode ?? new ProgressNode();
        
        // Mark dirty if any properties changed
        if (node.Value != Value || 
            node.Minimum != Minimum || 
            node.Maximum != Maximum ||
            node.IsIndeterminate != IsIndeterminate)
        {
            node.MarkDirty();
        }
        
        node.Value = Value;
        node.Minimum = Minimum;
        node.Maximum = Maximum;
        node.IsIndeterminate = IsIndeterminate;
        
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(ProgressNode);
}
