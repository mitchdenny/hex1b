using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that catches exceptions and displays a fallback when errors occur.
/// Similar to React's ErrorBoundary pattern.
/// </summary>
/// <param name="Child">The child widget to render (may throw during any lifecycle phase).</param>
public sealed record RescueWidget(Hex1bWidget? Child) : Hex1bWidget
{
    /// <summary>
    /// An exception that occurred during the Build phase (widget construction).
    /// If set, the rescue widget will immediately show the fallback.
    /// </summary>
    internal Exception? BuildException { get; init; }

    /// <summary>
    /// Whether to show detailed exception information (stack trace, etc.).
    /// Defaults to true in DEBUG builds, false in RELEASE builds.
    /// </summary>
    public bool ShowDetails { get; init; } =
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>
    /// Optional custom fallback widget builder. Receives a RescueContext for building the fallback UI.
    /// If null, a default fallback is used.
    /// </summary>
    internal Func<RescueContext, Hex1bWidget>? FallbackBuilder { get; init; }

    /// <summary>
    /// Handler called when an exception is caught.
    /// </summary>
    internal Func<RescueEventArgs, Task>? RescueHandler { get; init; }

    /// <summary>
    /// Handler called after the rescue state is reset (e.g., when user clicks Retry).
    /// </summary>
    internal Func<RescueResetEventArgs, Task>? ResetHandler { get; init; }

    /// <summary>
    /// Sets a synchronous handler called when an exception is caught.
    /// </summary>
    /// <param name="handler">The handler to invoke when an error occurs.</param>
    /// <returns>A new RescueWidget with the handler set.</returns>
    public RescueWidget OnRescue(Action<RescueEventArgs> handler)
        => this with { RescueHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when an exception is caught.
    /// </summary>
    /// <param name="handler">The async handler to invoke when an error occurs.</param>
    /// <returns>A new RescueWidget with the handler set.</returns>
    public RescueWidget OnRescue(Func<RescueEventArgs, Task> handler)
        => this with { RescueHandler = handler };

    /// <summary>
    /// Sets a synchronous handler called after the rescue state is reset.
    /// This is called after internal cleanup when the user triggers a retry.
    /// </summary>
    /// <param name="handler">The handler to invoke after reset.</param>
    /// <returns>A new RescueWidget with the handler set.</returns>
    public RescueWidget OnReset(Action<RescueResetEventArgs> handler)
        => this with { ResetHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called after the rescue state is reset.
    /// This is called after internal cleanup when the user triggers a retry.
    /// </summary>
    /// <param name="handler">The async handler to invoke after reset.</param>
    /// <returns>A new RescueWidget with the handler set.</returns>
    public RescueWidget OnReset(Func<RescueResetEventArgs, Task> handler)
        => this with { ResetHandler = handler };

    /// <summary>
    /// Sets a custom fallback builder for rendering the error UI.
    /// The builder receives a RescueContext with error information and a Reset() method.
    /// </summary>
    /// <param name="builder">A function that builds the fallback widget.</param>
    /// <returns>A new RescueWidget with the custom fallback.</returns>
    /// <example>
    /// <code>
    /// ctx.Rescue(ctx.SomeWidget())
    ///    .WithFallback(rescue => rescue.VStack(v => [
    ///        v.Text($"Error: {rescue.Exception.Message}"),
    ///        v.Button("Retry").OnClick(_ => rescue.Reset())
    ///    ]))
    /// </code>
    /// </example>
    public RescueWidget WithFallback(Func<RescueContext, Hex1bWidget> builder)
        => this with { FallbackBuilder = builder };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as RescueNode ?? new RescueNode();

        node.SourceWidget = this;
        node.ShowDetails = ShowDetails;
        node.FallbackBuilder = FallbackBuilder;
        node.RescueHandler = RescueHandler;
        node.ResetHandler = ResetHandler;
        node.FocusRing = context.FocusRing; // Store for lazy fallback creation

        // If there was a Build phase exception (from the widget builder), capture it now
        if (BuildException != null && !node.HasError)
        {
            await node.CaptureErrorAsync(BuildException, RescueErrorPhase.Build);
        }

        // If we're in error state (from Build or previous error), show fallback
        if (node.HasError)
        {
            // Build and reconcile the fallback instead
            var fallbackWidget = node.BuildFallbackWidget();
            node.FallbackChild = await context.ReconcileChildAsync(node.FallbackChild, fallbackWidget, node);
            return node;
        }

        try
        {
            node.Child = await context.ReconcileChildAsync(node.Child, Child!, node);
            node.FallbackChild = null; // Clear any previous fallback
        }
        catch (Exception ex)
        {
            await node.CaptureErrorAsync(ex, RescueErrorPhase.Reconcile);

            // Build fallback
            var fallbackWidget = node.BuildFallbackWidget();
            node.FallbackChild = await context.ReconcileChildAsync(node.FallbackChild, fallbackWidget, node);
        }

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(RescueNode);
}
