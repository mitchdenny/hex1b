namespace Hex1b;

/// <summary>
/// Builder extensions for configuring a placeholder workload that drives the
/// terminal until the real ("primary") workload signals it is ready.
/// </summary>
public static class PlaceholderWorkloadBuilderExtensions
{
    /// <summary>
    /// Wraps the already-configured workload (the <em>primary</em>) with a
    /// <see cref="PlaceholderWorkloadAdapter"/> that initially streams from
    /// the <em>placeholder</em> workload built by <paramref name="configurePlaceholder"/>,
    /// then swaps to the primary once it is ready.
    /// </summary>
    /// <param name="builder">The terminal builder whose primary workload is being decorated.</param>
    /// <param name="configurePlaceholder">
    /// Configures the placeholder using a fresh <see cref="Hex1bTerminalBuilder"/>.
    /// Only workload-shaped configuration is honoured (e.g. <c>WithHex1bApp</c>);
    /// presentation, mouse, scrollback and filter settings on the inner builder
    /// are ignored — the outer terminal owns those.
    /// </param>
    /// <param name="resumePolicy">
    /// What to do if the primary workload disconnects after going active.
    /// Defaults to <see cref="PlaceholderResumePolicy.OnDisconnect"/>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no primary workload has been configured on
    /// <paramref name="builder"/> before this call.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The placeholder is treated as the source of truth for the screen until
    /// the primary's <see cref="IConnectableWorkloadAdapter.ConnectedTask"/>
    /// completes (or the primary first appears, for non-connectable workloads).
    /// On each swap a terminal-reset sequence is prepended to the new active's
    /// bytes so the parser drops modes / SGR / scroll-region / alt-screen state
    /// from the previous occupant.
    /// </para>
    /// <para>
    /// This must be called <strong>after</strong> the primary workload has been
    /// configured (e.g. via <c>WithHmp1UdsClient</c>, <c>WithShellProcess</c>,
    /// <c>WithHex1bApp</c>, etc.). Calling it earlier throws.
    /// </para>
    /// </remarks>
    public static Hex1bTerminalBuilder WithPlaceholderWorkload(
        this Hex1bTerminalBuilder builder,
        Action<Hex1bTerminalBuilder> configurePlaceholder,
        PlaceholderResumePolicy resumePolicy = PlaceholderResumePolicy.OnDisconnect)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurePlaceholder);

        var primaryFactory = builder.GetConfiguredWorkloadFactory();
        var primaryAdapter = builder.GetConfiguredWorkloadAdapter();

        if (primaryFactory is null && primaryAdapter is null)
        {
            throw new InvalidOperationException(
                "WithPlaceholderWorkload must be called after the primary workload has been configured " +
                "(e.g. WithHmp1UdsClient, WithShellProcess, WithHex1bApp).");
        }

        // Build the placeholder eagerly through its own builder so callers
        // can use the standard With* vocabulary; we only need the resulting
        // workload adapter — presentation/scrollback/etc. on the inner
        // builder are ignored on purpose.
        var placeholderBuilder = new Hex1bTerminalBuilder();
        configurePlaceholder(placeholderBuilder);

        var placeholderFactory = placeholderBuilder.GetConfiguredWorkloadFactory();
        var placeholderAdapterDirect = placeholderBuilder.GetConfiguredWorkloadAdapter();

        if (placeholderFactory is null && placeholderAdapterDirect is null)
        {
            throw new InvalidOperationException(
                "WithPlaceholderWorkload's configurePlaceholder callback must configure a workload on the inner builder.");
        }

        builder.ClearConfiguredWorkload();
        builder.SetWorkloadFactory(presentation =>
        {
            IHex1bTerminalWorkloadAdapter primary;
            Func<CancellationToken, Task<int>>? primaryRun = null;
            if (primaryFactory != null)
            {
                var ctx = primaryFactory(presentation);
                primary = ctx.WorkloadAdapter;
                primaryRun = ctx.RunCallback;
            }
            else
            {
                primary = primaryAdapter!;
            }

            IHex1bTerminalWorkloadAdapter placeholder;
            if (placeholderFactory != null)
            {
                // Placeholder shouldn't see the outer presentation adapter — it
                // doesn't drive the screen directly; it produces bytes consumed
                // by PlaceholderWorkloadAdapter.
                var ctx = placeholderFactory(null);
                placeholder = ctx.WorkloadAdapter;
            }
            else
            {
                placeholder = placeholderAdapterDirect!;
            }

            var wrapped = new PlaceholderWorkloadAdapter(primary, placeholder, resumePolicy);
            return new Hex1bTerminalBuildContext(wrapped, primaryRun);
        });

        return builder;
    }

    /// <summary>
    /// Convenience overload that configures a <see cref="Hex1bApp"/> as the placeholder.
    /// </summary>
    /// <param name="builder">The terminal builder whose primary workload is being decorated.</param>
    /// <param name="configure">A standard <c>WithHex1bApp</c>-shaped configuration callback.</param>
    /// <param name="resumePolicy">
    /// What to do if the primary workload disconnects after going active.
    /// Defaults to <see cref="PlaceholderResumePolicy.OnDisconnect"/>.
    /// </param>
    public static Hex1bTerminalBuilder WithPlaceholderHex1bApp(
        this Hex1bTerminalBuilder builder,
        Func<Hex1bApp, Hex1bAppOptions, Func<RootContext, Hex1b.Widgets.Hex1bWidget>> configure,
        PlaceholderResumePolicy resumePolicy = PlaceholderResumePolicy.OnDisconnect)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return builder.WithPlaceholderWorkload(b => b.WithHex1bApp(configure), resumePolicy);
    }
}
